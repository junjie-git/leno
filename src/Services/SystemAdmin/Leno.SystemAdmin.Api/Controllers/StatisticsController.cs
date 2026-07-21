using Leno.Infrastructure.Auth;
using Leno.SystemAdmin.Application.DTOs;
using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Repositories;
using Leno.SystemAdmin.Domain.Services;
using Leno.SystemAdmin.Domain.ValueObjects;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leno.SystemAdmin.Api.Controllers;

/// <summary>
/// 统计数据对账控制器，提供对账状态查询与手动触发对账功能。
/// SystemAdmin 域仅以只读方式消费各域集成事件，不写回任何域的写库。
/// 所有返回对账记录的端点返回 <see cref="ReconciliationRecordDto"/> 而非领域聚合 <see cref="ReconciliationRecord"/>，
/// 避免泄露 <see cref="StatisticsSnapshot"/> 内部结构（含原始指标与差异项明细）。
/// </summary>
[Authorize(Roles = "Operator,Admin")]
[ApiController]
[Route("api/admin/statistics")]
public sealed class StatisticsController : SystemAdminControllerBase
{
    private readonly IStatisticsReconciliationService _reconciliationService;
    private readonly IReconciliationRecordRepository _reconciliationRecordRepository;

    public StatisticsController(
        ICurrentUserContext currentUser,
        IStatisticsReconciliationService reconciliationService,
        IReconciliationRecordRepository reconciliationRecordRepository)
        : base(currentUser)
    {
        ArgumentNullException.ThrowIfNull(reconciliationService);
        ArgumentNullException.ThrowIfNull(reconciliationRecordRepository);
        _reconciliationService = reconciliationService;
        _reconciliationRecordRepository = reconciliationRecordRepository;
    }

    /// <summary>
    /// 获取最近一次对账状态。
    /// </summary>
    [HttpGet("reconciliation-status")]
    [ProducesResponseType(typeof(ApiResponse<ReconciliationStatusDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReconciliationStatusAsync(CancellationToken ct)
    {
        var latest = await _reconciliationRecordRepository.GetLatestAsync(ct);
        if (latest is null)
        {
            return Ok(ApiResponse.Success(new ReconciliationStatusDto
            {
                HasRun = false,
                Status = null,
                ReportType = null,
                ReconciledAt = null,
                DiscrepancyCount = 0,
                IsConsistent = true
            }));
        }

        return Ok(ApiResponse.Success(new ReconciliationStatusDto
        {
            HasRun = true,
            Status = latest.Status.ToString(),
            ReportType = latest.ReportType.ToString(),
            ReconciledAt = latest.ReconciledAt,
            DiscrepancyCount = latest.Snapshot.Discrepancies.Count,
            IsConsistent = latest.Status == ReconciliationStatus.Consistent,
            AlertTriggered = latest.AlertTriggered,
            CorrectionTriggered = latest.CorrectionTriggered
        }));
    }

    /// <summary>
    /// 手动触发对账（按报表类型和时间范围）。
    /// </summary>
    [HttpPost("reconcile")]
    [ProducesResponseType(typeof(ApiResponse<ReconciliationRecordDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<List<ReconciliationRecordDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> TriggerReconciliationAsync(
        [FromQuery] ReportType? reportType,
        [FromQuery] DateTime? start,
        [FromQuery] DateTime? end,
        CancellationToken ct)
    {
        var period = GetPeriodOrDefault(start, end);

        if (reportType.HasValue)
        {
            var record = await _reconciliationService.ReconcileAsync(reportType.Value, period, ct);
            return Ok(ApiResponse.Success(ToDto(record)));
        }

        var records = await _reconciliationService.ReconcileAllAsync(period, ct);
        var dtos = records.Select(ToDto).ToList();
        return Ok(ApiResponse.Success(dtos));
    }

    /// <summary>
    /// 获取对账记录列表（按报表类型和时间范围查询）。
    /// </summary>
    [HttpGet("reconciliation-records")]
    [ProducesResponseType(typeof(ApiResponse<List<ReconciliationRecordDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReconciliationRecordsAsync(
        [FromQuery] ReportType? reportType,
        [FromQuery] DateTime? start,
        [FromQuery] DateTime? end,
        CancellationToken ct)
    {
        var period = GetPeriodOrDefault(start, end);
        var type = reportType ?? ReportType.OrderGmv;
        var records = await _reconciliationRecordRepository.GetByPeriodAsync(type, period.Start, period.End, ct);
        var dtos = records.Select(ToDto).ToList();
        return Ok(ApiResponse.Success(dtos));
    }

    private static ReportPeriod GetPeriodOrDefault(DateTime? start, DateTime? end)
    {
        var now = DateTime.UtcNow;
        var periodStart = start ?? now.AddDays(-7);
        var periodEnd = end ?? now;

        if (periodStart >= periodEnd)
        {
            periodStart = periodEnd.AddDays(-7);
        }

        return new ReportPeriod(periodStart, periodEnd);
    }

    /// <summary>
    /// 将领域聚合 <see cref="ReconciliationRecord"/> 投影为 <see cref="ReconciliationRecordDto"/>。
    /// 不暴露 Snapshot 内部结构（含原始指标与差异项明细），仅投影对外契约字段。
    /// </summary>
    private static ReconciliationRecordDto ToDto(ReconciliationRecord record)
    {
        return new ReconciliationRecordDto
        {
            RecordId = record.RecordId,
            ReportType = record.ReportType,
            ReconciledAt = record.ReconciledAt,
            Status = record.Status,
            DiscrepancyCount = record.Snapshot.Discrepancies.Count,
            AlertTriggered = record.AlertTriggered,
            CorrectionTriggered = record.CorrectionTriggered,
            ErrorMessage = record.Snapshot.ErrorMessage
        };
    }
}

/// <summary>
/// 对账状态 DTO。
/// </summary>
public sealed class ReconciliationStatusDto
{
    /// <summary>是否已执行过对账。</summary>
    public bool HasRun { get; set; }

    /// <summary>对账状态字符串。</summary>
    public string? Status { get; set; }

    /// <summary>报表类型。</summary>
    public string? ReportType { get; set; }

    /// <summary>对账时间。</summary>
    public DateTime? ReconciledAt { get; set; }

    /// <summary>差异项数量。</summary>
    public int DiscrepancyCount { get; set; }

    /// <summary>是否一致。</summary>
    public bool IsConsistent { get; set; }

    /// <summary>是否触发告警。</summary>
    public bool AlertTriggered { get; set; }

    /// <summary>是否触发自动修正。</summary>
    public bool CorrectionTriggered { get; set; }
}