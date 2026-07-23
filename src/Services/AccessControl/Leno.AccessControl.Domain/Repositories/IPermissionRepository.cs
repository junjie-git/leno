using Leno.AccessControl.Domain.Aggregates;
using Leno.SharedKernel.Abstractions;

namespace Leno.AccessControl.Domain.Repositories;

/// <summary>
/// 角色权限仓储接口，定义在领域层，由基础设施层实现。
/// 从 UserAuth BC 迁入 AccessControl BC（3.6 AuthN/AuthZ 拆分）。
/// </summary>
public interface IPermissionRepository : IRepository<Role>
{
    /// <summary>按名称查询角色。</summary>
    Task<Role?> GetByNameAsync(string name, CancellationToken ct = default);

    /// <summary>分页查询角色列表。</summary>
    Task<(IReadOnlyList<Role> Items, int Total)> QueryAsync(
        string? keyword = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default);

    /// <summary>查询拥有指定权限的角色列表。</summary>
    Task<IReadOnlyList<Role>> GetRolesByPermissionAsync(string resourceKey, CancellationToken ct = default);

    /// <summary>检查角色是否被用户引用（通过 UserRoleAssignment 表）。</summary>
    Task<bool> HasUserReferencesAsync(Guid roleId, CancellationToken ct = default);
}
