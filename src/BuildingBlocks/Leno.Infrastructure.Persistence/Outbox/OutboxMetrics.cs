using System.Diagnostics.Metrics;

namespace Leno.Infrastructure.Outbox;

/// <summary>
/// Outbox 积压与处理指标（M5.3）。
/// 暴露 Prometheus 指标 <c>outbox_pending_count</c>（gauge）与 <c>outbox_published_total</c>（counter）。
/// 由 Outbox 后台服务定期调用 <see cref="SetPendingCount"/> 更新积压值。
/// </summary>
public static class OutboxMetrics
{
    public const string MeterName = "Leno.Outbox";

    private static readonly Meter _meter = new(MeterName, "1.0.0");

    /// <summary>Outbox 待发布消息数（gauge），由后台服务定期更新。</summary>
    public static ObservableGauge<int> PendingCountGauge { get; }

    /// <summary>Outbox 已发布消息数（counter）。</summary>
    public static Counter<int> PublishedCounter { get; } =
        _meter.CreateCounter<int>("outbox_published_total", unit: "messages", description: "Outbox 已发布消息数");

    private static int _currentPendingCount;

    static OutboxMetrics()
    {
        PendingCountGauge = _meter.CreateObservableGauge<int>(
            "outbox_pending_count",
            () => new Measurement<int>(_currentPendingCount),
            unit: "messages",
            description: "Outbox 待发布消息数");
    }

    /// <summary>更新 Outbox 待发布消息数（由后台服务定期调用）。</summary>
    public static void SetPendingCount(int count)
    {
        Interlocked.Exchange(ref _currentPendingCount, count < 0 ? 0 : count);
    }

    /// <summary>记录一次成功发布。</summary>
    public static void RecordPublished(string bcName)
    {
        PublishedCounter.Add(1, new KeyValuePair<string, object?>("bc", bcName));
    }
}
