using Leno.SharedKernel.Abstractions;

namespace Leno.Promotion.Domain.Events;

/// <summary>
/// 秒杀订单创建失败领域事件。
/// 实际发布方为订单域（经 <c>SeckillOrderCreationFailedIntegrationEvent</c> 集成事件），促销域消费后回退库存。
/// 此处保留为 <see cref="DomainEventBase"/> 子类供促销域内部聚合（如未来扩展）收集使用，
/// 当前无聚合收集，mapper 翻译规则为防御性注册。
/// </summary>
public sealed class SeckillOrderCreationFailedEvent : DomainEventBase
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

    public SeckillOrderCreationFailedEvent(
        Guid activityId,
        Guid skuId,
        Guid userId,
        Guid orderId,
        int quantity,
        string reason) : base(orderId)
    {
        ActivityId = activityId;
        SkuId = skuId;
        UserId = userId;
        OrderId = orderId;
        Quantity = quantity;
        Reason = reason ?? string.Empty;
    }
}
