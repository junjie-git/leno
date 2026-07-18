namespace Leno.Order.Application.Queries;

/// <summary>
/// 订单详情查询结果（CQRS 读侧 Query Result）。
/// 字段来自 <c>OrderReadModel</c> ES 读模型：含明细列表、金额与生命周期时间点。
/// </summary>
public sealed class OrderDetailResult
{
    public Guid OrderId { get; init; }

    public string OrderNo { get; init; } = string.Empty;

    public Guid UserId { get; init; }

    /// <summary>卖家（店铺）标识，会员订阅订单可为空。</summary>
    public Guid? SellerId { get; init; }

    /// <summary>订单类型名称（如 "Normal"、"Membership"、"Seckill"）。</summary>
    public string OrderType { get; init; } = string.Empty;

    public decimal ItemsAmount { get; init; }

    public decimal DiscountAmount { get; init; }

    public decimal PointsOffsetAmount { get; init; }

    public decimal FreightAmount { get; init; }

    public decimal TotalAmount { get; init; }

    public string Currency { get; init; } = "CNY";

    /// <summary>订单状态名称（如 "PendingPayment"、"Paid"、"Shipped"、"Completed"、"Cancelled"、"Closed"）。</summary>
    public string Status { get; init; } = string.Empty;

    public DateTime CreatedAt { get; init; }

    public DateTime? PaidAt { get; init; }

    public DateTime? ShippedAt { get; init; }

    /// <summary>完成时间（确认收货时间），未完成为 null。对应 Order 聚合的 CompletedAt。</summary>
    public DateTime? CompletedAt { get; init; }

    public DateTime? CancelledAt { get; init; }

    public IReadOnlyList<OrderItemDto> Items { get; init; } = Array.Empty<OrderItemDto>();
}

/// <summary>
/// 订单明细 DTO（基于 ES 读模型字段）。
/// </summary>
public sealed class OrderItemDto
{
    public Guid SkuId { get; init; }

    public string ProductName { get; init; } = string.Empty;

    public string SkuName { get; init; } = string.Empty;

    public decimal UnitPrice { get; init; }

    public int Quantity { get; init; }

    public decimal Subtotal { get; init; }
}
