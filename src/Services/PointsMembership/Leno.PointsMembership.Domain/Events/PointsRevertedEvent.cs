using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;

namespace Leno.PointsMembership.Domain.Events;

/// <summary>
/// 积分扣回集成事件，通过 RevertPoints 扣回已发放积分时发布。
/// 同时实现 <see cref="IDomainEvent"/> 以便经发件箱模式在同一事务内持久化。
/// </summary>
public sealed class PointsRevertedEvent : IntegrationEventBase, IDomainEvent
{
    public Guid AccountId { get; init; }

    public Guid UserId { get; init; }

    public int Amount { get; init; }

    public Guid ReferenceId { get; init; }

    public string Reason { get; init; } = string.Empty;

    public Guid AggregateId => AccountId;

    public PointsRevertedEvent() : base()
    {
    }

    public PointsRevertedEvent(Guid accountId, Guid userId, int amount, Guid referenceId, string reason)
        : base()
    {
        AccountId = accountId;
        UserId = userId;
        Amount = amount;
        ReferenceId = referenceId;
        Reason = reason ?? string.Empty;
    }
}