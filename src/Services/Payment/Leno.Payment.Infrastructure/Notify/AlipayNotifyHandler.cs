using Leno.Payment.Domain.Repositories;
using Leno.Payment.Domain.Services;
using Leno.Payment.Domain.ValueObjects;
using Leno.Payment.Infrastructure.Channels;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Leno.Payment.Infrastructure.Notify;

/// <summary>
/// 支付宝异步通知处理器，解析通知表单字段、验签、更新支付单/退款单状态并经发件箱发布集成事件。
/// 验签或状态机非法时返回 <c>fail</c>，通知渠道重试；处理成功返回 <c>success</c>。
/// 回调幂等：使用 Redis 记录已处理的渠道交易号，防止重复处理。
/// </summary>
public sealed class AlipayNotifyHandler
{
    private readonly AlipayAdapter _adapter;
    private readonly IPaymentOrderRepository _paymentOrderRepository;
    private readonly IRefundOrderRepository _refundOrderRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IConnectionMultiplexer? _redis;
    private readonly ILogger<AlipayNotifyHandler> _logger;

    public AlipayNotifyHandler(
        AlipayAdapter adapter,
        IPaymentOrderRepository paymentOrderRepository,
        IRefundOrderRepository refundOrderRepository,
        IUnitOfWork unitOfWork,
        IConnectionMultiplexer? redis = null,
        ILogger<AlipayNotifyHandler>? logger = null)
    {
        _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
        _paymentOrderRepository = paymentOrderRepository ?? throw new ArgumentNullException(nameof(paymentOrderRepository));
        _refundOrderRepository = refundOrderRepository ?? throw new ArgumentNullException(nameof(refundOrderRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _redis = redis;
        _logger = logger ?? InternalNullLoggerFactory.CreateLogger<AlipayNotifyHandler>();
    }

    /// <summary>
    /// 处理支付宝异步通知。
    /// </summary>
    /// <param name="rawBody">原始表单报文体。</param>
    /// <param name="formFields">表单字段字典。</param>
    /// <returns><c>success</c> 表示处理成功，<c>fail</c> 表示处理失败需重试。</returns>
    public async Task<string> HandleAsync(string rawBody, Dictionary<string, string> formFields)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(rawBody);
            ArgumentNullException.ThrowIfNull(formFields);

            var result = await _adapter.VerifyNotifyAsync(rawBody, formFields);

            if (!result.Verified)
            {
                _logger.LogWarning("支付宝通知验签失败 ChannelTradeNo={ChannelTradeNo}", result.ChannelTradeNo);
                return "fail";
            }

            // 回调幂等：使用 Redis 记录已处理的渠道交易号
            var channelTradeNo = result.ChannelTradeNo;
            if (!string.IsNullOrEmpty(channelTradeNo))
            {
                if (!await MarkCallbackProcessedAsync(channelTradeNo))
                {
                    _logger.LogInformation("支付宝通知：回调已处理，幂等跳过 ChannelTradeNo={ChannelTradeNo}", channelTradeNo);
                    return "success";
                }
            }

            if (result.IsPaid)
            {
                return await HandlePaymentNotifyAsync(formFields, result);
            }

            if (result.IsRefund)
            {
                return await HandleRefundNotifyAsync(formFields);
            }

            _logger.LogInformation("支付宝通知：非支付/退款通知，忽略");
            return "success";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "支付宝通知处理异常");
            return "fail";
        }
    }

    private async Task<string> HandlePaymentNotifyAsync(Dictionary<string, string> fields, ChannelNotifyResult result)
    {
        var outTradeNo = GetField(fields, "out_trade_no");
        var order = await _paymentOrderRepository.GetByOutTradeNoAsync(outTradeNo);
        if (order is null)
        {
            _logger.LogWarning("支付宝通知：支付单不存在 OutTradeNo={OutTradeNo}", outTradeNo);
            return "fail";
        }

        if (order.Status == PaymentStatus.Paid)
        {
            _logger.LogInformation("支付宝通知：支付单已支付，幂等跳过 OutTradeNo={OutTradeNo}", outTradeNo);
            return "success";
        }

        if (order.Status != PaymentStatus.Pending && order.Status != PaymentStatus.ChannelOrdered)
        {
            _logger.LogInformation("支付宝通知：支付单状态 {Status} 不可标记成功，跳过 OutTradeNo={OutTradeNo}",
                order.Status, outTradeNo);
            return "success";
        }

        var tradeNo = !string.IsNullOrEmpty(result.ChannelTradeNo) ? result.ChannelTradeNo : order.ChannelTradeNo;
        if (string.IsNullOrEmpty(tradeNo))
        {
            _logger.LogWarning("支付宝通知：缺少第三方交易号 OutTradeNo={OutTradeNo}", outTradeNo);
            return "fail";
        }

        // 支付金额强校验：渠道回调实付金额必须与本地支付单金额一致，否则记录安全告警并拒绝标记成功
        if (!result.Amount.HasValue || result.Amount.Value != order.Amount)
        {
            _logger.LogWarning("支付宝通知金额不一致，疑似伪造回调 OutTradeNo={OutTradeNo} 期望金额={Expected} 实付金额={Actual}",
                outTradeNo, order.Amount, result.Amount);
            return "fail";
        }

        order.MarkSucceeded(tradeNo, result.Amount.Value, result.PaidAt ?? DateTime.UtcNow);
        await _paymentOrderRepository.UpdateAsync(order);
        await _unitOfWork.SaveEntitiesAsync();

        _logger.LogInformation("支付宝通知：支付单已标记成功并发布 PaymentSucceededEvent OutTradeNo={OutTradeNo} PaymentId={PaymentId}",
            outTradeNo, order.Id);
        return "success";
    }

    private async Task<string> HandleRefundNotifyAsync(Dictionary<string, string> fields)
    {
        var outRefundNo = GetField(fields, "out_request_no");
        var refund = await _refundOrderRepository.GetByOutRefundNoAsync(outRefundNo);
        if (refund is null)
        {
            _logger.LogWarning("支付宝退款通知：退款单不存在 OutRefundNo={OutRefundNo}", outRefundNo);
            return "fail";
        }

        if (refund.Status == RefundStatus.Succeeded)
        {
            _logger.LogInformation("支付宝退款通知：退款单已成功，幂等跳过 OutRefundNo={OutRefundNo}", outRefundNo);
            return "success";
        }

        if (refund.Status != RefundStatus.Refunding)
        {
            _logger.LogInformation("支付宝退款通知：退款单状态 {Status} 不可标记成功，跳过 OutRefundNo={OutRefundNo}",
                refund.Status, outRefundNo);
            return "success";
        }

        var channelRefundNo = GetField(fields, "trade_no");
        if (string.IsNullOrEmpty(channelRefundNo))
        {
            channelRefundNo = refund.OutRefundNo;
        }

        refund.MarkSucceeded(channelRefundNo, DateTime.UtcNow);
        await _refundOrderRepository.UpdateAsync(refund);
        await _unitOfWork.SaveEntitiesAsync();

        _logger.LogInformation("支付宝退款通知：退款单已标记成功 OutRefundNo={OutRefundNo} RefundId={RefundId}",
            outRefundNo, refund.Id);
        return "success";
    }

    /// <summary>
    /// 标记回调已处理（Redis 幂等），返回 true 表示首次处理。
    /// </summary>
    private async Task<bool> MarkCallbackProcessedAsync(string channelTradeNo)
    {
        if (_redis is null)
        {
            return true; // Redis 不可用时放行
        }

        try
        {
            var db = _redis.GetDatabase();
            var key = $"payment:callback:alipay:{channelTradeNo}";
            return await db.StringSetAsync(key, "processed", TimeSpan.FromDays(30), When.NotExists);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "支付宝回调幂等检查异常 ChannelTradeNo={ChannelTradeNo}", channelTradeNo);
            return true; // 降级：Redis 异常时放行
        }
    }

    private static string GetField(Dictionary<string, string> dict, string key)
        => dict.TryGetValue(key, out var v) ? v : string.Empty;
}
