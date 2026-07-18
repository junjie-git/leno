using Leno.SharedKernel.Abstractions;

namespace Leno.Promotion.Domain.Events;

/// <summary>
/// 秒杀库存售罄领域事件，秒杀活动库存扣减至 0 时由 <see cref="Aggregates.SeckillActivity"/> 聚合收集。
/// mapper 翻译为 <see cref="Leno.SharedContracts.Events.SeckillStockSoldOutIntegrationEvent"/> 集成事件对外发布。
/// 消费方：通知域（售罄通知）、商品域（商品页售罄标记）。
/// </summary>
public sealed class SeckillStockSoldOutEvent : DomainEventBase
{
    /// <summary>秒杀活动标识。</summary>
    public Guid ActivityId { get; init; }

    /// <summary>商品 SKU 标识。</summary>
    public Guid SkuId { get; init; }

    /// <summary>售罄时间（UTC）。</summary>
    public DateTime SoldOutAt { get; init; }

    public SeckillStockSoldOutEvent(Guid activityId, Guid skuId, DateTime soldOutAt)
        : base(activityId)
    {
        ActivityId = activityId;
        SkuId = skuId;
        SoldOutAt = soldOutAt;
    }
}
