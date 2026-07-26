using Leno.AccessControl.Application;
using Leno.AccessControl.Application.DTOs;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leno.AccessControl.Api.Controllers;

/// <summary>
/// 角色权限管理控制器，提供角色 CRUD 与权限管理端点。
/// 路由 <c>api/admin/roles/*</c>，仅 <c>Operator,Admin</c> 角色可访问。
/// 从 UserAuth BC <c>AdminRolesController</c> 迁移，沿用 7 个端点契约，
/// 调整为：POST 创建返回 200（不再用 201 CreatedAtAction），响应统一 <see cref="ApiResponse{T}"/> 包装。
/// </summary>
[ApiController]
[Route("api/admin/roles")]
[Authorize(Roles = "Operator,Admin")]
public sealed class AdminRolesController : ControllerBase
{
    private readonly IRoleAppService _roleAppService;
    private readonly IRolePermissionAppService _rolePermissionAppService;

    public AdminRolesController(
        IRoleAppService roleAppService,
        IRolePermissionAppService rolePermissionAppService)
    {
        ArgumentNullException.ThrowIfNull(roleAppService);
        ArgumentNullException.ThrowIfNull(rolePermissionAppService);
        _roleAppService = roleAppService;
        _rolePermissionAppService = rolePermissionAppService;
    }

    /// <summary>分页查询角色列表。</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<RoleDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAsync(
        [FromQuery] string? keyword,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _roleAppService.QueryRolesAsync(keyword, page, pageSize, ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>查询角色详情。</summary>
    [HttpGet("{roleId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<RoleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAsync([FromRoute] Guid roleId, CancellationToken ct)
    {
        var result = await _roleAppService.GetRoleAsync(roleId, ct);
        return result is null
            ? NotFound(ApiResponse.Fail(StatusCodes.Status404NotFound, "角色不存在"))
            : Ok(ApiResponse.Success(result));
    }

    /// <summary>创建角色。</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<RoleDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateAsync([FromBody] CreateRoleDto request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var result = await _roleAppService.CreateRoleAsync(request, ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>编辑角色名称与描述。</summary>
    [HttpPut("{roleId:guid}")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateAsync([FromRoute] Guid roleId, [FromBody] UpdateRoleDto request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        await _roleAppService.UpdateRoleAsync(roleId, request, ct);
        return Ok(ApiResponse.Success());
    }

    /// <summary>删除角色（内置角色不可删除）。</summary>
    [HttpDelete("{roleId:guid}")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteAsync([FromRoute] Guid roleId, CancellationToken ct)
    {
        await _roleAppService.DeleteRoleAsync(roleId, ct);
        return Ok(ApiResponse.Success());
    }

    /// <summary>查看角色权限列表。</summary>
    [HttpGet("{roleId:guid}/permissions")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<string>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPermissionsAsync([FromRoute] Guid roleId, CancellationToken ct)
    {
        var result = await _rolePermissionAppService.GetRolePermissionsAsync(roleId, ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>更新角色权限（全量替换）。</summary>
    [HttpPut("{roleId:guid}/permissions")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdatePermissionsAsync([FromRoute] Guid roleId, [FromBody] UpdatePermissionsDto request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        await _rolePermissionAppService.UpdateRolePermissionsAsync(roleId, request.Permissions, ct);
        return Ok(ApiResponse.Success());
    }
}
