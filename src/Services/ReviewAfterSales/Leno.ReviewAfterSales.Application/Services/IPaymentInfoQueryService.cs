namespace Leno.ReviewAfterSales.Application.Services;

/// <summary>
/// 支付信息查询防腐层接口，供售后域在审核通过时获取支付单标识与支付渠道。
/// 实际实现位于基础设施层，通过 HTTP 调用支付域 API 或直接查询支付域数据库（单体部署时）。
/// </summary>
public interface IPaymentInfoQueryService
{
    /// <summary>
    /// 按订单标识查询支付单信息，返回支付单标识与支付渠道。
    /// 若订单无支付单（未支付），返回 null。
    /// </summary>
    /// <param name="orderId">订单标识。</param>
    Task<PaymentInfoResult?> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default);
}

/// <summary>
/// 支付信息查询结果。
/// </summary>
public sealed class PaymentInfoResult
{
    public Guid PaymentId { get; init; }
    public string Channel { get; init; } = string.Empty;
}
