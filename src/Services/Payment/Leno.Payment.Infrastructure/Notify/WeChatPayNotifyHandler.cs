using Leno.Payment.Domain.Repositories;
using Leno.Payment.Domain.Services;
using Leno.Payment.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;

namespace Leno.Payment.Infrastructure.Notify;

/// <summary>
/// 微信支付异步通知处理器，验签后基于 <see cref="ChannelNotifyResult"/> 更新支付单/退款单状态并经发件箱发布集成事件。
/// 验签或状态机非法时返回 <c>FAIL</c>，通知渠道重试；处理成功返回 <c>SUCCESS</c>。
/// 回调幂等：使用 Redis 记录已处理的渠道交易号，防止重复处理。
/// </summary>
/// <remarks>
/// P0-1 修复：移除验签前 <c>ParseXml</c> 调用。微信 V3 回调报文为 JSON 而非 XML，
/// 验签前调用 <c>ParseXml</c> 会抛 <c>XmlException</c> 被外层 catch 吞掉返回 <c>FAIL</c>，
/// 导致所有 V3 回调永远无法处理。修复后先验签，验签失败直接返回 <c>FAIL</c>，
/// 验签成功后直接使用 <see cref="ChannelNotifyResult"/> 中的字段，由 <c>WeChatPayAdapter</c> 解析解密数据填充。
/// </remarks>
public sealed class WeChatPayNotifyHandler
{
    private readonly IPaymentChannelAdapter _adapter;
    private readonly IPaymentOrderRepository _paymentOrderRepository;
    private readonly IRefundOrderRepository _refundOrderRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IConnectionMultiplexer? _redis;
    private readonly ILogger<WeChatPayNotifyHandler> _logger;

