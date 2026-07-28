using Leno.Infrastructure.Auth;
using Leno.SystemAdmin.Application;
using Leno.SystemAdmin.Application.DTOs;
using Leno.SystemAdmin.Domain.ValueObjects;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leno.SystemAdmin.Api.Controllers;

/// <summary>
/// 服务器监控控制器（2 Endpoints）：实时快照、历史指标折线。
/// 不依赖 Redis，永远可用；数据来自 .NET 进程内 API 与内存滚动窗口。
/// </summary>
[Authorize(Roles = "Admin")]
[ApiController]
public sealed class ServerMonitorController : SystemAdminControllerBase
{
    private readonly IServerMonitorAppService _serverMonitorAppService;

    public ServerMonitorController(
        ICurrentUserContext currentUser,
        IServerMonitorAppService serverMonitorAppService)
        : base(currentUser)
    {
        ArgumentNullException.ThrowIfNull(serverMonitorAppService);
        _serverMonitorAppService = serverMonitorAppService;
    }

    /// <summary>获取服务器快照（6 统计卡片 + 系统信息）。</summary>
    [HttpGet("api/admin/server-monitor/snapshot")]
    [ProducesResponseType(typeof(ApiResponse<ServerSnapshotDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetSnapshotAsync(CancellationToken ct)
    {
        var snapshot = await _serverMonitorAppService.GetSnapshotAsync(ct);
        return Ok(ApiResponse.Success(snapshot));
    }

    /// <summary>获取历史指标折线数据。metric: cpu/memory/disk-io；rangeSeconds: 1-3600。</summary>
    [HttpGet("api/admin/server-monitor/history")]
    [ProducesResponseType(typeof(ApiResponse<MetricHistoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetHistoryAsync(
        [FromQuery] string metric,
        [FromQuery] int rangeSeconds = 300,
        CancellationToken ct = default)
    {
        var history = await _serverMonitorAppService.GetHistoryAsync(metric, rangeSeconds, ct);
        return Ok(ApiResponse.Success(history));
    }
}
