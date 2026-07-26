using Leno.Infrastructure.Auth;
using Leno.SystemAdmin.Application;
using Leno.SystemAdmin.Application.DTOs;
using Leno.SystemAdmin.Domain.ValueObjects;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leno.SystemAdmin.Api.Controllers;

/// <summary>
/// 告警管理控制器，对接 Alertmanager，提供告警查询、详情与确认能力。
/// 静默规则相关接口由 <see cref="AlertSilencesController"/> 承载。
/// </summary>
[ApiController]
[Route("api/admin/alerts")]
[Authorize(Roles = "Admin")]
public sealed class AlertsController : SystemAdminControllerBase
{
    private readonly IAlertAppService _appService;

    public AlertsController(ICurrentUserContext currentUser, IAlertAppService appService)
        : base(currentUser)
    {
        ArgumentNullException.ThrowIfNull(appService);
        _appService = appService;
    }

    /// <summary>
    /// 分页查询告警事件，支持 module/severity/status/时间范围筛选。
    /// 设计文档 04-runtime-ops/alert-management.md §3 主要 API 第 1 行。
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<AlertListResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> QueryAsync(
        [FromQuery(Name = "module")] string? moduleName,
        [FromQuery] AlertSeverity? severity,
        [FromQuery] AlertStatus? status,
        [FromQuery] DateTime? start,
        [FromQuery] DateTime? end,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _appService.QueryAsync(moduleName, severity, status, start, end, page, pageSize, ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>
    /// 按 ID 获取告警详情，包含标签、注释、关联指标等全字段。
    /// 设计文档 04-runtime-ops/alert-management.md §3 主要 API 第 2 行。
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<AlertDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var result = await _appService.GetByIdAsync(id, ct);
        if (result is null)
        {
            return NotFound(ApiResponse.Fail(404, "告警不存在"));
        }

        return Ok(ApiResponse.Success(result));
    }

    /// <summary>
    /// 确认告警，状态由 Firing 流转为 Acknowledged。
    /// 设计文档 04-runtime-ops/alert-management.md §3 主要 API 第 3 行。
    /// </summary>
    [HttpPost("{id:guid}/acknowledge")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AcknowledgeAsync(Guid id, [FromBody] AcknowledgeAlertDto? dto, CancellationToken ct)
    {
        var operatorId = GetCurrentOperatorId().ToString();
        var comment = dto?.Comment;
        try
        {
            await _appService.AcknowledgeAsync(id, operatorId, comment, ct);
            return Ok(ApiResponse.Success());
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ApiResponse.Fail(404, ex.Message));
        }
    }
}
