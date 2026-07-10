using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;

namespace Leno.Promotion.Domain.Events;

/// <summary>
/// 秒杀库存售罄集成事件，秒杀活动库存扣减至 0 时由 <see cref="Aggregates.SeckillActivity"/> 聚合发布。
/// 消费方：通知域（售罄通知）、商品域（商品页售罄标记）。
/// 同时实现 <see cref="IDomainEvent"/> 以便经发件箱模式在同一事务内持久化。
/// 事件契约当前定义在促销域，消费方引用本类型；后续若需跨上下文解耦可迁移至 SharedContracts。
/// </summary>
public sealed class SeckillStockSoldOutEvent : IntegrationEventBase, IDomainEvent
{
    /// <summary>秒杀活动标识。</summary>
    public Guid ActivityId { get; init; }

    /// <summary>商品 SKU 标识。</summary>
    public Guid SkuId { get; init; }

    /// <summary>售罄时间（UTC）。</summary>
    public DateTime SoldOutAt { get; init; }

    /// <summary>聚合根标识，用于发件箱归类。</summary>
    public Guid AggregateId => ActivityId;

    /// <summary>供 System.Text.Json 反序列化使用的无参构造。</summary>
    public SeckillStockSoldOutEvent() : base()
    {
    }

    public SeckillStockSoldOutEvent(Guid activityId, Guid skuId, DateTime soldOutAt)
        : base()
    {
        ActivityId = activityId;
        SkuId = skuId;
        SoldOutAt = soldOutAt;
    }
}
