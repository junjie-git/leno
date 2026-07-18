namespace Leno.Order.Application.Queries;

/// <summary>
/// 订单列表分页查询结果（CQRS 读侧 Query Result）。
/// 字段来自 <c>OrderReadModel</c> ES 读模型：订单号、买卖双方、金额、状态、关键时间点。
/// </summary>
public sealed class OrderListResult
{
    /// <summary>当前页订单摘要列表。</summary>
    public required IReadOnlyList<OrderSummaryDto> Items { get; init; }

    /// <summary>命中总数。</summary>
    public int TotalCount { get; init; }

    /// <summary>页码，从 0 起（与 <see cref="OrderListQuery.PageIndex"/> 一致）。</summary>
    public int PageIndex { get; init; }

    /// <summary>每页条数。</summary>
    public int PageSize { get; init; }
}

/// <summary>
/// 订单摘要 DTO（基于 ES 读模型字段），用于列表查询场景。
/// </summary>
public sealed class OrderSummaryDto
{
    public Guid OrderId { get; init; }

    public string OrderNo { get; init; } = string.Empty;

    public Guid UserId { get; init; }

    /// <summary>卖家（店铺）标识，会员订阅订单可为空。</summary>
    public Guid? SellerId { get; init; }

    public decimal TotalAmount { get; init; }

    public string Currency { get; init; } = "CNY";

    /// <summary>订单状态名称（如 "PendingPayment"、"Paid"、"Shipped"）。</summary>
    public string Status { get; init; } = string.Empty;

    public DateTime CreatedAt { get; init; }

    public DateTime? PaidAt { get; init; }

    public DateTime? ShippedAt { get; init; }
}
