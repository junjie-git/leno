using Leno.SharedKernel.Abstractions;

namespace Leno.Promotion.Domain.Events;

/// <summary>
/// 秒杀订单确认领域事件。
/// 实际发布方为订单域（经 <c>SeckillOrderConfirmedIntegrationEvent</c> 集成事件），促销域消费后标记预占记录履约。
/// 此处保留为 <see cref="DomainEventBase"/> 子类供促销域内部聚合（如未来扩展）收集使用，
/// 当前无聚合收集，mapper 翻译规则为防御性注册。
/// </summary>
public sealed class SeckillOrderConfirmedEvent : DomainEventBase
{
    /// <summary>秒杀活动标识。</summary>
    public Guid ActivityId { get; init; }

    /// <summary>订单标识。</summary>
    public Guid OrderId { get; init; }

    public SeckillOrderConfirmedEvent(Guid activityId, Guid orderId) : base(orderId)
    {
        ActivityId = activityId;
        OrderId = orderId;
    }
}
