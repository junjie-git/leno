using Leno.SharedKernel.Abstractions;

namespace Leno.Identity.Domain.Events;

/// <summary>
/// 用户账户暂停事件（锁定 / 禁用）。
/// 从 UserAuth BC 迁入 Identity BC（3.6 AuthN/AuthZ 拆分）。
/// </summary>
public sealed class UserSuspendedEvent : DomainEventBase
{
    public Guid UserId { get; init; }
    public string Reason { get; init; } = string.Empty;
    public string SuspensionType { get; init; } = string.Empty;

    public UserSuspendedEvent(Guid userId, string reason, string suspensionType)
        : base(userId)
    {
        UserId = userId;
        Reason = reason;
        SuspensionType = suspensionType;
    }
}
