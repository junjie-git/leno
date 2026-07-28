using Leno.SystemAdmin.Domain.ValueObjects;

namespace Leno.SystemAdmin.Domain.Services;

/// <summary>指标历史存储抽象（内存滚动窗口）。</summary>
public interface IMetricHistoryStore
{
    Task RecordAsync(MetricName metric, double value, CancellationToken ct = default);
    Task<List<MetricPointDto>> GetHistoryAsync(MetricName metric, TimeSpan range, CancellationToken ct = default);
}
