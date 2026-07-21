namespace Leno.ReviewAfterSales.Domain.Services;

/// <summary>
/// 订单状态查询防腐层接口（M4 双轨方案抽取）。
/// 仅负责远程查询订单状态，业务规则校验由 <see cref="IAfterSalesEligibilityChecker"/> / <see cref="IReviewEligibilityChecker"/> 实现。
/// 实现位于基础设施层，通过 HttpClient 或 gRPC 调用订单域内部接口。
/// </summary>
public interface IOrderStatusProvider
{
    /// <summary>
    /// 查询订单状态（含用户标识、完成时间、订单行概要）。
    /// 远程失败抛 <see cref="Leno.SharedKernel.Exceptions.DomainException"/>，订单不存在抛同异常（ORDER_REMOTE_FAILED）。
    /// </summary>
    Task<OrderStatusInfo?> GetOrderStatusAsync(Guid orderId, CancellationToken ct = default);
}

/// <summary>订单状态概要信息（M4 双轨方案抽取）。</summary>
public sealed class OrderStatusInfo
{
    public Guid OrderId { get; init; }
    public int Status { get; init; }
    public Guid UserId { get; init; }
    /// <summary>订单归属卖家标识，由订单域防腐层查询填充，用于防止客户端伪造 SellerId。</summary>
    public Guid SellerId { get; init; }
    public DateTime CompletedAt { get; init; }
    public DateTime CreatedAt { get; init; }
    public List<OrderItemStatusInfo> Items { get; init; } = [];
}

/// <summary>订单行状态概要（M4 双轨方案抽取）。</summary>
public sealed class OrderItemStatusInfo
{
    public Guid OrderLineId { get; init; }
    public Guid SkuId { get; init; }
    /// <summary>SPU 标识，由订单域防腐层查询填充，用于防止客户端伪造 SpuId。</summary>
    public Guid SpuId { get; init; }
    public int Quantity { get; init; }
    public int AfterSalesStatus { get; init; }
}
