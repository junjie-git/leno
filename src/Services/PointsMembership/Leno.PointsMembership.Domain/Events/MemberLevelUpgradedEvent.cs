using Leno.SharedKernel.Abstractions;

namespace Leno.PointsMembership.Domain.Events;

/// <summary>
/// 会员等级升级领域事件，会员累计消费达门槛触发升级时发布。
/// 经发件箱模式在同一事务内持久化，由 IntegrationEventMapper 翻译为 MemberLevelChangedIntegrationEvent 对外发布。
/// </summary>
public sealed class MemberLevelUpgradedEvent : DomainEventBase
{
    public Guid UserId { get; init; }

    public int OldLevel { get; init; }

    public int NewLevel { get; init; }

    public DateTime UpgradedAt { get; init; }

    public MemberLevelUpgradedEvent(Guid userId, int oldLevel, int newLevel, DateTime upgradedAt)
        : base(userId)
    {
        UserId = userId;
        OldLevel = oldLevel;
        NewLevel = newLevel;
        UpgradedAt = upgradedAt;
    }
}
