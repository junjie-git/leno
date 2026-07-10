namespace Leno.Payment.Application.Services;

/// <summary>
/// 渠道状态主动查询服务（防腐层接口）。
/// 应用层通过此抽象主动查询第三方渠道支付/退款状态，具体实现由基础设施层提供（调用渠道适配器）。
/// </summary>
public interface IChannelStatusQueryService
{
    /// <summary>
    /// 主动查询渠道支付状态。
    /// </summary>
    /// <param name="channel">支付渠道。</param>
    /// <param name="outTradeNo">商户支付单号。</param>
    Task<ChannelStatusResult> QueryPaymentStatusAsync(Leno.Payment.Domain.ValueObjects.PaymentChannel channel, string outTradeNo, CancellationToken ct = default);

    /// <summary>
    /// 主动查询渠道退款状态。
    /// </summary>
    /// <param name="channel">支付渠道。</param>
    /// <param name="outTradeNo">原支付单商户单号。</param>
    /// <param name="outRefundNo">商户退款单号。</param>
    Task<ChannelRefundStatusResult> QueryRefundStatusAsync(Leno.Payment.Domain.ValueObjects.PaymentChannel channel, string outTradeNo, string outRefundNo, CancellationToken ct = default);
}

/// <summary>
/// 渠道支付状态查询结果。
/// </summary>
public sealed class ChannelStatusResult
{
    public bool IsPaid { get; init; }
    public string? ChannelTradeNo { get; init; }
    public DateTime? PaidAt { get; init; }
}

/// <summary>
/// 渠道退款状态查询结果。
/// </summary>
public sealed class ChannelRefundStatusResult
{
    public bool Succeeded { get; init; }
    public DateTime? RefundedAt { get; init; }
}
