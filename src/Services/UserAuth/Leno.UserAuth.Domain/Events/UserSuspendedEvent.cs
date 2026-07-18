using Leno.SharedKernel.Abstractions;

namespace Leno.UserAuth.Domain.Events;

/// <summary>
/// 用户账户停用领域事件，覆盖锁定与禁用两种停用场景。
/// 消费方：消息通知域（账户异常通知）、审计域。
/// </summary>
public sealed class UserSuspendedEvent : DomainEventBase
{
    /// <summary>用户标识。</summary>
    public Guid UserId { get; init; }

    /// <summary>停用原因（登录失败锁定 / 管理员锁定 / 管理员禁用）。</summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>停用类型：Locked 或 Disabled。</summary>
    public string SuspensionType { get; init; } = string.Empty;

    /// <summary>停用时间（UTC）。</summary>
    public DateTime SuspendedAt { get; init; }

    public UserSuspendedEvent(Guid userId, string reason, string suspensionType)
        : base(userId)
    {
        UserId = userId;
        Reason = reason;
        SuspensionType = suspensionType;
        SuspendedAt = OccurredAt;
    }
}
