using Leno.SharedKernel.Abstractions;

namespace Leno.Order.Domain.Events;

/// <summary>
/// 库存预占领域事件，订单域在 <see cref="Aggregates.StockReservation"/> 预占库存时由聚合收集。
/// mapper 翻译为 <see cref="Leno.SharedContracts.Events.StockReservedIntegrationEvent"/> 集成事件对外发布（若需跨上下文消费）。
/// 当前无跨上下文消费方，事件仅在本上下文内消费。
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
