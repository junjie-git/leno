using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Services;
using Leno.SystemAdmin.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Leno.SystemAdmin.Infrastructure.Services;

/// <summary>
/// 运营数据统计聚合服务实现，按报表类型从 <see cref="IStatisticsDataSource"/> 获取真实指标数据，
/// 组装为 <see cref="DashboardReport"/> 聚合根。
/// </summary>
public sealed class StatisticsAggregationService : IStatisticsAggregationService
{
    private readonly IStatisticsDataSource _dataSource;
    private readonly ILogger<StatisticsAggregationService> _logger;

    public StatisticsAggregationService(
        IStatisticsDataSource dataSource,
        ILogger<StatisticsAggregationService> logger)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        ArgumentNullException.ThrowIfNull(logger);
        _dataSource = dataSource;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<DashboardReport> AggregateAsync(
        ReportType reportType,
        ReportPeriod period,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "开始聚合运营数据 ReportType={ReportType} Start={Start} End={End}",
            reportType, period.Start, period.End);

        var metrics = await _dataSource.GetMetricsAsync(reportType, period, ct);

        if (metrics is null || metrics.Count == 0)
        {
            throw new ArgumentException(
                $"数据源未返回任何指标数据 ReportType={reportType}", nameof(reportType));
        }

        var granularity = DetermineGranularity(period);

        var report = DashboardReport.Create(
            Guid.NewGuid(),
            reportType,
            period,
            metrics,
            granularity);

        _logger.LogInformation(
            "运营数据聚合完成 ReportType={ReportType} ReportId={ReportId} MetricCount={MetricCount}",
            reportType, report.ReportId, metrics.Count);

        return report;
    }

    private static string DetermineGranularity(ReportPeriod period)
    {
        var span = period.End - period.Start;
        if (span.TotalHours <= 24) return "hourly";
        if (span.TotalDays <= 7) return "daily";
        return "weekly";
    }
}
