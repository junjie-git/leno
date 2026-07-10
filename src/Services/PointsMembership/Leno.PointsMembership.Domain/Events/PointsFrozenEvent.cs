using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;

namespace Leno.PointsMembership.Domain.Events;

/// <summary>
/// 积分冻结集成事件，下单冻结积分时发布。
/// 同时实现 <see cref="IDomainEvent"/> 以便经发件箱模式在同一事务内持久化。
/// </summary>
public sealed class PointsFrozenEvent : IntegrationEventBase, IDomainEvent
{
    public Guid AccountId { get; init; }

    public Guid UserId { get; init; }

    public int Amount { get; init; }

    public Guid OrderId { get; init; }

    public Guid AggregateId => AccountId;

    public PointsFrozenEvent() : base()
    {
    }

    public PointsFrozenEvent(Guid accountId, Guid userId, int amount, Guid orderId)
        : base()
    {
        AccountId = accountId;
        UserId = userId;
        Amount = amount;
        OrderId = orderId;
    }
}
