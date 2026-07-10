using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;

namespace Leno.PointsMembership.Domain.Events;

/// <summary>
/// 积分冻结集成事件，下单使用积分抵扣预占积分后由积分域发布。
/// 消费方：订单域（关联冻结记录）、通知域（积分变动通知）。
/// 同时实现 <see cref="IDomainEvent"/> 以便经发件箱模式在同一事务内持久化。
/// </summary>
public sealed class PointsFrozenEvent : IntegrationEventBase, IDomainEvent
{
    /// <summary>积分账户标识。</summary>
    public Guid AccountId { get; init; }

    /// <summary>所属用户标识。</summary>
    public Guid UserId { get; init; }

    /// <summary>冻结积分数量。</summary>
    public int Amount { get; init; }

    /// <summary>触发冻结的订单标识。</summary>
    public Guid OrderId { get; init; }

    /// <summary>聚合根标识，用于发件箱归类。</summary>
    public Guid AggregateId => AccountId;

    /// <summary>供 System.Text.Json 反序列化使用的无参构造。</summary>
    public PointsFrozenEvent() : base()
    {
    }

    public PointsFrozenEvent(Guid accountId, Guid userId, int amount, Guid orderId) : base()
    {
        AccountId = accountId;
        UserId = userId;
        Amount = amount;
        OrderId = orderId;
    }
}
