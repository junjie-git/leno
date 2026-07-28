using Leno.Infrastructure.Abstractions.Sessions;
using Leno.Infrastructure.Auth;
using Leno.SystemAdmin.Application;
using Leno.SystemAdmin.Application.DTOs;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leno.SystemAdmin.Api.Controllers;

/// <summary>
/// 在线用户管理控制器（4 Endpoints）：分页查询、详情、强制下线、统计。
/// 强制下线校验 sessionId != 当前操作者 sessionId（防自降）。
/// Redis 不可用时查询返回空列表、强制下线返回 503。
/// </summary>
[Authorize(Roles = "Admin")]
[ApiController]
public sealed class OnlineUsersController : SystemAdminControllerBase
{
    private readonly IOnlineUserAppService _onlineUserAppService;

    public OnlineUsersController(
        ICurrentUserContext currentUser,
        IOnlineUserAppService onlineUserAppService)
        : base(currentUser)
    {
        ArgumentNullException.ThrowIfNull(onlineUserAppService);
        _onlineUserAppService = onlineUserAppService;
    }

    /// <summary>分页查询在线用户。</summary>
    [HttpGet("api/admin/online-users")]
    [ProducesResponseType(typeof(ApiResponse<OnlineUserListResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListAsync(
        [FromQuery] string? username,
        [FromQuery] string? ipAddress,
        [FromQuery] DateTime? loginAtFrom,
        [FromQuery] DateTime? loginAtTo,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = new OnlineUserQuery
        {
            Username = username,
            IpAddress = ipAddress,
            LoginAtFrom = loginAtFrom,
            LoginAtTo = loginAtTo,
            Page = page,
            PageSize = pageSize
        };
        var result = await _onlineUserAppService.QueryAsync(query, ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>按 sessionId 获取在线用户详情。</summary>
    [HttpGet("api/admin/online-users/{sessionId}")]
    [ProducesResponseType(typeof(ApiResponse<OnlineUserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByIdAsync(string sessionId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return BadRequest(ApiResponse.Fail(400, "sessionId 不可为空"));
        }

        var user = await _onlineUserAppService.GetByIdAsync(sessionId, ct);
        if (user is null)
        {
            return NotFound(ApiResponse.Fail(404, "在线用户会话不存在"));
        }

        return Ok(ApiResponse.Success(user));
    }

    /// <summary>强制下线指定会话。sessionId == 当前操作者 sessionId 时返回 403。</summary>
    [HttpDelete("api/admin/online-users/{sessionId}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ForceOfflineAsync(string sessionId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return BadRequest(ApiResponse.Fail(400, "sessionId 不可为空"));
        }

        var currentSessionId = CurrentUser.SessionId ?? string.Empty;
        await _onlineUserAppService.ForceOfflineAsync(sessionId, currentSessionId, ct);
        return Ok(ApiResponse.Success(new { forcedOffline = true, sessionId }));
    }

    /// <summary>获取在线用户统计指标。</summary>
    [HttpGet("api/admin/online-users/stats")]
    [ProducesResponseType(typeof(ApiResponse<OnlineUserStatsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStatsAsync(CancellationToken ct)
    {
        var stats = await _onlineUserAppService.GetStatsAsync(ct);
        return Ok(ApiResponse.Success(stats));
    }
}
