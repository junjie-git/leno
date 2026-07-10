using Leno.Infrastructure.Auth;
using Leno.SharedContracts.Responses;
using Leno.UserAuth.Application;
using Leno.UserAuth.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leno.UserAuth.Api.Controllers;

/// <summary>
/// 用户管理后台控制器，提供用户分页查询、角色分配与账户状态管理端点。
/// 仅 Operator/Admin 角色可访问；写操作经审计拦截器在事务内写入审计日志。
/// </summary>
[Authorize(Roles = "Admin,Operator")]
[ApiController]
[Route("api/admin/users")]
public sealed class AdminUsersController : UserAuthControllerBase
{
    private readonly IUserAdminAppService _userAdminAppService;

    public AdminUsersController(ICurrentUserContext currentUser, IUserAdminAppService userAdminAppService)
        : base(currentUser)
    {
        ArgumentNullException.ThrowIfNull(userAdminAppService);
        _userAdminAppService = userAdminAppService;
    }

    /// <summary>分页查询用户列表。</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<AdminUserDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> QueryUsersAsync([FromQuery] AdminUserQueryDto query, CancellationToken ct)
    {
        var result = await _userAdminAppService.QueryUsersAsync(query, ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>查询用户详情。</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<AdminUserDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUserAsync(Guid id, CancellationToken ct)
    {
        var user = await _userAdminAppService.GetUserAsync(id, ct);
        return Ok(ApiResponse.Success(user));
    }

    /// <summary>为用户分配角色（幂等，不会移除已有角色）。</summary>
    [HttpPost("{id:guid}/roles")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> AssignRolesAsync(Guid id, [FromBody] AssignRolesDto dto, CancellationToken ct)
    {
        await _userAdminAppService.AssignRolesAsync(id, dto, GetCurrentUserId(), ct);
        return Ok(ApiResponse.Success("角色已分配"));
    }

    /// <summary>锁定用户账户。</summary>
    [HttpPost("{id:guid}/suspend")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> SuspendAsync(Guid id, [FromBody] SuspendUserDto dto, CancellationToken ct)
    {
        await _userAdminAppService.SuspendAsync(id, dto, GetCurrentUserId(), ct);
        return Ok(ApiResponse.Success("账户已锁定"));
    }

    /// <summary>解锁或恢复用户账户为 Active。</summary>
    [HttpPost("{id:guid}/resume")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ResumeAsync(Guid id, CancellationToken ct)
    {
        await _userAdminAppService.ResumeAsync(id, GetCurrentUserId(), ct);
        return Ok(ApiResponse.Success("账户已恢复"));
    }
}
