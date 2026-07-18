using Leno.SharedKernel.Abstractions;

namespace Leno.UserAuth.Domain.Events;

/// <summary>
/// 用户密码修改成功领域事件。
/// 消费方：消息通知域（安全通知）、安全审计。
/// </summary>
public sealed class UserPasswordChangedEvent : DomainEventBase
{
    /// <summary>用户标识。</summary>
    public Guid UserId { get; init; }

    /// <summary>密码变更时间（UTC）。</summary>
    public DateTime ChangedAt { get; init; }

    public UserPasswordChangedEvent(Guid userId)
        : base(userId)
    {
        UserId = userId;
        ChangedAt = OccurredAt;
    }
}
