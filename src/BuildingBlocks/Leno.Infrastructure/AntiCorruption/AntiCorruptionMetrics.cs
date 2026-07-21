using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace Leno.Infrastructure.AntiCorruption;

/// <summary>
/// 防腐层可观测性指标（M4.1 + M4 双轨方案）。
/// 由所有 BC 共享，Meter 名 <c>Leno.AntiCorruption</c>。
/// 各 BC 启动时通过 <c>AddLenoOpenTelemetry</c> 回调 <c>.AddMeter("Leno.AntiCorruption")</c> 订阅。
/// </summary>
public static class AntiCorruptionMetrics
{
    public const string MeterNamePrefix = "Leno.";
    public const string ServiceLabel = "service";
    public const string OperationLabel = "operation";
    public const string ReasonLabel = "reason";
    public const string StatusCodeLabel = "status_code";
    public const string PathLabel = "path";
    public const string FailureCounterName = "anticorruption_failure_total";
    public const string FallbackCounterName = "anticorruption_fallback_total";
    public const string CircuitOpenGaugeName = "anticorruption_circuit_open";
    public const string GrpcRequestCounterName = "anticorruption_grpc_request_total";
    public const string GrpcDurationHistogramName = "anticorruption_grpc_duration_seconds";

    private static readonly Meter _meter = new("Leno.AntiCorruption", "1.0.0");

    public static Meter Meter => _meter;

    public static Counter<int> FailureCounter { get; } =
        _meter.CreateCounter<int>(
            FailureCounterName,
            unit: "times",
            description: "防腐层远程调用失败次数（按 service/operation/path 维度统计）");

    public static Counter<int> FallbackCounter { get; } =
        _meter.CreateCounter<int>(
            FallbackCounterName,
            unit: "times",
            description: "gRPC 降级到 HttpClient 的次数（按 service/reason 维度统计）");

    public static ObservableGauge<int> CircuitOpenGauge { get; private set; } = null!;

    public static Counter<int> GrpcRequestCounter { get; } =
        _meter.CreateCounter<int>(
            GrpcRequestCounterName,
            unit: "times",
            description: "gRPC 调用计数（按 service/status_code 维度统计）");

    public static Histogram<double> GrpcDurationHistogram { get; } =
        _meter.CreateHistogram<double>(
            GrpcDurationHistogramName,
            unit: "s",
            description: "gRPC 调用延迟分布（按 service/status_code 维度统计）");

    /// <summary>
    /// 熔断器状态值回调表（service -> 0=Closed / 1=Open / 2=HalfOpen）。由 CircuitBreakerState 维护。
    /// T14 修复：原仅区分 Open(1)/Closed|HalfOpen(0)，现区分三态，运维可识别 HalfOpen 探测中。
    /// </summary>
    /// <remarks>使用 ConcurrentDictionary 保证多 BC 并发写入与 OTLP 枚举的线程安全。</remarks>
    private static readonly ConcurrentDictionary<string, int> _circuitOpenStates = new();

    /// <summary>初始化 ObservableGauge（启动时调用一次即可，重复调用幂等）。</summary>
    public static void Initialize()
    {
        CircuitOpenGauge ??= _meter.CreateObservableGauge<int>(
            CircuitOpenGaugeName,
            observeValues: () => _circuitOpenStates.Select(kv => new Measurement<int>(
                kv.Value,
                new KeyValuePair<string, object?>(ServiceLabel, kv.Key))),
            unit: "state",
            description: "熔断器状态（0=Closed，1=Open，2=HalfOpen）");
    }

    public static string GetMeterName(string bcName)
        => $"{MeterNamePrefix}{bcName}.AntiCorruption";

    public static void RecordFailure(string service, string operation, string path = "http")
    {
        if (string.IsNullOrEmpty(service) || string.IsNullOrEmpty(operation))
        {
            return;
        }

        FailureCounter.Add(1,
            new KeyValuePair<string, object?>(ServiceLabel, service),
            new KeyValuePair<string, object?>(OperationLabel, operation),
            new KeyValuePair<string, object?>(PathLabel, path));
    }

    /// <summary>记录一次 gRPC 降级到 HttpClient 的事件。</summary>
    /// <param name="service">防腐层服务标识。</param>
    /// <param name="reason">降级原因：circuit_open / grpc_Unavailable / grpc_DeadlineExceeded / grpc_Internal / grpc_ResourceExhausted / grpc_unknown。</param>
    public static void RecordFallback(string service, string reason)
    {
        if (string.IsNullOrEmpty(service) || string.IsNullOrEmpty(reason))
        {
            return;
        }

        FallbackCounter.Add(1,
            new KeyValuePair<string, object?>(ServiceLabel, service),
            new KeyValuePair<string, object?>(ReasonLabel, reason));
    }

    /// <summary>
    /// 更新熔断器三态状态（由 CircuitBreakerState 调用）。
    /// T14 修复：替代原 <see cref="UpdateCircuitOpenState"/> 的二态布尔值，
    /// 支持区分 Closed(0) / Open(1) / HalfOpen(2)。
    /// </summary>
    /// <param name="service">防腐层服务标识。</param>
    /// <param name="state">熔断器状态值：0=Closed, 1=Open, 2=HalfOpen。</param>
    public static void UpdateCircuitState(string service, int state)
    {
        _circuitOpenStates[service] = state;
    }

    /// <summary>
    /// 更新熔断器 Open 状态（向后兼容重载，由 Order.Infrastructure.LogisticsTrackingService 等历史调用方使用）。
    /// 内部委托给 <see cref="UpdateCircuitState"/>：isOpen=true→1(Open)，isOpen=false→0(Closed)。
    /// </summary>
    public static void UpdateCircuitOpenState(string service, bool isOpen)
    {
        UpdateCircuitState(service, isOpen ? 1 : 0);
    }

    /// <summary>记录一次 gRPC 调用计数与延迟。</summary>
    public static void RecordGrpcRequest(string service, string statusCode, double durationSeconds)
    {
        if (string.IsNullOrEmpty(service))
        {
            return;
        }

        GrpcRequestCounter.Add(1,
            new KeyValuePair<string, object?>(ServiceLabel, service),
            new KeyValuePair<string, object?>(StatusCodeLabel, statusCode));

        GrpcDurationHistogram.Record(durationSeconds,
            new KeyValuePair<string, object?>(ServiceLabel, service),
            new KeyValuePair<string, object?>(StatusCodeLabel, statusCode));
    }
}
