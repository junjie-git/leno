using Leno.Infrastructure.Auth;
using Leno.SystemAdmin.Application;
using Leno.SystemAdmin.Application.DTOs;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leno.SystemAdmin.Api.Controllers;

/// <summary>
/// Outbox 监控控制器，提供各域 Outbox 发件箱积压查询、重投与归档能力。
/// 跨域查询各域 outbox_messages 表，归档历史持久化至 SystemAdmin 库。
/// </summary>
[ApiController]
[Route("api/admin/outbox")]
[Authorize(Roles = "Admin")]
public sealed class OutboxMonitorController : SystemAdminControllerBase
{
    private readonly IOutboxMonitorAppService _appService;

    public OutboxMonitorController(ICurrentUserContext currentUser, IOutboxMonitorAppService appService)
        : base(currentUser)
    {
        ArgumentNullException.ThrowIfNull(appService);
        _appService = appService;
    }

    /// <summary>
    /// 获取各域 Outbox 积压汇总。
    /// 设计文档 05-audit/outbox-monitor.md §3 主要 API 第 1 行。
    /// </summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(ApiResponse<List<OutboxContextSummaryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSummaryAsync(CancellationToken ct)
    {
        var result = await _appService.GetSummaryAsync(ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>
    /// 获取近 N 小时积压趋势。
    /// 设计文档 05-audit/outbox-monitor.md §3 主要 API 第 2 行。
    /// </summary>
    [HttpGet("trend")]
    [ProducesResponseType(typeof(ApiResponse<List<OutboxTrendPointDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTrendAsync([FromQuery] int hours = 24, CancellationToken ct = default)
    {
        var result = await _appService.GetTrendAsync(hours, ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>
    /// 分页查询指定域积压事件详情。
    /// 设计文档 05-audit/outbox-monitor.md §3 主要 API 第 3 行。
    /// </summary>
    [HttpGet("{context}/messages")]
    [ProducesResponseType(typeof(ApiResponse<OutboxMessageListResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMessagesAsync(
        string context,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _appService.GetMessagesAsync(context, status, page, pageSize, ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>
    /// 批量重投指定域积压事件。messageIds 为空则重投全部。
    /// 设计文档 05-audit/outbox-monitor.md §3 主要 API 第 4 行。
    /// </summary>
    [HttpPost("{context}/republish")]
    [ProducesResponseType(typeof(ApiResponse<OutboxRepublishResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RepublishAsync(
        string context,
        [FromBody] BatchRepublishOutboxDto? dto,
        CancellationToken ct = default)
    {
        var operatorId = GetCurrentOperatorId().ToString();
        try
        {
            var result = await _appService.RepublishAsync(context, dto?.MessageIds, operatorId, ct);
            return Ok(ApiResponse.Success(result));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse.Fail(400, ex.Message));
        }
    }

    /// <summary>
    /// 归档指定域陈旧积压事件（CreatedAt 早于 before 的事件）。
    /// 设计文档 05-audit/outbox-monitor.md §3 主要 API 第 5 行。
    /// </summary>
    [HttpPost("{context}/archive")]
    [ProducesResponseType(typeof(ApiResponse<OutboxArchiveResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ArchiveAsync(
        string context,
        [FromBody] ArchiveOutboxDto dto,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var operatorId = GetCurrentOperatorId().ToString();
        try
        {
            var result = await _appService.ArchiveAsync(context, dto.Before, operatorId, dto.Reason, ct);
            return Ok(ApiResponse.Success(result));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse.Fail(400, ex.Message));
        }
    }

    /// <summary>
    /// 分页查询指定域归档历史。
    /// 设计文档 05-audit/outbox-monitor.md §3 主要 API 第 6 行。
    /// </summary>
    [HttpGet("{context}/archive-history")]
    [ProducesResponseType(typeof(ApiResponse<OutboxArchiveHistoryListResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetArchiveHistoryAsync(
        string context,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _appService.GetArchiveHistoryAsync(context, page, pageSize, ct);
        return Ok(ApiResponse.Success(result));
    }
}
