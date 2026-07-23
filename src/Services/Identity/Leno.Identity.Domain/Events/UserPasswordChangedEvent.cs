using Leno.SharedKernel.Abstractions;

namespace Leno.Identity.Domain.Events;

/// <summary>
/// 用户密码变更领域事件。
/// 消费方：审计域；触发已有 RefreshToken 失效。
/// 从 UserAuth BC 迁入 Identity BC（3.6 AuthN/AuthZ 拆分）。
/// </summary>
public sealed class UserPasswordChangedEvent : DomainEventBase
{
    /// <summary>用户标识。</summary>
    public Guid UserId { get; init; }

    /// <summary>变更时间（UTC）。</summary>
    public DateTime ChangedAt { get; init; }

    /// <summary>是否为密码找回流程触发。</summary>
    public bool IsResetFlow { get; init; }

    public UserPasswordChangedEvent(Guid userId, bool isResetFlow = false)
        : base(userId)
    {
        UserId = userId;
        ChangedAt = OccurredAt;
        IsResetFlow = isResetFlow;
    }
}
