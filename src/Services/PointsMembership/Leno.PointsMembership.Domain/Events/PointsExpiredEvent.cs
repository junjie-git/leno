using Leno.SharedKernel.Abstractions;

namespace Leno.PointsMembership.Domain.Events;

/// <summary>
/// 积分过期领域事件，积分账户中过期积分被清理时发布。
/// 上下文内部领域事件，当前无跨上下文消费方，不翻译为集成事件。
/// </summary>
public sealed class PointsExpiredEvent : DomainEventBase
{
    /// <summary>所属用户标识。</summary>
    public Guid UserId { get; init; }

    /// <summary>过期积分数量。</summary>
    public int Points { get; init; }

    /// <summary>过期时间（UTC）。</summary>
    public DateTime ExpiredAt { get; init; }

    public PointsExpiredEvent(Guid userId, int points, DateTime expiredAt)
        : base(userId)
    {
        UserId = userId;
        Points = points;
        ExpiredAt = expiredAt;
    }
}
