using Leno.AccessControl.Domain.Aggregates;
using Leno.AccessControl.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;

namespace Leno.AccessControl.Domain.Repositories;

/// <summary>
/// 用户角色分配仓储接口，定义在领域层，由基础设施层实现。
/// 从 UserAuth BC 的 User._roles 内联集合拆出（3.6 AuthN/AuthZ 拆分）。
/// </summary>
public interface IUserRoleAssignmentRepository : IRepository<UserRoleAssignment>
{
    /// <summary>查询用户当前生效的角色分配记录（IsActive=true）。</summary>
    Task<IReadOnlyList<UserRoleAssignment>> GetActiveByUserAsync(Guid userId, CancellationToken ct = default);

    /// <summary>查询用户当前生效的角色编码列表（用于 JWT 角色声明填充）。</summary>
    Task<IReadOnlyList<string>> GetActiveRoleCodesAsync(Guid userId, CancellationToken ct = default);

    /// <summary>查询用户是否拥有指定角色。</summary>
    Task<bool> HasRoleAsync(Guid userId, RoleType role, CancellationToken ct = default);

    /// <summary>统计用户当前生效角色数量（用于"至少保留一个角色"校验）。</summary>
    Task<int> CountActiveRolesAsync(Guid userId, CancellationToken ct = default);

    /// <summary>按用户标识与角色类型查询生效的分配记录。</summary>
    Task<UserRoleAssignment?> GetActiveAssignmentAsync(Guid userId, RoleType role, CancellationToken ct = default);
}
