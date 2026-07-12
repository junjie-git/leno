using Leno.Infrastructure.Auth;
using Leno.SharedContracts.Responses;
using Leno.UserAuth.Application.DTOs;
using Leno.UserAuth.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leno.UserAuth.Api.Controllers;

/// <summary>
/// 角色权限管理控制器，提供角色 CRUD 与权限管理端点。
/// 仅 Admin 角色可访问。
/// </summary>
[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/admin/roles")]
public sealed class AdminRolesController : UserAuthControllerBase
{
    private readonly IPermissionAppService _permissionAppService;

    public AdminRolesController(ICurrentUserContext currentUser, IPermissionAppService permissionAppService)
        : base(currentUser)
    {
        ArgumentNullException.ThrowIfNull(permissionAppService);
        _permissionAppService = permissionAppService;
    }

    /// <summary>分页查询角色列表。</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<RoleDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> QueryRolesAsync(
        [FromQuery] string? keyword,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _permissionAppService.QueryRolesAsync(keyword, page, pageSize, ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>查询角色详情。</summary>
    [HttpGet("{roleId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<RoleDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRoleAsync(Guid roleId, CancellationToken ct)
    {
        var role = await _permissionAppService.GetRoleAsync(roleId, ct);
        return Ok(ApiResponse.Success(role));
    }

    /// <summary>创建角色。</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<RoleDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateRoleAsync([FromBody] SaveRoleDto dto, CancellationToken ct)
    {
        var role = await _permissionAppService.CreateRoleAsync(dto, ct);
        return CreatedAtAction(nameof(GetRoleAsync), new { roleId = role.Id }, ApiResponse.Success(role));
    }

    /// <summary>编辑角色。</summary>
    [HttpPut("{roleId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<RoleDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateRoleAsync(Guid roleId, [FromBody] SaveRoleDto dto, CancellationToken ct)
    {
        var role = await _permissionAppService.UpdateRoleAsync(roleId, dto, ct);
        return Ok(ApiResponse.Success(role));
    }

    /// <summary>删除角色（内置角色不可删除）。</summary>
    [HttpDelete("{roleId:guid}")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteRoleAsync(Guid roleId, CancellationToken ct)
    {
        await _permissionAppService.DeleteRoleAsync(roleId, ct);
        return Ok(ApiResponse.Success("角色已删除"));
    }

    /// <summary>查看角色权限列表。</summary>
    [HttpGet("{roleId:guid}/permissions")]
    [ProducesResponseType(typeof(ApiResponse<List<string>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRolePermissionsAsync(Guid roleId, CancellationToken ct)
    {
        var permissions = await _permissionAppService.GetRolePermissionsAsync(roleId, ct);
        return Ok(ApiResponse.Success(permissions));
    }

    /// <summary>更新角色权限（全量替换）。</summary>
    [HttpPut("{roleId:guid}/permissions")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateRolePermissionsAsync(Guid roleId, [FromBody] UpdatePermissionsDto dto, CancellationToken ct)
    {
        await _permissionAppService.UpdateRolePermissionsAsync(roleId, dto, ct);
        return Ok(ApiResponse.Success("权限已更新"));
    }
}