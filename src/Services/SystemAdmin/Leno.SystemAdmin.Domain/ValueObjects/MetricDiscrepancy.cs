using Leno.SystemAdmin.Domain.Exceptions;

namespace Leno.SystemAdmin.Domain.ValueObjects;

/// <summary>
/// 指标差异项值对象，表示对账过程中发现的单个指标差异。
/// 不可变记录，包含聚合值与域值及其差异。
/// </summary>
public sealed record MetricDiscrepancy
{
    private const int MaxKeyLength = 128;

    /// <summary>指标键，如 "total_gmv", "success_rate"。</summary>
    public string MetricKey { get; }

    /// <summary>SystemAdmin 聚合统计值。</summary>
    public decimal AggregatedValue { get; }

    /// <summary>各域事件溯源统计值。</summary>
    public decimal DomainValue { get; }

    /// <summary>差异绝对值。</summary>
    public decimal Difference { get; }

    /// <summary>差异百分比（相对于 DomainValue），DomainValue 为 0 时取 0。</summary>
    public decimal DifferencePercentage { get; }

    public MetricDiscrepancy(string metricKey, decimal aggregatedValue, decimal domainValue)
    {
        if (string.IsNullOrWhiteSpace(metricKey))
        {
            throw new SystemAdminDomainException("指标键不可为空", "DISCREPANCY_KEY_EMPTY");
        }

        if (metricKey.Trim().Length > MaxKeyLength)
        {
            throw new SystemAdminDomainException($"指标键长度不可超过 {MaxKeyLength} 字符", "DISCREPANCY_KEY_LENGTH");
        }

        MetricKey = metricKey.Trim();
        AggregatedValue = aggregatedValue;
        DomainValue = domainValue;
        Difference = Math.Abs(aggregatedValue - domainValue);
        DifferencePercentage = domainValue != 0
            ? Math.Round(Math.Abs(aggregatedValue - domainValue) / Math.Abs(domainValue) * 100, 4)
            : 0;
    }
}