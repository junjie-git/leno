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
/// 运营数据看板控制器，提供运营数据概览、支付统计、积分统计、
/// 通知送达率、售后统计、店铺排行、报表管理等功能。
/// 所有端点返回 <see cref="DashboardReportDto"/> 而非领域聚合 <see cref="DashboardReport"/>，避免泄露聚合内部结构。
/// </summary>
[Authorize(Roles = "Operator,Admin")]
[ApiController]
[Route("api/admin/dashboard")]
public sealed class DashboardController : SystemAdminControllerBase
{
    private readonly IStatisticsAggregationService _aggregationService;
    private readonly IDashboardReportRepository _reportRepository;

    public DashboardController(
        ICurrentUserContext currentUser,
        IStatisticsAggregationService aggregationService,
        IDashboardReportRepository reportRepository)
        : base(currentUser)
    {
        ArgumentNullException.ThrowIfNull(aggregationService);
        ArgumentNullException.ThrowIfNull(reportRepository);
        _aggregationService = aggregationService;
        _reportRepository = reportRepository;
    }

    /// <summary>
    /// 获取运营数据概览（订单量/GMV/转化率）。
    /// </summary>
    [HttpGet("overview")]
    [ProducesResponseType(typeof(ApiResponse<DashboardReportDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOverviewAsync(
        [FromQuery] DateTime? start,
        [FromQuery] DateTime? end,
        CancellationToken ct)
    {
        var period = GetPeriodOrDefault(start, end);
        var report = await _aggregationService.AggregateAsync(ReportType.OrderGmv, period, ct);
        return Ok(ApiResponse.Success(ToDto(report)));
    }

    /// <summary>
    /// 获取支付成功率统计（按渠道）。
    /// </summary>
    [HttpGet("payment-stats")]
    [ProducesResponseType(typeof(ApiResponse<DashboardReportDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPaymentStatsAsync(
        [FromQuery] DateTime? start,
        [FromQuery] DateTime? end,
        CancellationToken ct)
    {
        var period = GetPeriodOrDefault(start, end);
        var report = await _aggregationService.AggregateAsync(ReportType.PaymentSuccessRate, period, ct);
        return Ok(ApiResponse.Success(ToDto(report)));
    }

    /// <summary>
    /// 获取积分发放量统计。
    /// </summary>
    [HttpGet("points-stats")]
    [ProducesResponseType(typeof(ApiResponse<DashboardReportDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPointsStatsAsync(
        [FromQuery] DateTime? start,
        [FromQuery] DateTime? end,
        CancellationToken ct)
    {
        var period = GetPeriodOrDefault(start, end);
        var report = await _aggregationService.AggregateAsync(ReportType.PointsIssued, period, ct);
        return Ok(ApiResponse.Success(ToDto(report)));
    }

    /// <summary>
    /// 获取通知送达率统计。
    /// </summary>
    [HttpGet("notification-delivery")]
    [ProducesResponseType(typeof(ApiResponse<DashboardReportDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetNotificationDeliveryAsync(
        [FromQuery] DateTime? start,
        [FromQuery] DateTime? end,
        CancellationToken ct)
    {
        var period = GetPeriodOrDefault(start, end);
        var report = await _aggregationService.AggregateAsync(ReportType.NotificationDelivery, period, ct);
        return Ok(ApiResponse.Success(ToDto(report)));
    }

    /// <summary>
    /// 获取售后统计（售后量/退款金额）。
    /// </summary>
    [HttpGet("after-sales-stats")]
    [ProducesResponseType(typeof(ApiResponse<DashboardReportDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAfterSalesStatsAsync(
        [FromQuery] DateTime? start,
        [FromQuery] DateTime? end,
        CancellationToken ct)
    {
        var period = GetPeriodOrDefault(start, end);
        var report = await _aggregationService.AggregateAsync(ReportType.AfterSalesVolume, period, ct);
        return Ok(ApiResponse.Success(ToDto(report)));
    }

    /// <summary>
    /// 获取店铺排行 TopN。
    /// </summary>
    [HttpGet("shop-ranking")]
    [ProducesResponseType(typeof(ApiResponse<DashboardReportDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetShopRankingAsync(
        [FromQuery] DateTime? start,
        [FromQuery] DateTime? end,
        CancellationToken ct)
    {
        var period = GetPeriodOrDefault(start, end);
        var report = await _aggregationService.AggregateAsync(ReportType.ShopRanking, period, ct);
        return Ok(ApiResponse.Success(ToDto(report)));
    }

    /// <summary>
    /// 获取报表列表（按类型和时间范围查询）。
    /// </summary>
    [HttpGet("reports")]
    [ProducesResponseType(typeof(ApiResponse<List<DashboardReportDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReportsAsync(
        [FromQuery] ReportType reportType,
        [FromQuery] DateTime? start,
        [FromQuery] DateTime? end,
        CancellationToken ct)
    {
        var period = GetPeriodOrDefault(start, end);
        var reports = await _reportRepository.GetByPeriodAsync(reportType, period.Start, period.End, ct);
        var dtos = reports.Select(ToDto).ToList();
        return Ok(ApiResponse.Success(dtos));
    }

    /// <summary>
    /// 获取报表详情。
    /// </summary>
    [HttpGet("reports/{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<DashboardReportDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetReportByIdAsync(Guid id, CancellationToken ct)
    {
        var report = await _reportRepository.GetByIdAsync(id, ct);
        if (report is null)
        {
            return NotFound(ApiResponse.Fail(404, "报表不存在"));
        }

        return Ok(ApiResponse.Success(ToDto(report)));
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
    /// 将领域聚合 <see cref="DashboardReport"/> 投影为 <see cref="DashboardReportDto"/>。
    /// 不暴露 Period 值对象与 DataVersion 等内部字段，仅投影对外契约字段。
    /// </summary>
    private static DashboardReportDto ToDto(DashboardReport report)
    {
        return new DashboardReportDto
        {
            ReportId = report.ReportId,
            ReportType = report.ReportType,
            Granularity = report.Granularity,
            GeneratedAt = report.GeneratedAt,
            PeriodStart = report.Period.Start,
            PeriodEnd = report.Period.End,
            Metrics = report.Metrics
                .Select(m => new MetricItemDto { Key = m.Key, Value = m.Value, Unit = m.Unit })
                .ToList()
        };
    }
}
