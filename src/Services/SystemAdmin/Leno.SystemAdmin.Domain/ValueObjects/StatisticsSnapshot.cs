using Leno.SystemAdmin.Domain.Exceptions;

namespace Leno.SystemAdmin.Domain.ValueObjects;

/// <summary>
/// 统计快照值对象，存储对账结果。
/// 包含聚合指标与域指标及其差异项列表，用于对账审计与告警。
/// 不可变记录。
/// </summary>
public sealed record StatisticsSnapshot
{
    private const int MaxMetricsCount = 200;

    /// <summary>对账的报表类型。</summary>
    public ReportType ReportType { get; }

    /// <summary>对账覆盖的时间周期。</summary>
    public ReportPeriod Period { get; }

    /// <summary>SystemAdmin 聚合统计指标列表。</summary>
    public List<MetricItem> AggregatedMetrics { get; }

    /// <summary>各域事件溯源统计指标列表。</summary>
    public List<MetricItem> DomainMetrics { get; }

    /// <summary>差异项列表。</summary>
    public List<MetricDiscrepancy> Discrepancies { get; }

    /// <summary>
    /// 对账状态，由差异项自动推导。
    /// </summary>
    public ReconciliationStatus Status { get; }

    /// <summary>对账时的错误信息，对账失败时填充。</summary>
    public string? ErrorMessage { get; }

    public StatisticsSnapshot(
        ReportType reportType,
        ReportPeriod period,
        List<MetricItem> aggregatedMetrics,
        List<MetricItem> domainMetrics,
        List<MetricDiscrepancy> discrepancies)
    {
        ArgumentNullException.ThrowIfNull(period);
        ArgumentNullException.ThrowIfNull(aggregatedMetrics);
        ArgumentNullException.ThrowIfNull(domainMetrics);
        ArgumentNullException.ThrowIfNull(discrepancies);

        if (aggregatedMetrics.Count == 0 && domainMetrics.Count == 0)
        {
            throw new SystemAdminDomainException("聚合指标与域指标不可同时为空", "SNAPSHOT_METRICS_EMPTY");
        }

        if (aggregatedMetrics.Count > MaxMetricsCount)
        {
            throw new SystemAdminDomainException($"聚合指标数量不可超过 {MaxMetricsCount}", "SNAPSHOT_METRICS_TOO_MANY");
        }

        if (domainMetrics.Count > MaxMetricsCount)
        {
            throw new SystemAdminDomainException($"域指标数量不可超过 {MaxMetricsCount}", "SNAPSHOT_DOMAIN_METRICS_TOO_MANY");
        }

        ReportType = reportType;
        Period = period;
        AggregatedMetrics = aggregatedMetrics;
        DomainMetrics = domainMetrics;
        Discrepancies = discrepancies;
        Status = discrepancies.Count > 0 ? ReconciliationStatus.DiscrepancyFound : ReconciliationStatus.Consistent;
        ErrorMessage = null;
    }

    /// <summary>
    /// 创建对账失败的快照。
    /// </summary>
    public static StatisticsSnapshot CreateError(
        ReportType reportType,
        ReportPeriod period,
        string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(errorMessage))
        {
            throw new SystemAdminDomainException("对账错误信息不可为空", "SNAPSHOT_ERROR_EMPTY");
        }

        return new StatisticsSnapshot(reportType, period, errorMessage);
    }

    private StatisticsSnapshot(
        ReportType reportType,
        ReportPeriod period,
        string errorMessage)
    {
        ReportType = reportType;
        Period = period;
        AggregatedMetrics = new List<MetricItem>();
        DomainMetrics = new List<MetricItem>();
        Discrepancies = new List<MetricDiscrepancy>();
        Status = ReconciliationStatus.Error;
        ErrorMessage = errorMessage;
    }
}