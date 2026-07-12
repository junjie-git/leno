using Leno.SystemAdmin.Domain.Exceptions;
using Leno.SystemAdmin.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;

namespace Leno.SystemAdmin.Domain.Aggregates;

/// <summary>
/// 运营数据看板报表聚合根，记录指定时间周期内某类运营指标的聚合数据。
/// 报表生成后不可变，仅追加不可修改。聚合标识 <see cref="Entity.Id"/> 即对外 <c>ReportId</c>。
/// </summary>
public sealed class DashboardReport : AggregateRoot
{
    private const int MaxGranularityLength = 16;

    /// <summary>聚合标识，等同 <see cref="Entity.Id"/>。</summary>
    public Guid ReportId => Id;

    /// <summary>报表类型。</summary>
    public ReportType ReportType { get; private set; }

    /// <summary>报表覆盖的时间周期。</summary>
    public ReportPeriod Period { get; private set; } = default!;

    /// <summary>指标项列表。</summary>
    public List<MetricItem> Metrics { get; private set; } = new();

    /// <summary>统计粒度：hourly / daily / weekly。</summary>
    public string Granularity { get; private set; } = string.Empty;

    /// <summary>报表生成时间（UTC）。</summary>
    public DateTime GeneratedAt { get; private set; }

    /// <summary>数据版本号，用于标识数据源快照版本。</summary>
    public int DataVersion { get; private set; }

    /// <summary>EF Core 无参构造。</summary>
    private DashboardReport() { }

    private DashboardReport(Guid id) : base(id) { }

    /// <summary>
    /// 工厂方法，校验各字段与必填项，构建运营数据报表。报表仅追加，无更新方法。
    /// </summary>
    /// <param name="reportId">报表标识，由应用层生成。</param>
    /// <param name="reportType">报表类型。</param>
    /// <param name="period">报表覆盖的时间周期。</param>
    /// <param name="metrics">指标项列表。</param>
    /// <param name="granularity">统计粒度：hourly / daily / weekly。</param>
    public static DashboardReport Create(
        Guid reportId,
        ReportType reportType,
        ReportPeriod period,
        List<MetricItem> metrics,
        string granularity)
    {
        if (reportId == Guid.Empty)
        {
            throw new SystemAdminDomainException("报表标识不可为空", "REPORT_ID_EMPTY");
        }

        ArgumentNullException.ThrowIfNull(period);

        if (metrics is null || metrics.Count == 0)
        {
            throw new SystemAdminDomainException("指标项列表不可为空", "REPORT_METRICS_EMPTY");
        }

        ValidateGranularity(granularity);

        return new DashboardReport(reportId)
        {
            ReportType = reportType,
            Period = period,
            Metrics = metrics,
            Granularity = granularity.Trim().ToLowerInvariant(),
            GeneratedAt = DateTime.UtcNow,
            DataVersion = 1
        };
    }

    private static void ValidateGranularity(string granularity)
    {
        if (string.IsNullOrWhiteSpace(granularity))
        {
            throw new SystemAdminDomainException("统计粒度不可为空", "REPORT_GRANULARITY_EMPTY");
        }

        var trimmed = granularity.Trim().ToLowerInvariant();
        if (trimmed.Length > MaxGranularityLength)
        {
            throw new SystemAdminDomainException($"统计粒度长度不可超过 {MaxGranularityLength} 字符", "REPORT_GRANULARITY_LENGTH");
        }

        if (trimmed is not ("hourly" or "daily" or "weekly"))
        {
            throw new SystemAdminDomainException("统计粒度必须为 hourly / daily / weekly", "REPORT_GRANULARITY_INVALID");
        }
    }
}