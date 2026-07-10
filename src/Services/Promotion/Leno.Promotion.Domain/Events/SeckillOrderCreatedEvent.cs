using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;

namespace Leno.Promotion.Domain.Events;

/// <summary>
/// 秒杀订单创建集成事件，秒杀下单 Redis 预扣成功后由促销域发布。
/// 消费方：通知域（下单成功通知）、订单域（异步创建秒杀订单）。
/// 同时实现 <see cref="IDomainEvent"/> 以便经发件箱模式在同一事务内持久化。
/// 事件契约当前定义在促销域，通知域/订单域消费时引用本类型；后续若需跨上下文解耦可迁移至 SharedContracts。
/// </summary>
public sealed class SeckillOrderCreatedEvent : IntegrationEventBase, IDomainEvent
{
    /// <summary>秒杀活动标识。</summary>
    public Guid ActivityId { get; init; }

    /// <summary>商品 SKU 标识。</summary>
    public Guid SkuId { get; init; }

    /// <summary>商品 SPU 标识。</summary>
    public Guid SpuId { get; init; }

    /// <summary>下单用户标识。</summary>
    public Guid UserId { get; init; }

    /// <summary>异步创建的订单标识。</summary>
    public Guid OrderId { get; init; }

    /// <summary>秒杀价（单价）。</summary>
    public decimal SeckillPrice { get; init; }

    /// <summary>下单数量。</summary>
    public int Quantity { get; init; }

    /// <summary>币种（ISO 4217）。</summary>
    public string Currency { get; init; } = "CNY";

    /// <summary>聚合根标识，用于发件箱归类。</summary>
    public Guid AggregateId => ActivityId;

    /// <summary>供 System.Text.Json 反序列化使用的无参构造。</summary>
    public SeckillOrderCreatedEvent() : base()
    {
    }

    public SeckillOrderCreatedEvent(
        Guid activityId,
        Guid spuId,
        Guid skuId,
        Guid userId,
        Guid orderId,
        decimal seckillPrice,
        int quantity) : base()
    {
        ActivityId = activityId;
        SpuId = spuId;
        SkuId = skuId;
        UserId = userId;
        OrderId = orderId;
        SeckillPrice = seckillPrice;
        Quantity = quantity;
    }
}