    public WeChatPayNotifyHandler(
        IPaymentChannelAdapter adapter,
        IPaymentOrderRepository paymentOrderRepository,
        IRefundOrderRepository refundOrderRepository,
        IUnitOfWork unitOfWork,
        IConnectionMultiplexer? redis = null,
        ILogger<WeChatPayNotifyHandler>? logger = null)
    {
        _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
        _paymentOrderRepository = paymentOrderRepository ?? throw new ArgumentNullException(nameof(paymentOrderRepository));
        _refundOrderRepository = refundOrderRepository ?? throw new ArgumentNullException(nameof(refundOrderRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _redis = redis;
        _logger = logger ?? NullLogger<WeChatPayNotifyHandler>.Instance;
    }

    /// <summary>
    /// 处理微信支付异步通知。先验签，验签失败直接返回 <c>FAIL</c>，不再解析未授信报文。
    /// </summary>
    /// <param name="rawBody">原始报文体（V3 为 JSON）。</param>
    /// <param name="headers">通知请求头字典。</param>
    /// <returns><c>SUCCESS</c> 表示处理成功，<c>FAIL</c> 表示处理失败需重试。</returns>
    public async Task<string> HandleAsync(string rawBody, Dictionary<string, string> headers)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(rawBody);
            ArgumentNullException.ThrowIfNull(headers);

            // 先验签，验签失败直接返回 FAIL，不解析未授信报文（P0-1 修复）
            var result = await _adapter.VerifyNotifyAsync(rawBody, headers);

            if (!result.Verified)
            {
                _logger.LogWarning("微信支付通知验签失败 ChannelTradeNo={ChannelTradeNo}", result.ChannelTradeNo);
                return "FAIL";
            }

            // 回调幂等：使用 Redis 记录已处理的渠道交易号
            var channelTradeNo = result.ChannelTradeNo;
            if (!string.IsNullOrEmpty(channelTradeNo))
            {
                if (!await MarkCallbackProcessedAsync(channelTradeNo))
                {
                    _logger.LogInformation("微信支付通知：回调已处理，幂等跳过 ChannelTradeNo={ChannelTradeNo}", channelTradeNo);
                    return "SUCCESS";
                }
            }

            if (result.IsPaid)
            {
                return await HandlePaymentNotifyAsync(result);
            }

            if (result.IsRefund)
            {
                return await HandleRefundNotifyAsync(result);
            }

            _logger.LogInformation("微信支付通知：非支付/退款通知，忽略");
            return "SUCCESS";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "微信支付通知处理异常");
            return "FAIL";
        }
    }

    private async Task<string> HandlePaymentNotifyAsync(ChannelNotifyResult result)
    {
        var outTradeNo = result.OutTradeNo;
        if (string.IsNullOrEmpty(outTradeNo))
        {
            _logger.LogWarning("微信支付通知：OutTradeNo 为空 ChannelTradeNo={ChannelTradeNo}", result.ChannelTradeNo);
            return "FAIL";
        }

        var order = await _paymentOrderRepository.GetByOutTradeNoAsync(outTradeNo);
        if (order is null)
        {
            _logger.LogWarning("微信支付通知：支付单不存在 OutTradeNo={OutTradeNo}", outTradeNo);
            return "FAIL";
        }

        if (order.Status == PaymentStatus.Paid)
        {
            _logger.LogInformation("微信支付通知：支付单已支付，幂等跳过 OutTradeNo={OutTradeNo}", outTradeNo);
            return "SUCCESS";
        }

        if (order.Status != PaymentStatus.Pending && order.Status != PaymentStatus.ChannelOrdered)
        {
            _logger.LogInformation("微信支付通知：支付单状态 {Status} 不可标记成功，跳过 OutTradeNo={OutTradeNo}",
                order.Status, outTradeNo);
            return "SUCCESS";
        }

        var tradeNo = !string.IsNullOrEmpty(result.ChannelTradeNo) ? result.ChannelTradeNo : order.ChannelTradeNo;
        if (string.IsNullOrEmpty(tradeNo))
        {
            _logger.LogWarning("微信支付通知：缺少第三方交易号 OutTradeNo={OutTradeNo}", outTradeNo);
            return "FAIL";
        }

        // 支付金额强校验：渠道回调实付金额必须与本地支付单金额一致，否则记录安全告警并拒绝标记成功
        if (!result.Amount.HasValue || result.Amount.Value != order.Amount)
        {
            _logger.LogWarning("微信支付通知金额不一致，疑似伪造回调 OutTradeNo={OutTradeNo} 期望金额={Expected} 实付金额={Actual}",
                outTradeNo, order.Amount, result.Amount);
            return "FAIL";
        }

        order.MarkSucceeded(tradeNo, result.Amount.Value, result.PaidAt ?? DateTime.UtcNow);
        await _paymentOrderRepository.UpdateAsync(order);
        await _unitOfWork.SaveEntitiesAsync();

        _logger.LogInformation("微信支付通知：支付单已标记成功并发布 PaymentSucceededEvent OutTradeNo={OutTradeNo} PaymentId={PaymentId}",
            outTradeNo, order.Id);
        return "SUCCESS";
    }

    private async Task<string> HandleRefundNotifyAsync(ChannelNotifyResult result)
    {
        // WeChatPayAdapter 在退款通知时将 out_refund_no 填入 OutTradeNo 字段，refund_id 填入 ChannelTradeNo 字段
        var outRefundNo = result.OutTradeNo;
        if (string.IsNullOrEmpty(outRefundNo))
        {
            _logger.LogWarning("微信支付通知：退款通知缺少 OutRefundNo ChannelTradeNo={ChannelTradeNo}", result.ChannelTradeNo);
            return "FAIL";
        }

        var refund = await _refundOrderRepository.GetByOutRefundNoAsync(outRefundNo);
        if (refund is null)
        {
            _logger.LogWarning("微信退款通知：退款单不存在 OutRefundNo={OutRefundNo}", outRefundNo);
            return "FAIL";
        }

        if (refund.Status == RefundStatus.Succeeded)
        {
            _logger.LogInformation("微信退款通知：退款单已成功，幂等跳过 OutRefundNo={OutRefundNo}", outRefundNo);
            return "SUCCESS";
        }

        if (refund.Status != RefundStatus.Refunding)
        {
            _logger.LogInformation("微信退款通知：退款单状态 {Status} 不可标记成功，跳过 OutRefundNo={OutRefundNo}",
                refund.Status, outRefundNo);
            return "SUCCESS";
        }

        var channelRefundNo = !string.IsNullOrEmpty(result.ChannelTradeNo) ? result.ChannelTradeNo : refund.OutRefundNo;
        refund.MarkSucceeded(channelRefundNo, DateTime.UtcNow);
        await _refundOrderRepository.UpdateAsync(refund);
        await _unitOfWork.SaveEntitiesAsync();

        _logger.LogInformation("微信退款通知：退款单已标记成功 OutRefundNo={OutRefundNo} RefundId={RefundId}",
            outRefundNo, refund.Id);
        return "SUCCESS";
    }

    /// <summary>
    /// 标记回调已处理（Redis 幂等），返回 true 表示首次处理。
    /// T19：Redis 故障时不再 fail-open 放行，向上抛出由 <see cref="HandleAsync"/> 外层 catch 返回 FAIL 让渠道重试，
    /// 由 <see cref="PaymentOrder"/> 聚合状态机兜底幂等（重复回调到达已 Paid 状态时跳过）。
    /// <see cref="_redis"/> 为 null（开发环境未配置 Redis）时仍放行，此为配置选择而非故障。
    /// </summary>
    private async Task<bool> MarkCallbackProcessedAsync(string channelTradeNo)
    {
        if (_redis is null)
        {
            return true; // Redis 未配置时放行（开发环境）
        }

        try
        {
            var db = _redis.GetDatabase();
            var key = $"payment:callback:wechatpay:{channelTradeNo}";
            return await db.StringSetAsync(key, "processed", TimeSpan.FromDays(30), When.NotExists);
        }
        catch (Exception ex)
        {
            // T19: Redis 故障不再 fail-open 放行，向上抛出由外层返回 FAIL 让渠道重试，
            // 由 PaymentOrder 聚合状态机兜底幂等
            _logger.LogError(ex, "微信支付回调幂等检查 Redis 故障 ChannelTradeNo={ChannelTradeNo}", channelTradeNo);
            throw;
        }
    }
}
