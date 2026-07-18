using Leno.SharedKernel.Abstractions;

namespace Leno.Promotion.Domain.Events;

/// <summary>
/// 秒杀订单创建领域事件，秒杀下单 Redis 预扣成功后由 <see cref="Aggregates.SeckillActivity"/> 聚合收集。
/// mapper 翻译为 <see cref="Leno.SharedContracts.Events.SeckillOrderCreatedIntegrationEvent"/> 集成事件对外发布。
/// 消费方：通知域（下单成功通知）、订单域（异步创建秒杀订单）。
/// </summary>
public sealed class SeckillOrderCreatedEvent : DomainEventBase
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

    public SeckillOrderCreatedEvent(
        Guid activityId,
        Guid spuId,
        Guid skuId,
        Guid userId,
        Guid orderId,
        decimal seckillPrice,
        int quantity) : base(activityId)
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
