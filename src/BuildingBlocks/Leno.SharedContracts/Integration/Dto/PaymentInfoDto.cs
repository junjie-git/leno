namespace Leno.SharedContracts.Integration.Dto;

/// <summary>
/// 支付信息共享 DTO（D2.2 ACL 模式去重）。
/// 各 BC 的 PaymentInfoQueryService 防腐层统一返回此类型，消除 3 BC 重复定义。
/// </summary>
public sealed class PaymentInfoDto
{
    /// <summary>订单标识。</summary>
    public Guid OrderId { get; init; }

    /// <summary>支付单标识。</summary>
    public Guid PaymentId { get; init; }

    /// <summary>支付金额（分）。</summary>
    public long AmountCents { get; init; }

    /// <summary>支付状态（如 "Paid"、"Pending"、"Refunded"）。</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>支付渠道（如 "WeChatPay"、"Alipay"）。</summary>
    public string Channel { get; init; } = string.Empty;

    /// <summary>支付完成时间（UTC），未支付为 default。</summary>
    public DateTime PaidAt { get; init; }
}
