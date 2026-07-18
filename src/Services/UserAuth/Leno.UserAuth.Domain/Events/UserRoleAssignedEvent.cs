using Leno.SharedKernel.Abstractions;

namespace Leno.UserAuth.Domain.Events;

/// <summary>
/// 用户角色分配领域事件。
/// 消费方：审计域。Token 中角色声明在下一次登录或刷新后生效。
/// </summary>
public sealed class UserRoleAssignedEvent : DomainEventBase
{
    /// <summary>用户标识。</summary>
    public Guid UserId { get; init; }

    /// <summary>分配的角色编码（Buyer/Seller/Operator/Admin）。</summary>
    public string Role { get; init; } = string.Empty;

    /// <summary>操作人标识，管理员操作时非空。</summary>
    public Guid? OperatorId { get; init; }

    /// <summary>分配时间（UTC）。</summary>
    public DateTime AssignedAt { get; init; }

    public UserRoleAssignedEvent(Guid userId, string role, Guid? operatorId)
        : base(userId)
    {
        UserId = userId;
        Role = role;
        OperatorId = operatorId;
        AssignedAt = OccurredAt;
    }
}
