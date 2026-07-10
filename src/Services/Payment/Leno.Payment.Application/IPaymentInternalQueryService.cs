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
}
