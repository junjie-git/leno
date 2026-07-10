using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;

namespace Leno.PointsMembership.Domain.Events;

/// <summary>
/// 会员等级提升集成事件，累计消费达到更高门槛后由会员域发布。
/// 消费方：通知域（升级通知）、权益域（按新等级发放权益）。
/// 同时实现 <see cref="IDomainEvent"/> 以便经发件箱模式在同一事务内持久化。
/// </summary>
public sealed class MemberLevelUpgradedEvent : IntegrationEventBase, IDomainEvent
{
    /// <summary>会员所属用户标识。</summary>
    public Guid UserId { get; init; }

    /// <summary>原等级编号。</summary>
    public int OldLevel { get; init; }

    /// <summary>新等级编号。</summary>
    public int NewLevel { get; init; }

    /// <summary>升级时间（UTC）。</summary>
    public DateTime UpgradedAt { get; init; }

    /// <summary>聚合根标识，用于发件箱归类。</summary>
    public Guid AggregateId => UserId;

    /// <summary>供 System.Text.Json 反序列化使用的无参构造。</summary>
    public MemberLevelUpgradedEvent() : base()
    {
    }

    public MemberLevelUpgradedEvent(Guid userId, int oldLevel, int newLevel, DateTime upgradedAt) : base()
    {
        UserId = userId;
        OldLevel = oldLevel;
        NewLevel = newLevel;
        UpgradedAt = upgradedAt;
    }
}
