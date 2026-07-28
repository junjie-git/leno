using System.Collections.Concurrent;
using Leno.SystemAdmin.Domain.Services;
using Leno.SystemAdmin.Domain.ValueObjects;

namespace Leno.SystemAdmin.Infrastructure.Services;

/// <summary>
/// 内存滚动窗口指标历史存储：3 个 metric × 300 点。
/// 使用 ConcurrentQueue + lock 保证线程安全；超过 maxPoints 时移除最早点。
/// 重启清空符合"实时监控"语义。
/// </summary>
public sealed class MemoryMetricHistoryStore : IMetricHistoryStore
{
    private const int DefaultMaxPointsPerMetric = 300;
    private readonly int _maxPointsPerMetric;
    private readonly ConcurrentDictionary<MetricName, ConcurrentQueue<MetricPointDto>> _stores = new();

    public MemoryMetricHistoryStore(int maxPointsPerMetric = DefaultMaxPointsPerMetric)
    {
        if (maxPointsPerMetric <= 0)
        {
            throw new ArgumentException("maxPointsPerMetric 必须大于 0", nameof(maxPointsPerMetric));
        }
        _maxPointsPerMetric = maxPointsPerMetric;
    }

    /// <inheritdoc />
    public Task RecordAsync(MetricName metric, double value, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var queue = _stores.GetOrAdd(metric, _ => new ConcurrentQueue<MetricPointDto>());
        lock (queue)
        {
            queue.Enqueue(new MetricPointDto { Timestamp = DateTime.UtcNow, Value = value });
            while (queue.Count > _maxPointsPerMetric && queue.TryDequeue(out _))
            {
                // 滚动窗口：移除最早点
            }
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<List<MetricPointDto>> GetHistoryAsync(MetricName metric, TimeSpan range, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (!_stores.TryGetValue(metric, out var queue))
        {
            return Task.FromResult(new List<MetricPointDto>());
        }

        var threshold = DateTime.UtcNow - range;
        List<MetricPointDto> snapshot;
        lock (queue)
        {
            snapshot = queue.Where(p => p.Timestamp >= threshold).ToList();
        }
        return Task.FromResult(snapshot);
    }
}
