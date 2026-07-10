using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;

namespace Leno.PointsMembership.Domain.Events;

/// <summary>
/// 会员等级升级集成事件，会员累计消费达门槛触发升级时发布。
/// 消费方：消息通知域（等级升级通知）。
/// 同时实现 <see cref="IDomainEvent"/> 以便经发件箱模式在同一事务内持久化。
/// </summary>
public sealed class MemberLevelUpgradedEvent : IntegrationEventBase, IDomainEvent
{
    public Guid UserId { get; init; }

    public int OldLevel { get; init; }

    public int NewLevel { get; init; }

    public DateTime UpgradedAt { get; init; }

    public Guid AggregateId => UserId;

    public MemberLevelUpgradedEvent() : base()
    {
    }

    public MemberLevelUpgradedEvent(Guid userId, int oldLevel, int newLevel, DateTime upgradedAt)
        : base()
    {
        UserId = userId;
        OldLevel = oldLevel;
        NewLevel = newLevel;
        UpgradedAt = upgradedAt;
    }
}
