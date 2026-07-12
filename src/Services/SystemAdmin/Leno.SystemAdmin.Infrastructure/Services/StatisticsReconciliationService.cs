using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Repositories;
using Leno.SystemAdmin.Domain.Services;
using Leno.SystemAdmin.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;

namespace Leno.SystemAdmin.Infrastructure.Services;

/// <summary>
/// 统计数据对账服务实现。
/// 比对 SystemAdmin 聚合统计与各域事件溯源统计，生成对账记录。
/// SystemAdmin 域仅以只读方式消费各域集成事件，不写回任何域的写库。
/// </summary>
public sealed class StatisticsReconciliationService : IStatisticsReconciliationService
{
    private readonly IStatisticsAggregationService _aggregationService;
    private readonly IDashboardReportRepository _dashboardReportRepository;
    private readonly IReconciliationRecordRepository _reconciliationRecordRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StatisticsReconciliationService> _logger;

    public StatisticsReconciliationService(
        IStatisticsAggregationService aggregationService,
        IDashboardReportRepository dashboardReportRepository,
        IReconciliationRecordRepository reconciliationRecordRepository,
        IUnitOfWork unitOfWork,
        ILogger<StatisticsReconciliationService> logger)
    {
        ArgumentNullException.ThrowIfNull(aggregationService);
        ArgumentNullException.ThrowIfNull(dashboardReportRepository);
        ArgumentNullException.ThrowIfNull(reconciliationRecordRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(logger);
        _aggregationService = aggregationService;
        _dashboardReportRepository = dashboardReportRepository;
        _reconciliationRecordRepository = reconciliationRecordRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<List<ReconciliationRecord>> ReconcileAllAsync(
        ReportPeriod period,
        CancellationToken ct = default)
    {
        var records = new List<ReconciliationRecord>();
        var reportTypes = Enum.GetValues<ReportType>();

        _logger.LogInformation("开始全量对账 PeriodStart={Start} PeriodEnd={End} ReportTypeCount={Count}",
            period.Start, period.End, reportTypes.Length);

        foreach (var reportType in reportTypes)
        {
            try
            {
                var record = await ReconcileAsync(reportType, period, ct);
                records.Add(record);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "对账失败 ReportType={ReportType}", reportType);
                var errorSnapshot = StatisticsSnapshot.CreateError(
                    reportType,
                    period,
                    $"对账异常: {ex.Message}");
                var errorRecord = ReconciliationRecord.Create(Guid.NewGuid(), errorSnapshot);
                await _reconciliationRecordRepository.AddAsync(errorRecord, ct);
                records.Add(errorRecord);
            }
        }

        await _unitOfWork.SaveEntitiesAsync(ct);

        _logger.LogInformation("全量对账完成 RecordCount={Count}", records.Count);
        return records;
    }

    /// <inheritdoc />
    public async Task<ReconciliationRecord> ReconcileAsync(
        ReportType reportType,
        ReportPeriod period,
        CancellationToken ct = default)
    {
        _logger.LogInformation("开始对账 ReportType={ReportType} PeriodStart={Start} PeriodEnd={End}",
            reportType, period.Start, period.End);

        // 1. 获取 SystemAdmin 聚合统计数据
        var aggregatedReport = await _aggregationService.AggregateAsync(reportType, period, ct);
        var aggregatedMetrics = aggregatedReport.Metrics;

        // 2. 获取各域事件溯源统计数据（从 DashboardReport 仓储读取历史报表）
        var domainReports = await _dashboardReportRepository.GetByPeriodAsync(
            reportType, period.Start, period.End, ct);

        var domainMetrics = AggregateDomainMetrics(domainReports);

        // 3. 比对差异
        var discrepancies = CompareMetrics(aggregatedMetrics, domainMetrics);

        // 4. 生成对账快照
        var snapshot = new StatisticsSnapshot(
            reportType,
            period,
            aggregatedMetrics,
            domainMetrics,
            discrepancies);

        // 5. 创建对账记录
        var record = ReconciliationRecord.Create(Guid.NewGuid(), snapshot);

        if (snapshot.Status == ReconciliationStatus.DiscrepancyFound)
        {
            // 记录差异日志
            foreach (var discrepancy in discrepancies)
            {
                _logger.LogWarning(
                    "对账发现差异 ReportType={ReportType} Metric={MetricKey} Aggregated={Aggregated} Domain={Domain} Diff={Diff} Pct={Pct}%",
                    reportType, discrepancy.MetricKey, discrepancy.AggregatedValue,
                    discrepancy.DomainValue, discrepancy.Difference, discrepancy.DifferencePercentage);
            }

            // 触发告警
            record.MarkAlertTriggered();
            _logger.LogWarning("对账差异告警已触发 ReportType={ReportType} DiscrepancyCount={Count}",
                reportType, discrepancies.Count);

            // 触发自动修正（当差异较大时）
            if (discrepancies.Any(d => d.DifferencePercentage > 10))
            {
                record.MarkCorrectionTriggered();
                _logger.LogWarning("对账自动修正已触发 ReportType={ReportType} 差异超过 10%", reportType);
            }
        }
        else
        {
            _logger.LogInformation("对账一致 ReportType={ReportType}", reportType);
        }

        await _reconciliationRecordRepository.AddAsync(record, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        return record;
    }

    /// <summary>
    /// 从各域历史报表中聚合域指标。
    /// 当前基于 DashboardReport 历史数据，后续可扩展为直接从事件溯源读取。
    /// </summary>
    private static List<MetricItem> AggregateDomainMetrics(List<DashboardReport> domainReports)
    {
        if (domainReports.Count == 0)
        {
            return new List<MetricItem>();
        }

        var allMetrics = domainReports.SelectMany(r => r.Metrics).ToList();
        var grouped = allMetrics
            .GroupBy(m => m.Key)
            .Select(g => new MetricItem(g.Key, g.Sum(m => m.Value), g.First().Unit))
            .ToList();

        return grouped;
    }

    /// <summary>
    /// 比对聚合指标与域指标，生成差异列表。
    /// 仅对同名指标进行比对，差异百分比超过 1% 视为差异。
    /// </summary>
    private static List<MetricDiscrepancy> CompareMetrics(
        List<MetricItem> aggregatedMetrics,
        List<MetricItem> domainMetrics)
    {
        var discrepancies = new List<MetricDiscrepancy>();
        var domainDict = domainMetrics.ToDictionary(m => m.Key, m => m.Value);

        foreach (var metric in aggregatedMetrics)
        {
            if (domainDict.TryGetValue(metric.Key, out var domainValue))
            {
                var discrepancy = new MetricDiscrepancy(metric.Key, metric.Value, domainValue);
                // 差异百分比超过 1% 或差异绝对值大于 0.01 时才记录
                if (discrepancy.DifferencePercentage > 1 || discrepancy.Difference > 0.01m)
                {
                    discrepancies.Add(discrepancy);
                }
            }
            else
            {
                // 聚合指标在域数据中不存在，视为差异
                discrepancies.Add(new MetricDiscrepancy(metric.Key, metric.Value, 0));
            }
        }

        return discrepancies;
    }
}