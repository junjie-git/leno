using Leno.SharedKernel.Abstractions;

namespace Leno.Inventory.Domain.Events;

/// <summary>
/// 库存预占领域事件，由 <see cref="Aggregates.StockReservation"/> 聚合在预占库存时收集。
/// mapper 翻译为 <see cref="Leno.SharedContracts.Integration.Inventory.StockReservedIntegrationEvent"/> 集成事件对外发布。
/// </summary>
public sealed class StockReservedEvent : DomainEventBase
{
    /// <summary>SKU 标识。</summary>
    public Guid SkuId { get; init; }

    /// <summary>关联订单标识。</summary>
    public Guid OrderId { get; init; }

    /// <summary>预占数量。</summary>
    public int Quantity { get; init; }

    public StockReservedEvent(Guid skuId, Guid orderId, int quantity)
        : base(skuId)
    {
        SkuId = skuId;
        OrderId = orderId;
        Quantity = quantity;
    }
}
