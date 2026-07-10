using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;

namespace Leno.Order.Domain.Events;

/// <summary>
/// 库存预占集成事件，订单域在 <see cref="Aggregates.StockReservation"/> 预占库存时发布。
/// 消费方：库存（记录预占明细）。
/// 同时实现 <see cref="IDomainEvent"/> 以便订单域经发件箱模式在同一事务内持久化。
/// </summary>
public sealed class StockReservedEvent : IntegrationEventBase, IDomainEvent
{
    /// <summary>SKU 标识。</summary>
    public Guid SkuId { get; init; }

    /// <summary>关联订单标识。</summary>
    public Guid OrderId { get; init; }

    /// <summary>预占数量。</summary>
    public int Quantity { get; init; }

    /// <summary>聚合根标识，用于发件箱归类。</summary>
    public Guid AggregateId => SkuId;

    /// <summary>供 System.Text.Json 反序列化使用的无参构造。</summary>
    public StockReservedEvent() : base()
    {
    }

    public StockReservedEvent(Guid skuId, Guid orderId, int quantity) : base()
    {
        SkuId = skuId;
        OrderId = orderId;
        Quantity = quantity;
    }
}
