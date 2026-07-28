using Leno.Infrastructure.Auth;
using Leno.SystemAdmin.Application;
using Leno.SystemAdmin.Application.DTOs;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leno.SystemAdmin.Api.Controllers;

/// <summary>
/// 菜单管理控制器（5 Endpoints）：菜单树查询、创建、更新、删除、同级排序。
/// 所有操作要求 Admin 角色；写操作由 [AuditLog] Action Filter 自动记录审计日志。
/// </summary>
[Authorize(Roles = "Admin")]
[ApiController]
public sealed class MenusController : SystemAdminControllerBase
{
    private readonly IMenuAppService _menuAppService;

    public MenusController(
        ICurrentUserContext currentUser,
        IMenuAppService menuAppService)
        : base(currentUser)
    {
        ArgumentNullException.ThrowIfNull(menuAppService);
        _menuAppService = menuAppService;
    }

    /// <summary>获取完整菜单树（按 ParentId 组装层级）。</summary>
    [HttpGet("api/admin/menus/tree")]
    [ProducesResponseType(typeof(ApiResponse<List<MenuDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetTreeAsync(CancellationToken ct)
    {
        var tree = await _menuAppService.GetTreeAsync(ct);
        return Ok(ApiResponse.Success(tree));
    }

    /// <summary>创建菜单节点。</summary>
    [HttpPost("api/admin/menus")]
    [ProducesResponseType(typeof(ApiResponse<MenuDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateAsync([FromBody] CreateMenuDto body, CancellationToken ct)
    {
        var operatorId = GetCurrentOperatorId();
        var menu = await _menuAppService.CreateAsync(body, operatorId, ct);
        return CreatedAtAction(nameof(GetTreeAsync), new { }, ApiResponse.Success(menu));
    }

    /// <summary>更新菜单节点（部分更新）。</summary>
    [HttpPut("api/admin/menus/{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<MenuDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateAsync(Guid id, [FromBody] UpdateMenuDto body, CancellationToken ct)
    {
        var operatorId = GetCurrentOperatorId();
        var menu = await _menuAppService.UpdateAsync(id, body, operatorId, ct);
        return Ok(ApiResponse.Success(menu));
    }

    /// <summary>删除菜单节点（递归删除子树由仓储处理，带子菜单抛业务异常）。</summary>
    [HttpDelete("api/admin/menus/{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken ct)
    {
        var operatorId = GetCurrentOperatorId();
        await _menuAppService.DeleteAsync(id, operatorId, ct);
        return Ok(ApiResponse.Success(new { deleted = true, id }));
    }

    /// <summary>批量更新同级菜单排序。</summary>
    [HttpPut("api/admin/menus/sort")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SortAsync([FromBody] List<MenuSortItemDto> items, CancellationToken ct)
    {
        var operatorId = GetCurrentOperatorId();
        await _menuAppService.SortAsync(items, operatorId, ct);
        return Ok(ApiResponse.Success(new { sorted = items.Count }));
    }
}
