using Leno.AccessControl.Application.DTOs;

namespace Leno.AccessControl.Application.Services;

/// <summary>
/// 角色权限管理应用服务接口。
/// 写操作需传入 <paramref name="operatorId" /> 用于领域事件追溯（审计由 SystemAdmin BC 消费领域事件完成）。
/// 从 UserAuth BC 迁入 AccessControl BC（3.6 AuthN/AuthZ 拆分）。
/// </summary>
public interface IPermissionAppService
{
    /// <summary>分页查询角色列表。</summary>
    Task<PagedResult<RoleDto>> QueryRolesAsync(string? keyword, int page, int pageSize, CancellationToken ct = default);

    /// <summary>查询角色详情。</summary>
    Task<RoleDto> GetRoleAsync(Guid roleId, CancellationToken ct = default);

    /// <summary>创建角色。</summary>
    Task<RoleDto> CreateRoleAsync(SaveRoleDto dto, Guid operatorId, CancellationToken ct = default);

    /// <summary>编辑角色。</summary>
    Task<RoleDto> UpdateRoleAsync(Guid roleId, SaveRoleDto dto, Guid operatorId, CancellationToken ct = default);

    /// <summary>删除角色（内置角色不可删除）。</summary>
    Task DeleteRoleAsync(Guid roleId, Guid operatorId, CancellationToken ct = default);

    /// <summary>查看角色权限。</summary>
    Task<List<string>> GetRolePermissionsAsync(Guid roleId, CancellationToken ct = default);

    /// <summary>更新角色权限（全量替换）。</summary>
    Task UpdateRolePermissionsAsync(Guid roleId, UpdatePermissionsDto dto, Guid operatorId, CancellationToken ct = default);
}
