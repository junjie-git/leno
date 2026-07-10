namespace Leno.Order.Application;

/// <summary>
/// 订单域内部查询服务，供售后/评价域校验资格使用。
/// </summary>
public interface IOrderInternalQueryService
{
    Task<OrderStatusResultDto?> GetOrderStatusAsync(Guid orderId, CancellationToken ct = default);
}

/// <summary>订单状态概要，供跨域资格校验使用。</summary>
public sealed class OrderStatusResultDto
{
    public Guid OrderId { get; set; }

    public int Status { get; set; }  // OrderStatus as int to avoid cross-domain enum dependency

    public Guid UserId { get; set; }

    public DateTime CompletedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public List<OrderItemStatusDto> Items { get; set; } = [];
}

public sealed class OrderItemStatusDto
{
    public Guid OrderLineId { get; set; }

    public Guid SkuId { get; set; }

    public int Quantity { get; set; }

    public int AfterSalesStatus { get; set; }  // AfterSalesStatus as int, 0=none
}
