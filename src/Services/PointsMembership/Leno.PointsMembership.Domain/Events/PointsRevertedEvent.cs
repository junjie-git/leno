using Leno.SharedKernel.Abstractions;

namespace Leno.PointsMembership.Domain.Events;

/// <summary>
/// 积分扣回领域事件，通过 RevertPoints 扣回已发放积分时发布。
/// 经发件箱模式在同一事务内持久化，由 IntegrationEventMapper 翻译为 PointsRevertedIntegrationEvent 对外发布。
/// </summary>
public sealed class PointsRevertedEvent : DomainEventBase
{
    public Guid AccountId { get; init; }

    public Guid UserId { get; init; }

    public int Amount { get; init; }

    public Guid ReferenceId { get; init; }

    public string Reason { get; init; } = string.Empty;

    public PointsRevertedEvent(Guid accountId, Guid userId, int amount, Guid referenceId, string reason)
        : base(accountId)
    {
        AccountId = accountId;
        UserId = userId;
        Amount = amount;
        ReferenceId = referenceId;
        Reason = reason ?? string.Empty;
    }
}
