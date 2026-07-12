using System.Text;
using Leno.Infrastructure.Auth;
using Leno.SystemAdmin.Application;
using Leno.SystemAdmin.Application.DTOs;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leno.SystemAdmin.Api.Controllers;

/// <summary>
/// 审计与操作日志查询控制器（只读，支持分页查询与 CSV 导出）。
/// </summary>
[Authorize(Roles = "Operator,Admin")]
[ApiController]
public sealed class AuditLogsController : SystemAdminControllerBase
{
    private readonly IAuditLogAppService _auditLogAppService;
    private readonly IAuditLogEntryAppService _auditLogEntryAppService;

    public AuditLogsController(
        ICurrentUserContext currentUser,
        IAuditLogAppService auditLogAppService,
        IAuditLogEntryAppService auditLogEntryAppService)
        : base(currentUser)
    {
        ArgumentNullException.ThrowIfNull(auditLogAppService);
        ArgumentNullException.ThrowIfNull(auditLogEntryAppService);
        _auditLogAppService = auditLogAppService;
        _auditLogEntryAppService = auditLogEntryAppService;
    }

    /// <summary>分页查询审计日志，支持运营人员、资源类型与时间区间过滤。</summary>
    [HttpGet("api/admin/audit-logs")]
    [ProducesResponseType(typeof(ApiResponse<AuditLogListResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> QueryAuditLogsAsync(
        [FromQuery] Guid? operatorId,
        [FromQuery] string? resourceType,
        [FromQuery] DateTime? fromTime,
        [FromQuery] DateTime? toTime,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _auditLogAppService.QueryAuditLogsAsync(operatorId, resourceType, fromTime, toTime, page, pageSize, ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>导出审计日志为 CSV 文件。</summary>
    [HttpGet("api/admin/audit-logs/export")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportAuditLogsAsync(
        [FromQuery] Guid? operatorId,
        [FromQuery] string? resourceType,
        [FromQuery] DateTime? fromTime,
        [FromQuery] DateTime? toTime,
        CancellationToken ct)
    {
        var csv = await _auditLogAppService.ExportAuditLogsAsync(operatorId, resourceType, fromTime, toTime, ct);
        return File(Encoding.UTF8.GetBytes(csv), "text/csv", "audit-logs.csv");
    }

    /// <summary>分页查询操作日志，支持运营人员、模块与时间区间过滤。</summary>
    [HttpGet("api/admin/operation-logs")]
    [ProducesResponseType(typeof(ApiResponse<OperationLogListResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> QueryOperationLogsAsync(
        [FromQuery] Guid? operatorId,
        [FromQuery(Name = "module")] string? moduleName,
        [FromQuery] DateTime? fromTime,
        [FromQuery] DateTime? toTime,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _auditLogAppService.QueryOperationLogsAsync(operatorId, moduleName, fromTime, toTime, page, pageSize, ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>按标识获取跨域审计日志条目详情。</summary>
    [HttpGet("api/admin/audit-logs/{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<AuditLogEntryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAuditLogByIdAsync(Guid id, CancellationToken ct)
    {
        var entry = await _auditLogEntryAppService.GetByIdAsync(id, ct);
        if (entry is null)
        {
            return NotFound(ApiResponse.Fail(404, "审计日志条目不存在"));
        }

        return Ok(ApiResponse.Success(entry));
    }

    /// <summary>分页查询跨域审计日志条目，支持模块、操作动作、时间区间与操作人过滤。</summary>
    [HttpGet("api/admin/audit-log-entries")]
    [ProducesResponseType(typeof(ApiResponse<AuditLogEntryListResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> QueryAuditLogEntriesAsync(
        [FromQuery(Name = "module")] string? moduleName,
        [FromQuery] string? action,
        [FromQuery] DateTime? fromTime,
        [FromQuery] DateTime? toTime,
        [FromQuery] Guid? operatorId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _auditLogEntryAppService.QueryAsync(moduleName, action, fromTime, toTime, operatorId, page, pageSize, ct);
        return Ok(ApiResponse.Success(result));
    }
}
