namespace Leno.Payment.Application;

/// <summary>
/// 支付域内部查询服务，供售后域关联支付单信息使用。
/// </summary>
public interface IPaymentInternalQueryService
{
    /// <summary>按订单标识查询支付单概要信息。</summary>
    /// <param name="orderId">订单标识。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>支付信息概要，不存在时返回 null。</returns>
    Task<PaymentInfoResultDto?> GetPaymentInfoByOrderIdAsync(Guid orderId, CancellationToken ct = default);
}

/// <summary>支付信息概要，供跨域查询使用。</summary>
public sealed class PaymentInfoResultDto
{
    public Guid PaymentId { get; set; }

    // PaymentChannel as int to avoid cross-domain enum dependency
    public int Channel { get; set; }

    public Guid OrderId { get; set; }

    // PaymentStatus as int
    public int Status { get; set; }

    /// <summary>支付金额（元）。</summary>
    public decimal Amount { get; set; }

    /// <summary>币种（ISO 4217），默认 CNY。</summary>
    public string Currency { get; set; } = "CNY";

    /// <summary>支付时间（UTC），未支付时为 null。</summary>
    public DateTime? PaidAt { get; set; }

    /// <summary>第三方交易号，未支付时为 null。</summary>
    public string? TradeNo { get; set; }

    /// <summary>已退款总金额（元），无退款时为 0。</summary>
    public decimal RefundedAmount { get; set; }
}
