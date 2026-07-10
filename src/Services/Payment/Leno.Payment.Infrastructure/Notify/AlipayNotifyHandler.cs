using Leno.Payment.Domain.Repositories;
using Leno.Payment.Domain.Services;
using Leno.Payment.Domain.ValueObjects;
using Leno.Payment.Infrastructure.Channels;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;

namespace Leno.Payment.Infrastructure.Notify;

/// <summary>
/// 支付宝异步通知处理器，解析通知表单字段、验签、更新支付单/退款单状态并经发件箱发布集成事件。
/// 验签或状态机非法时返回 <c>fail</c>，通知渠道重试；处理成功返回 <c>success</c>。
/// </summary>
public sealed class AlipayNotifyHandler
{
    private readonly AlipayAdapter _adapter;
    private readonly IPaymentOrderRepository _paymentOrderRepository;
    private readonly IRefundOrderRepository _refundOrderRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AlipayNotifyHandler> _logger;

    public AlipayNotifyHandler(
        AlipayAdapter adapter,
        IPaymentOrderRepository paymentOrderRepository,
        IRefundOrderRepository refundOrderRepository,
        IUnitOfWork unitOfWork,
        ILogger<AlipayNotifyHandler> logger)
    {
        _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
        _paymentOrderRepository = paymentOrderRepository ?? throw new ArgumentNullException(nameof(paymentOrderRepository));
        _refundOrderRepository = refundOrderRepository ?? throw new ArgumentNullException(nameof(refundOrderRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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

        var channelTradeNo = !string.IsNullOrEmpty(result.ChannelTradeNo) ? result.ChannelTradeNo : order.ChannelTradeNo;
        if (string.IsNullOrEmpty(channelTradeNo))
        {
            _logger.LogWarning("支付宝通知：缺少第三方交易号 OutTradeNo={OutTradeNo}", outTradeNo);
            return "fail";
        }

        order.MarkSucceeded(channelTradeNo, result.PaidAt ?? DateTime.UtcNow);
        await _paymentOrderRepository.UpdateAsync(order);
        await _unitOfWork.SaveEntitiesAsync();

        _logger.LogInformation("支付宝通知：支付单已标记成功 OutTradeNo={OutTradeNo} PaymentId={PaymentId}",
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

    private static string GetField(Dictionary<string, string> dict, string key)
        => dict.TryGetValue(key, out var v) ? v : string.Empty;
}
