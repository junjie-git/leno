using Leno.SharedKernel.Abstractions;

namespace Leno.AccessControl.Domain.Events;

/// <summary>
/// 用户角色撤销领域事件。
/// 消费方：审计域、Identity BC（JWT 角色声明在下一次登录或刷新后失效）。
/// 从 UserAuth BC 拆分而来（3.6 AuthN/AuthZ 拆分）。
/// </summary>
public sealed class UserRoleRevokedEvent : DomainEventBase
{
    /// <summary>用户标识。</summary>
    public Guid UserId { get; init; }

    /// <summary>撤销的角色编码（Buyer/Seller/Operator/Admin）。</summary>
    public string Role { get; init; } = string.Empty;

    /// <summary>操作人标识，管理员操作时非空。</summary>
    public Guid? OperatorId { get; init; }

    /// <summary>撤销时间（UTC）。</summary>
    public DateTime RevokedAt { get; init; }

    public UserRoleRevokedEvent(Guid userId, string role, Guid? operatorId)
        : base(userId)
    {
        UserId = userId;
        Role = role;
        OperatorId = operatorId;
        RevokedAt = OccurredAt;
    }
}
