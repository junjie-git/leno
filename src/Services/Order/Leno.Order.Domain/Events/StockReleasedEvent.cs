using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;

namespace Leno.Order.Domain.Events;

/// <summary>
/// 库存释放集成事件，订单域在订单取消回退预占时发布。
/// 消费方：库存（回退预占明细）。
/// 同时实现 <see cref="IDomainEvent"/> 以便订单域经发件箱模式在同一事务内持久化。
/// </summary>
public sealed class StockReleasedEvent : IntegrationEventBase, IDomainEvent
{
    /// <summary>SKU 标识。</summary>
    public Guid SkuId { get; init; }

    /// <summary>关联订单标识。</summary>
    public Guid OrderId { get; init; }

    /// <summary>释放数量。</summary>
    public int Quantity { get; init; }

    /// <summary>聚合根标识，用于发件箱归类。</summary>
    public Guid AggregateId => SkuId;

    /// <summary>供 System.Text.Json 反序列化使用的无参构造。</summary>
    public StockReleasedEvent() : base()
    {
    }

    public StockReleasedEvent(Guid skuId, Guid orderId, int quantity) : base()
    {
        SkuId = skuId;
        OrderId = orderId;
        Quantity = quantity;
    }
}
