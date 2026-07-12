using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;

namespace Leno.PointsMembership.Domain.Events;

/// <summary>
/// 会员等级变更集成事件，MemberLevelEvaluationJob 评估后等级发生变化时发布。
/// 消费方：消息通知域（等级变更通知）。
/// 同时实现 <see cref="IDomainEvent"/> 以便经发件箱模式在同一事务内持久化。
/// </summary>
public sealed class MemberLevelChangedEvent : IntegrationEventBase, IDomainEvent
{
    /// <summary>所属用户标识。</summary>
    public Guid UserId { get; init; }

    /// <summary>变更前等级编号。</summary>
    public int OldLevel { get; init; }

    /// <summary>变更后等级编号。</summary>
    public int NewLevel { get; init; }

    /// <summary>当前成长值。</summary>
    public int GrowthValue { get; init; }

    /// <summary>聚合根标识，用于发件箱归类。</summary>
    public Guid AggregateId => UserId;

    /// <summary>供 System.Text.Json 反序列化使用的无参构造。</summary>
    public MemberLevelChangedEvent() : base()
    {
    }

    public MemberLevelChangedEvent(Guid userId, int oldLevel, int newLevel, int growthValue)
        : base()
    {
        UserId = userId;
        OldLevel = oldLevel;
        NewLevel = newLevel;
        GrowthValue = growthValue;
    }
}