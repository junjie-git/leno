namespace Leno.Order.Application.Queries;

/// <summary>
/// 订单摘要 DTO（基于 ES 读模型字段），用于列表查询场景。
/// 原 <c>OrderListResult</c> 已删除，统一使用共享 <c>PageResult&lt;OrderSummaryDto&gt;</c>。
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
