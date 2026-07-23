using Leno.AccessControl.Domain.Events;
using Leno.AccessControl.Domain.Exceptions;
using Leno.AccessControl.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;

namespace Leno.AccessControl.Domain.Aggregates;

/// <summary>
/// 用户角色分配聚合根，承载用户与角色的多对多关系。
/// 从 UserAuth BC 的 User._roles 内联集合拆出为独立聚合（3.6 AuthN/AuthZ 拆分）：
/// <list type="bullet">
/// <item>原实现：<c>User.Roles</c> 作为 owned collection 与 User 同表存储，角色变更需加载 User 聚合。</item>
/// <item>新实现：每条分配记录独立聚合，支持跨用户批量查询（GetUserRoles RPC）、审计与软删除。</item>
/// <item>不变式：用户至少保留一个角色（INV-12）；管理员不可撤销自身 Admin 角色（INV-13）。</item>
/// </list>
/// </summary>
public sealed class UserRoleAssignment : AggregateRoot
{
    /// <summary>用户标识（引用 Identity BC User.Id）。</summary>
    public Guid UserId { get; private set; }

    /// <summary>角色类型（内置角色枚举）。</summary>
    public RoleType Role { get; private set; }

    /// <summary>角色编码字符串，便于 JWT 角色声明与跨 BC 传递。</summary>
    public string RoleCode => Role.ToString();

    /// <summary>是否为当前生效分配（软删除标记，撤销后置 false）。</summary>
    public bool IsActive { get; private set; }

    /// <summary>分配时间（UTC）。</summary>
    public DateTime AssignedAt { get; private set; }

    /// <summary>撤销时间（UTC），未撤销为 null。</summary>
    public DateTime? RevokedAt { get; private set; }

    /// <summary>操作人标识，管理员操作时非空。</summary>
    public Guid? OperatorId { get; private set; }

    /// <summary>EF Core 无参构造。</summary>
    private UserRoleAssignment() { }

    private UserRoleAssignment(Guid id) : base(id) { }

    /// <summary>
    /// 工厂方法，创建用户角色分配记录。
    /// </summary>
    /// <param name="id">分配记录标识。</param>
    /// <param name="userId">用户标识。</param>
    /// <param name="role">角色类型。</param>
    /// <param name="operatorId">操作人标识，自助注册时为 null。</param>
    public static UserRoleAssignment Create(Guid id, Guid userId, RoleType role, Guid? operatorId = null)
    {
        if (id == Guid.Empty)
        {
            throw new AccessControlDomainException("用户角色分配标识不可为空", "USER_ROLE_ASSIGNMENT_ID_EMPTY");
        }

        if (userId == Guid.Empty)
        {
            throw new AccessControlDomainException("用户标识不可为空", "USER_ROLE_ASSIGNMENT_USER_EMPTY");
        }

        if (!Enum.IsDefined(role))
        {
            throw new AccessControlDomainException("未定义的角色类型", "USER_ROLE_ASSIGNMENT_ROLE_INVALID");
        }

        var assignment = new UserRoleAssignment(id)
        {
            UserId = userId,
            Role = role,
            IsActive = true,
            AssignedAt = DateTime.UtcNow,
            OperatorId = operatorId
        };

        assignment.AddDomainEvent(new UserRoleAssignedEvent(userId, role.ToString(), operatorId));
        return assignment;
    }

    /// <summary>
    /// 撤销角色分配（软删除）。
    /// 不在此处校验"至少保留一个角色"与"禁止撤销自身 Admin"，由应用服务聚合多个分配记录后统一校验，
    /// 避免聚合根单条记录无法感知用户全部角色分配。
    /// </summary>
    /// <param name="operatorId">操作人标识。</param>
    public void Revoke(Guid? operatorId = null)
    {
        if (!IsActive)
        {
            // 幂等：已撤销直接返回
            return;
        }

        IsActive = false;
        RevokedAt = DateTime.UtcNow;
        OperatorId = operatorId;

        AddDomainEvent(new UserRoleRevokedEvent(UserId, Role.ToString(), operatorId));
    }
}
