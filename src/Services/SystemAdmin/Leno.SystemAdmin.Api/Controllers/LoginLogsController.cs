using System.Text;
using Leno.Infrastructure.Auth;
using Leno.SystemAdmin.Application;
using Leno.SystemAdmin.Application.DTOs;
using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.ValueObjects;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leno.SystemAdmin.Api.Controllers;

/// <summary>
/// 登录日志控制器（3 Endpoints）：分页查询、详情、CSV 导出。
/// Admin 与 Operator 均可读（与 AuditLogsController 鉴权一致）。
/// </summary>
[Authorize(Roles = "Admin,Operator")]
[ApiController]
public sealed class LoginLogsController : SystemAdminControllerBase
{
    private readonly ILoginLogAppService _loginLogAppService;

    public LoginLogsController(
        ICurrentUserContext currentUser,
        ILoginLogAppService loginLogAppService)
        : base(currentUser)
    {
        ArgumentNullException.ThrowIfNull(loginLogAppService);
        _loginLogAppService = loginLogAppService;
    }

    /// <summary>分页查询登录日志。</summary>
    [HttpGet("api/admin/login-logs")]
    [ProducesResponseType(typeof(ApiResponse<LoginLogListResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListAsync(
        [FromQuery] string? username,
        [FromQuery] LoginResult? result,
        [FromQuery] DateTime? loginAtFrom,
        [FromQuery] DateTime? loginAtTo,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = new LoginLogQuery
        {
            Username = username,
            Result = result,
            LoginAtFrom = loginAtFrom,
            LoginAtTo = loginAtTo,
            Page = page,
            PageSize = pageSize
        };
        var resultDto = await _loginLogAppService.QueryAsync(query, ct);
        return Ok(ApiResponse.Success(resultDto));
    }

    /// <summary>按标识获取登录日志详情。</summary>
    [HttpGet("api/admin/login-logs/{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<LoginLogDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var log = await _loginLogAppService.GetByIdAsync(id, ct);
        if (log is null)
        {
            return NotFound(ApiResponse.Fail(404, "登录日志不存在"));
        }
        return Ok(ApiResponse.Success(log));
    }

    /// <summary>导出登录日志为 CSV（单次最多 10 万条）。</summary>
    [HttpGet("api/admin/login-logs/export")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportAsync(
        [FromQuery] string? username,
        [FromQuery] LoginResult? result,
        [FromQuery] DateTime? loginAtFrom,
        [FromQuery] DateTime? loginAtTo,
        CancellationToken ct)
    {
        var query = new LoginLogQuery
        {
            Username = username,
            Result = result,
            LoginAtFrom = loginAtFrom,
            LoginAtTo = loginAtTo
        };
        var csv = await _loginLogAppService.ExportAsync(query, ct);
        return File(Encoding.UTF8.GetBytes(csv), "text/csv", "login-logs.csv");
    }
}
