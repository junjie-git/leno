using Leno.Order.Domain.ValueObjects;

namespace Leno.Order.Application.DTOs;

/// <summary>
/// 订单明细 DTO，表达单 SKU 的下单快照与分摊优惠。
/// </summary>
public sealed class OrderItemDto
{
    public Guid SkuId { get; init; }

    public string ProductName { get; init; } = string.Empty;

    public string SkuName { get; init; } = string.Empty;

    public string? MainImage { get; init; }

    public decimal UnitPrice { get; init; }

    public int Quantity { get; init; }

    public decimal DiscountAllocation { get; init; }

    public decimal Subtotal { get; init; }
}

/// <summary>
/// 订单 DTO，表达订单完整视图（含明细）。
/// </summary>
public sealed class OrderDto
{
    public Guid Id { get; init; }

    public string OrderNo { get; init; } = string.Empty;

    public OrderType OrderType { get; init; }

    public Guid UserId { get; init; }

    public Guid SellerId { get; init; }

    public OrderStatus Status { get; init; }

    public decimal ItemsAmount { get; init; }

    public decimal DiscountAmount { get; init; }

    public decimal PointsOffsetAmount { get; init; }

    public decimal FreightAmount { get; init; }

    public decimal TotalAmount { get; init; }

    public PaymentMethod? PaymentMethod { get; init; }

    public DateTime ExpireAt { get; init; }

    public DateTime? PaidAt { get; init; }

    public DateTime? ShippedAt { get; init; }

    public string? LogisticsNo { get; init; }

    public string? LogisticsCompanyCode { get; init; }

    public DateTime? CompletedAt { get; init; }

    public DateTime? CancelledAt { get; init; }

    public string? CancelReason { get; init; }

    public DateTime CreatedAt { get; init; }

    public List<OrderItemDto> Items { get; init; } = new();
}

/// <summary>
/// 下单预览明细项 DTO，仅承载 SKU 与数量。
/// </summary>
public sealed class OrderPreviewItemDto
{
    public Guid SkuId { get; init; }

    public int Quantity { get; init; }
}

/// <summary>
/// 结算项 DTO，购物车结算或直接下单的单 SKU 表达。
/// </summary>
public sealed class CheckoutItemDto
{
    public Guid SkuId { get; init; }

    public int Quantity { get; init; }

    /// <summary>来源购物车项标识，非购物车来源可为空。</summary>
    public Guid? SourceCartItemId { get; init; }
}

/// <summary>
/// 创建订单 DTO，按卖家自动拆单，收货地址以快照字段直传。
/// </summary>
public sealed class CreateOrderDto
{
    public List<CheckoutItemDto> Items { get; init; } = new();

    public PaymentMethod PaymentMethod { get; init; }

    /// <summary>使用积分抵现的积分数，100 积分 = 1 元。</summary>
    public int PointsToUse { get; init; }

    public string RecipientName { get; init; } = string.Empty;

    public string RecipientPhone { get; init; } = string.Empty;

    public string Province { get; init; } = string.Empty;

    public string City { get; init; } = string.Empty;

    public string District { get; init; } = string.Empty;

    public string Detail { get; init; } = string.Empty;
}

/// <summary>
/// 立即购买 DTO，单 SKU 直传，内部转换为 <see cref="CreateOrderDto"/>。
/// </summary>
public sealed class BuyNowDto
{
    public Guid SkuId { get; init; }

    public int Quantity { get; init; }

    public PaymentMethod PaymentMethod { get; init; }

    /// <summary>使用积分抵现的积分数，100 积分 = 1 元。</summary>
    public int PointsToUse { get; init; }

    public string RecipientName { get; init; } = string.Empty;

    public string RecipientPhone { get; init; } = string.Empty;

    public string Province { get; init; } = string.Empty;

    public string City { get; init; } = string.Empty;

    public string District { get; init; } = string.Empty;

    public string Detail { get; init; } = string.Empty;
}

/// <summary>
/// 发起支付 DTO。
/// </summary>
public sealed class PayOrderDto
{
    public PaymentMethod PaymentMethod { get; init; }
}

/// <summary>
/// 发货 DTO。
/// </summary>
public sealed class ShipOrderDto
{
    public string LogisticsNo { get; init; } = string.Empty;

    public string LogisticsCompanyCode { get; init; } = string.Empty;
}

/// <summary>
/// 买家取消订单 DTO。
/// </summary>
public sealed class CancelOrderDto
{
    public string Reason { get; init; } = string.Empty;
}

/// <summary>
/// 运营强制取消订单 DTO。
/// </summary>
public sealed class ForceCancelOrderDto
{
    public string Reason { get; init; } = string.Empty;

    /// <summary>操作人标识，从 JWT 声明中解析，前端无需传入。</summary>
    public Guid OperatorId { get; init; }
}

/// <summary>
/// 订单列表分页结果 DTO。
/// </summary>
public sealed class OrderListResultDto
{
    public List<OrderDto> Items { get; init; } = new();

    public int Total { get; init; }

    public int Page { get; init; }

    public int PageSize { get; init; }
}

/// <summary>
/// 下单预览结果 DTO，表达预估金额汇总与明细。
/// </summary>
public sealed class OrderPreviewResultDto
{
    public decimal ItemsAmount { get; init; }

    public decimal DiscountAmount { get; init; }

    public decimal PointsOffsetAmount { get; init; }

    public decimal FreightAmount { get; init; }

    public decimal TotalAmount { get; init; }

    public List<PreviewItemDetail> Items { get; init; } = new();
}

/// <summary>
/// 预览明细项，含 SKU 名称、单价、数量与小计。
/// </summary>
public sealed class PreviewItemDetail
{
    public Guid SkuId { get; init; }

    public string ProductName { get; init; } = string.Empty;

    public decimal UnitPrice { get; init; }

    public int Quantity { get; init; }

    public decimal Subtotal { get; init; }
}
