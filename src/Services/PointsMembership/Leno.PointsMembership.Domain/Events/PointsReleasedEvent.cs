using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;

namespace Leno.PointsMembership.Domain.Events;

/// <summary>
/// 积分释放集成事件，订单取消释放冻结积分时发布。
/// 同时实现 <see cref="IDomainEvent"/> 以便经发件箱模式在同一事务内持久化。
/// </summary>
public sealed class PointsReleasedEvent : IntegrationEventBase, IDomainEvent
{
    public Guid AccountId { get; init; }

    public Guid UserId { get; init; }

    public int Amount { get; init; }

    public Guid OrderId { get; init; }

    public Guid AggregateId => AccountId;

    public PointsReleasedEvent() : base()
    {
    }

    public PointsReleasedEvent(Guid accountId, Guid userId, int amount, Guid orderId)
        : base()
    {
        AccountId = accountId;
        UserId = userId;
        Amount = amount;
        OrderId = orderId;
    }
}
