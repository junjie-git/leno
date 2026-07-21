using Leno.SharedKernel.Abstractions;

namespace Leno.PointsMembership.Domain.Events;

/// <summary>
/// 会员等级升级领域事件，会员累计消费达门槛触发升级时发布。
/// 经发件箱模式在同一事务内持久化，由 IntegrationEventMapper 翻译为集成事件版
/// <see cref="Leno.SharedContracts.Events.MemberLevelUpgradedEvent"/> 对外发布，
/// 供 <c>MemberLevelUpgradedReadModelSyncConsumer</c> 重建 ES 读模型。
/// PM-M05 修复：新增 MemberId 字段，供 mapper 填充集成事件的 MemberId。
/// </summary>
public sealed class MemberLevelUpgradedEvent : DomainEventBase
{
    /// <summary>会员标识（聚合根 Id），供 mapper 填充集成事件 MemberId。</summary>
    public Guid MemberId { get; init; }

    public Guid UserId { get; init; }

    public int OldLevel { get; init; }

    public int NewLevel { get; init; }

    public DateTime UpgradedAt { get; init; }

    public MemberLevelUpgradedEvent(Guid memberId, Guid userId, int oldLevel, int newLevel, DateTime upgradedAt)
        : base(userId)
    {
        MemberId = memberId;
        UserId = userId;
        OldLevel = oldLevel;
        NewLevel = newLevel;
        UpgradedAt = upgradedAt;
    }
}
