using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;

namespace Leno.Promotion.Domain.Events;

/// <summary>
/// 秒杀订单确认集成事件，订单域成功创建秒杀订单后发布。
/// 消费方：促销域（标记预占记录为已履约，补偿任务跳过）。
/// 同时实现 <see cref="IDomainEvent"/> 以便订单域经发件箱模式在同一事务内持久化。
/// </summary>
public sealed class SeckillOrderConfirmedEvent : IntegrationEventBase, IDomainEvent
{
    /// <summary>秒杀活动标识。</summary>
    public Guid ActivityId { get; init; }

    /// <summary>订单标识。</summary>
    public Guid OrderId { get; init; }

    /// <summary>聚合根标识，用于发件箱归类。</summary>
    public Guid AggregateId => OrderId;

    /// <summary>供 System.Text.Json 反序列化使用的无参构造。</summary>
    public SeckillOrderConfirmedEvent() : base()
    {
    }

    public SeckillOrderConfirmedEvent(Guid activityId, Guid orderId) : base()
    {
        ActivityId = activityId;
        OrderId = orderId;
    }
}