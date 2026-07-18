using System.Diagnostics.Metrics;

namespace Leno.Infrastructure.AntiCorruption;

/// <summary>
/// 防腐层可观测性指标（M4.1）。
/// 由所有 BC 共享，Meter 名 <c>Leno.&lt;BC&gt;.AntiCorruption</c> 通过 <see cref="GetMeterName"/> 生成。
/// 各 BC 启动时通过 <c>AddLenoOpenTelemetry</c> 回调 <c>.AddMeter(AntiCorruptionMetrics.MeterName)</c> 订阅。
/// </summary>
public static class AntiCorruptionMetrics
{
    public const string MeterNamePrefix = "Leno.";
    public const string ServiceLabel = "service";
    public const string OperationLabel = "operation";
    public const string FailureCounterName = "anticorruption_failure_total";

    private static readonly Meter _meter = new("Leno.AntiCorruption", "1.0.0");

    public static Meter Meter => _meter;

    public static Counter<int> FailureCounter { get; } =
        _meter.CreateCounter<int>(
            FailureCounterName,
            unit: "times",
            description: "防腐层远程调用失败次数（按 service/operation 维度统计）");

    public static string GetMeterName(string bcName)
        => $"{MeterNamePrefix}{bcName}.AntiCorruption";

    public static void RecordFailure(string service, string operation)
    {
        if (string.IsNullOrEmpty(service) || string.IsNullOrEmpty(operation))
        {
            return;
        }

        FailureCounter.Add(1, new KeyValuePair<string, object?>(ServiceLabel, service),
                              new KeyValuePair<string, object?>(OperationLabel, operation));
    }
}
