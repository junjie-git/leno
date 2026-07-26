using Leno.AccessControl.Application.DTOs;

namespace Leno.AccessControl.Application;

/// <summary>
/// 角色管理应用服务接口（角色 CRUD）。
/// 供 AccessControl 域 <c>AdminRolesController</c> 使用，对应旧域 UserAuth AdminRolesController 的 5 个角色管理端点。
/// 与 <see cref="Services.IPermissionAppService"/> 区别：本接口面向 HTTP Controller，去除 operatorId 审计参数
/// （审计由 SystemAdmin BC 消费领域事件完成），并将 <see cref="GetRoleAsync"/> 返回值改为可空，
/// 由 Controller 层决定 404 响应语义，避免抛领域异常被全局异常中间件统一映射为 400。
/// </summary>
public interface IRoleAppService
{
    /// <summary>分页查询角色列表（按名称或描述模糊匹配）。</summary>
    Task<PagedResult<RoleDto>> QueryRolesAsync(string? keyword, int page, int pageSize, CancellationToken ct = default);

    /// <summary>查询角色详情，不存在返回 null。</summary>
    Task<RoleDto?> GetRoleAsync(Guid roleId, CancellationToken ct = default);

    /// <summary>创建角色，返回创建后的角色 DTO。</summary>
    Task<RoleDto> CreateRoleAsync(CreateRoleDto request, CancellationToken ct = default);

    /// <summary>更新角色名称与描述。</summary>
    Task UpdateRoleAsync(Guid roleId, UpdateRoleDto request, CancellationToken ct = default);

    /// <summary>删除角色（内置角色不可删除，存在用户引用不可删除）。</summary>
    Task DeleteRoleAsync(Guid roleId, CancellationToken ct = default);
}
