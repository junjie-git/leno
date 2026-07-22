using Leno.Order.Domain.Aggregates;
using Leno.Order.Domain.Exceptions;
using Leno.Order.Domain.Services;
using Leno.Order.Domain.ValueObjects;
using OrderAggregate = Leno.Order.Domain.Aggregates.Order;

namespace Leno.Order.Infrastructure.Services;

/// <summary>
/// 订单定价预览领域服务实现（P1-T18）。
/// 内部构造一个临时 <see cref="Order"/> 聚合实例（不持久化），复用其 ApplyDiscount / ApplyPointsOffset / RecalculateTotal 不变量与公式，
/// 替代应用层 PreviewAsync 中重复的金额计算逻辑。
/// 临时聚合使用 <see cref="OrderType.Membership"/> 以绕过 sellerId 非空校验（预览场景可能跨多卖家汇总）。
/// </summary>
public sealed class OrderPricingPreviewService : IOrderPricingPreviewService
{
    private readonly IOrderPricingDomainService _pricingDomainService;

    public OrderPricingPreviewService(IOrderPricingDomainService pricingDomainService)
    {
        ArgumentNullException.ThrowIfNull(pricingDomainService);
        _pricingDomainService = pricingDomainService;
    }

    /// <inheritdoc />
    public async Task<OrderPreviewResult> PreviewAsync(
        IReadOnlyList<OrderPreviewItem> items,
        decimal totalDiscount,
        decimal pointsOffsetRaw,
        decimal freightAmount,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (items.Count == 0)
        {
            throw new OrderDomainException("预览明细不可为空", "ORDER_PREVIEW_ITEMS_EMPTY");
        }

        if (freightAmount < 0)
        {
            throw new OrderDomainException("运费金额不可为负", "ORDER_FREIGHT_INVALID");
        }

        // 1. 构建订单明细（OrderItem.Create 已校验单价、数量）
        // 使用非空占位 Guid 绕过 ProductSnapshot.Create 的 sellerId/spuId 非空校验（预览不持久化）
        var placeholderId = Guid.NewGuid();
        var itemSubtotals = new List<(Guid SkuId, decimal Subtotal)>(items.Count);
        var orderItems = new List<OrderItem>(items.Count);
        foreach (var item in items)
        {
            var snapshot = ProductSnapshot.Create(
                item.SkuId, placeholderId, item.ProductName, item.ProductName, null, placeholderId);
            var orderItem = OrderItem.Create(
                Guid.NewGuid(), item.SkuId, snapshot, item.UnitPrice, item.Quantity, null);
            orderItems.Add(orderItem);
            itemSubtotals.Add((item.SkuId, orderItem.Subtotal));
        }

        // 2. 优惠分摊（复用 OrderPricingDomainService 的比例分摊逻辑，已校验 totalDiscount ≤ sumSubtotals）
        var allocations = totalDiscount > 0
            ? await _pricingDomainService.CalculateAndAllocateAsync(totalDiscount, itemSubtotals, ct)
            : new List<(Guid SkuId, decimal Allocation)>(0);

        // 3. 临时聚合：使用 Membership 类型绕过 sellerId 非空校验；积分初始为 0，由 ApplyPointsOffset 校验裁剪
        var previewOrder = OrderAggregate.Create(
            orderId: Guid.NewGuid(),
            orderNo: "PREVIEW",
            orderType: OrderType.Membership,
            userId: placeholderId,
            sellerId: Guid.Empty,
            items: orderItems,
            address: AddressSnapshot.Create("PREVIEW", "00000000000", "PREVIEW", "PREVIEW", "PREVIEW", "PREVIEW"),
            freightAmount: freightAmount,
            pointsOffsetAmount: 0m,
            expireAt: DateTime.UtcNow.AddMinutes(1));
        // 预览不发布领域事件（Order.Create 会发布 OrderCreatedDomainEvent）
        previewOrder.ClearDomainEvents();

        // 4. 应用优惠分摊（聚合根校验分摊总和与单项上限，等价于 PreviewAsync 原有逻辑）
        if (totalDiscount > 0 && allocations.Count > 0)
        {
            previewOrder.ApplyDiscount(totalDiscount, allocations);
        }

        // 5. 应用积分抵现（聚合根校验 0 ≤ pointsOffset ≤ ItemsAmount - DiscountAmount 与 MaxPointsOffsetAmount，等价于原有 maxOffset 裁剪）
        var pointsOffset = pointsOffsetRaw;
        if (pointsOffset < 0)
        {
            pointsOffset = 0m;
        }
        if (pointsOffset > 0)
        {
            previewOrder.ApplyPointsOffset(pointsOffset);
        }

        // 6. 复用聚合根 TotalAmount（RecalculateTotal 公式），无需应用层重复实现
        var itemDetails = orderItems
            .Select(o => new OrderPreviewItemDetail
            {
                SkuId = o.SkuId,
                ProductName = o.ProductSnapshot.ProductName,
                UnitPrice = o.UnitPrice,
                Quantity = o.Quantity,
                Subtotal = o.Subtotal,
                DiscountAllocation = o.DiscountAllocation
            })
            .ToList();

        return new OrderPreviewResult
        {
            ItemsAmount = previewOrder.ItemsAmount,
            DiscountAmount = previewOrder.DiscountAmount,
            PointsOffsetAmount = previewOrder.PointsOffsetAmount,
            FreightAmount = previewOrder.FreightAmount,
            TotalAmount = previewOrder.TotalAmount,
            Items = itemDetails
        };
    }
}
