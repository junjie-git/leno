namespace Leno.AccessControl.Application;

/// <summary>
/// 角色权限管理应用服务接口。
/// 供 AccessControl 域 <c>AdminRolesController</c> 角色权限子资源端点使用
/// （<c>GET/PUT api/admin/roles/{roleId}/permissions</c>）。
/// 与 <see cref="Services.IPermissionAppService"/> 区别：本接口面向 HTTP Controller，去除 operatorId 审计参数，
/// 权限列表类型统一为 <see cref="IReadOnlyList{T}"/>，与 <see cref="DTOs.RoleDto.Permissions"/> 保持一致。
/// </summary>
public interface IRolePermissionAppService
{
    /// <summary>查询角色权限资源键列表，角色不存在时返回空列表（由调用方先验证角色存在性）。</summary>
    Task<IReadOnlyList<string>> GetRolePermissionsAsync(Guid roleId, CancellationToken ct = default);

    /// <summary>更新角色权限（全量替换）。</summary>
    Task UpdateRolePermissionsAsync(Guid roleId, IReadOnlyList<string> permissions, CancellationToken ct = default);
}
