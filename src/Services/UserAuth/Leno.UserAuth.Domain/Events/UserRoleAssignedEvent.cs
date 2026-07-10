using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;

namespace Leno.UserAuth.Domain.Events;

/// <summary>
/// 用户角色分配集成事件。
/// 消费方：审计域。Token 中角色声明在下一次登录或刷新后生效。
/// </summary>
public sealed class UserRoleAssignedEvent : IntegrationEventBase, IDomainEvent
{
    /// <summary>用户标识。</summary>
    public Guid UserId { get; init; }

    /// <summary>分配的角色编码（Buyer/Seller/Operator/Admin）。</summary>
    public string Role { get; init; } = string.Empty;

    /// <summary>操作人标识，管理员操作时非空。</summary>
    public Guid? OperatorId { get; init; }

    /// <summary>分配时间（UTC）。</summary>
    public DateTime AssignedAt { get; init; }

    /// <summary>聚合根标识。</summary>
    public Guid AggregateId => UserId;

    /// <summary>供 System.Text.Json 反序列化使用的无参构造。</summary>
    public UserRoleAssignedEvent() : base()
    {
    }

    public UserRoleAssignedEvent(Guid userId, string role, Guid? operatorId)
        : base()
    {
        UserId = userId;
        Role = role;
        OperatorId = operatorId;
        AssignedAt = OccurredAt;
    }
}
