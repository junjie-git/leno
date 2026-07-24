using Leno.SharedKernel.Abstractions;

namespace Leno.Membership.Domain.Events;

/// <summary>
/// 会员等级变更领域事件，Member 聚合 EvaluateGrowthLevel 评估后成长值等级发生变化时发布。
/// 经发件箱模式在同一事务内持久化，由 IntegrationEventMapper 翻译为
/// <see cref="Leno.SharedContracts.Events.MemberLevelChangedIntegrationEvent"/> 对外发布，
/// 供 Points BC 消费发放等级提升奖励积分、消息通知域发送等级变更通知。
/// </summary>
public sealed class MemberLevelChangedDomainEvent : DomainEventBase
{
    /// <summary>所属用户标识。</summary>
    public Guid UserId { get; init; }

    /// <summary>变更前等级编号。</summary>
    public int OldLevel { get; init; }

    /// <summary>变更后等级编号。</summary>
    public int NewLevel { get; init; }

    /// <summary>当前成长值。</summary>
    public int GrowthValue { get; init; }

    public MemberLevelChangedDomainEvent(Guid userId, int oldLevel, int newLevel, int growthValue)
        : base(userId)
    {
        UserId = userId;
        OldLevel = oldLevel;
        NewLevel = newLevel;
        GrowthValue = growthValue;
    }
}
