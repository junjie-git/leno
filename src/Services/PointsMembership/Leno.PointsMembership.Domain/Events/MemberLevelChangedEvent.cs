using Leno.SharedKernel.Abstractions;

namespace Leno.PointsMembership.Domain.Events;

/// <summary>
/// 会员等级变更领域事件，MemberLevelEvaluationJob 评估后等级发生变化时发布。
/// 经发件箱模式在同一事务内持久化，由 IntegrationEventMapper 翻译为 MemberLevelChangedIntegrationEvent 对外发布。
/// </summary>
public sealed class MemberLevelChangedEvent : DomainEventBase
{
    /// <summary>所属用户标识。</summary>
    public Guid UserId { get; init; }

    /// <summary>变更前等级编号。</summary>
    public int OldLevel { get; init; }

    /// <summary>变更后等级编号。</summary>
    public int NewLevel { get; init; }

    /// <summary>当前成长值。</summary>
    public int GrowthValue { get; init; }

    public MemberLevelChangedEvent(Guid userId, int oldLevel, int newLevel, int growthValue)
        : base(userId)
    {
        UserId = userId;
        OldLevel = oldLevel;
        NewLevel = newLevel;
        GrowthValue = growthValue;
    }
}
