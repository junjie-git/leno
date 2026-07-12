using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;

namespace Leno.PointsMembership.Domain.Events;

/// <summary>
/// 积分过期集成事件，积分账户中过期积分被清理时发布。
/// 消费方：消息通知域（积分过期通知）。
/// 同时实现 <see cref="IDomainEvent"/> 以便经发件箱模式在同一事务内持久化。
/// </summary>
public sealed class PointsExpiredEvent : IntegrationEventBase, IDomainEvent
{
    /// <summary>所属用户标识。</summary>
    public Guid UserId { get; init; }

    /// <summary>过期积分数量。</summary>
    public int Points { get; init; }

    /// <summary>过期时间（UTC）。</summary>
    public DateTime ExpiredAt { get; init; }

    /// <summary>聚合根标识，用于发件箱归类。</summary>
    public Guid AggregateId => UserId;

    /// <summary>供 System.Text.Json 反序列化使用的无参构造。</summary>
    public PointsExpiredEvent() : base()
    {
    }

    public PointsExpiredEvent(Guid userId, int points, DateTime expiredAt)
        : base()
    {
        UserId = userId;
        Points = points;
        ExpiredAt = expiredAt;
    }
}