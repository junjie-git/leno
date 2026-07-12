using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;

namespace Leno.Promotion.Domain.Events;

/// <summary>
/// 秒杀订单创建失败集成事件，订单域消费 SeckillOrderCreatedEvent 后若创建订单失败则发布此事件。
/// 消费方：促销域（回退 Redis 库存 + 回退 DB 基线）。
/// 同时实现 <see cref="IDomainEvent"/> 以便订单域经发件箱模式在同一事务内持久化。
/// </summary>
public sealed class SeckillOrderCreationFailedEvent : IntegrationEventBase, IDomainEvent
{
    /// <summary>秒杀活动标识。</summary>
    public Guid ActivityId { get; init; }

    /// <summary>商品 SKU 标识。</summary>
    public Guid SkuId { get; init; }

    /// <summary>下单用户标识。</summary>
    public Guid UserId { get; init; }

    /// <summary>订单标识（与 SeckillOrderCreatedEvent 中的 OrderId 一致）。</summary>
    public Guid OrderId { get; init; }

    /// <summary>失败原因。</summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>下单数量。</summary>
    public int Quantity { get; init; }

    /// <summary>聚合根标识，用于发件箱归类。</summary>
    public Guid AggregateId => OrderId;

    /// <summary>供 System.Text.Json 反序列化使用的无参构造。</summary>
    public SeckillOrderCreationFailedEvent() : base()
    {
    }

    public SeckillOrderCreationFailedEvent(
        Guid activityId,
        Guid skuId,
        Guid userId,
        Guid orderId,
        int quantity,
        string reason) : base()
    {
        ActivityId = activityId;
        SkuId = skuId;
        UserId = userId;
        OrderId = orderId;
        Quantity = quantity;
        Reason = reason ?? string.Empty;
    }
}