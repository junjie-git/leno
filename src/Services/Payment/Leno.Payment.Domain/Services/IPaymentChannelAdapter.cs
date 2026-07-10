using Leno.Payment.Domain.Aggregates;

namespace Leno.Payment.Domain.Services;

/// <summary>
/// 渠道下单结果，第三方支付渠道返回的预支付参数。
/// </summary>
public sealed class ChannelPaymentResult
{
    /// <summary>预支付标识（微信预支付会话标识）。</summary>
    public string? PrepayId { get; init; }

    /// <summary>扫码支付链接（微信 Native / 支付宝当面付）。</summary>
    public string? CodeUrl { get; init; }

    /// <summary>H5 支付跳转链接。</summary>
    public string? H5Url { get; init; }

    /// <summary>第三方交易号。</summary>
    public string? ChannelTradeNo { get; init; }
}

/// <summary>
/// 渠道支付查询结果，用于主动查询支付状态。
/// </summary>
public sealed class ChannelPaymentQueryResult
{
    /// <summary>是否已支付。</summary>
    public bool IsPaid { get; init; }

    /// <summary>第三方交易号。</summary>
    public string? ChannelTradeNo { get; init; }

    /// <summary>支付时间（UTC）。</summary>
    public DateTime? PaidAt { get; init; }
}

/// <summary>
/// 渠道退款下单结果，第三方支付渠道返回的退款受理结果。
/// </summary>
public sealed class ChannelRefundResult
{
    /// <summary>第三方退款单号。</summary>
    public string? ChannelRefundNo { get; init; }

    /// <summary>退款是否受理成功。</summary>
    public bool Succeeded { get; init; }
}

/// <summary>
/// 渠道退款查询结果，用于主动查询退款到账状态。
/// </summary>
public sealed class ChannelRefundQueryResult
{
    /// <summary>退款是否已到账。</summary>
    public bool Succeeded { get; init; }

    /// <summary>退款到账时间（UTC）。</summary>
    public DateTime? RefundedAt { get; init; }
}

/// <summary>
/// 渠道异步通知验签结果，统一封装各渠道回调报文解析后的语义字段。
/// </summary>
public sealed class ChannelNotifyResult
{
    /// <summary>验签是否通过。</summary>
    public bool Verified { get; init; }

    /// <summary>关联订单标识。</summary>
    public Guid OrderId { get; init; }

    /// <summary>第三方交易号。</summary>
    public string? ChannelTradeNo { get; init; }

    /// <summary>是否为支付成功通知。</summary>
    public bool IsPaid { get; init; }

    /// <summary>支付时间（UTC）。</summary>
    public DateTime? PaidAt { get; init; }

    /// <summary>是否为退款通知。</summary>
    public bool IsRefund { get; init; }

    /// <summary>退款金额（仅退款通知有值）。</summary>
    public decimal? RefundAmount { get; init; }
}

/// <summary>
/// 支付渠道适配器抽象契约，屏蔽各第三方支付渠道（微信/支付宝）差异。
/// 领域层依赖此抽象，基础设施层按渠道提供具体实现（如 WeChatPayChannelAdapter）。
/// </summary>
public interface IPaymentChannelAdapter
{
    /// <summary>
    /// 向第三方渠道发起下单，获取预支付参数。
    /// </summary>
    /// <param name="paymentOrder">支付单聚合。</param>
    /// <param name="ct">取消令牌。</param>
    Task<ChannelPaymentResult> CreatePaymentAsync(PaymentOrder paymentOrder, CancellationToken ct = default);

    /// <summary>
    /// 主动查询第三方渠道支付状态。
    /// </summary>
    /// <param name="outTradeNo">商户支付单号。</param>
    /// <param name="ct">取消令牌。</param>
    Task<ChannelPaymentQueryResult> QueryPaymentAsync(string outTradeNo, CancellationToken ct = default);

    /// <summary>
    /// 向第三方渠道发起退款。
    /// </summary>
    /// <param name="refundOrder">退款单聚合。</param>
    /// <param name="ct">取消令牌。</param>
    Task<ChannelRefundResult> CreateRefundAsync(RefundOrder refundOrder, CancellationToken ct = default);

    /// <summary>
    /// 主动查询第三方渠道退款到账状态。
    /// </summary>
    /// <param name="outRefundNo">商户退款单号。</param>
    /// <param name="ct">取消令牌。</param>
    Task<ChannelRefundQueryResult> QueryRefundAsync(string outRefundNo, CancellationToken ct = default);

    /// <summary>
    /// 验证并解析第三方渠道异步通知报文。
    /// </summary>
    /// <param name="rawBody">原始报文体。</param>
    /// <param name="headers">通知请求头字典。</param>
    /// <param name="ct">取消令牌。</param>
    Task<ChannelNotifyResult> VerifyNotifyAsync(string rawBody, Dictionary<string, string> headers, CancellationToken ct = default);
}
