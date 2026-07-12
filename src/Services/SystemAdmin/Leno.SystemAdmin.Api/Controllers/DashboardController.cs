using Leno.Infrastructure.Auth;
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
    [ProducesResponseType(typeof(ApiResponse<DashboardReport>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOverviewAsync(
        [FromQuery] DateTime? start,
        [FromQuery] DateTime? end,
        CancellationToken ct)
    {
        var period = GetPeriodOrDefault(start, end);
        var report = await _aggregationService.AggregateAsync(ReportType.OrderGmv, period, ct);
        return Ok(ApiResponse.Success(report));
    }

    /// <summary>
    /// 获取支付成功率统计（按渠道）。
    /// </summary>
    [HttpGet("payment-stats")]
    [ProducesResponseType(typeof(ApiResponse<DashboardReport>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPaymentStatsAsync(
        [FromQuery] DateTime? start,
        [FromQuery] DateTime? end,
        CancellationToken ct)
    {
        var period = GetPeriodOrDefault(start, end);
        var report = await _aggregationService.AggregateAsync(ReportType.PaymentSuccessRate, period, ct);
        return Ok(ApiResponse.Success(report));
    }

    /// <summary>
    /// 获取积分发放量统计。
    /// </summary>
    [HttpGet("points-stats")]
    [ProducesResponseType(typeof(ApiResponse<DashboardReport>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPointsStatsAsync(
        [FromQuery] DateTime? start,
        [FromQuery] DateTime? end,
        CancellationToken ct)
    {
        var period = GetPeriodOrDefault(start, end);
        var report = await _aggregationService.AggregateAsync(ReportType.PointsIssued, period, ct);
        return Ok(ApiResponse.Success(report));
    }

    /// <summary>
    /// 获取通知送达率统计。
    /// </summary>
    [HttpGet("notification-delivery")]
    [ProducesResponseType(typeof(ApiResponse<DashboardReport>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetNotificationDeliveryAsync(
        [FromQuery] DateTime? start,
        [FromQuery] DateTime? end,
        CancellationToken ct)
    {
        var period = GetPeriodOrDefault(start, end);
        var report = await _aggregationService.AggregateAsync(ReportType.NotificationDelivery, period, ct);
        return Ok(ApiResponse.Success(report));
    }

    /// <summary>
    /// 获取售后统计（售后量/退款金额）。
    /// </summary>
    [HttpGet("after-sales-stats")]
    [ProducesResponseType(typeof(ApiResponse<DashboardReport>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAfterSalesStatsAsync(
        [FromQuery] DateTime? start,
        [FromQuery] DateTime? end,
        CancellationToken ct)
    {
        var period = GetPeriodOrDefault(start, end);
        var report = await _aggregationService.AggregateAsync(ReportType.AfterSalesVolume, period, ct);
        return Ok(ApiResponse.Success(report));
    }

    /// <summary>
    /// 获取店铺排行 TopN。
    /// </summary>
    [HttpGet("shop-ranking")]
    [ProducesResponseType(typeof(ApiResponse<DashboardReport>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetShopRankingAsync(
        [FromQuery] DateTime? start,
        [FromQuery] DateTime? end,
        CancellationToken ct)
    {
        var period = GetPeriodOrDefault(start, end);
        var report = await _aggregationService.AggregateAsync(ReportType.ShopRanking, period, ct);
        return Ok(ApiResponse.Success(report));
    }

    /// <summary>
    /// 获取报表列表（按类型和时间范围查询）。
    /// </summary>
    [HttpGet("reports")]
    [ProducesResponseType(typeof(ApiResponse<List<DashboardReport>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReportsAsync(
        [FromQuery] ReportType reportType,
        [FromQuery] DateTime? start,
        [FromQuery] DateTime? end,
        CancellationToken ct)
    {
        var period = GetPeriodOrDefault(start, end);
        var reports = await _reportRepository.GetByPeriodAsync(reportType, period.Start, period.End, ct);
        return Ok(ApiResponse.Success(reports));
    }

    /// <summary>
    /// 获取报表详情。
    /// </summary>
    [HttpGet("reports/{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<DashboardReport>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetReportByIdAsync(Guid id, CancellationToken ct)
    {
        var report = await _reportRepository.GetByIdAsync(id, ct);
        if (report is null)
        {
            return NotFound(ApiResponse.Fail(404, "报表不存在"));
        }

        return Ok(ApiResponse.Success(report));
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
}