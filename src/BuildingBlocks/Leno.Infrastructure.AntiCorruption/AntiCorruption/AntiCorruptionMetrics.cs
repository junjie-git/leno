using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace Leno.Infrastructure.AntiCorruption;

/// <summary>
/// 防腐层可观测性指标（M4.1 + M4 双轨方案 + 阶段四 4.2 可插拔策略链）。
/// 由所有 BC 共享，Meter 名 <c>Leno.AntiCorruption</c>。
/// 各 BC 启动时通过 <c>AddLenoOpenTelemetry</c> 回调 <c>.AddMeter("Leno.AntiCorruption")</c> 订阅。
/// <para>
/// 阶段四 4.2 新增 channel 维度指标：
/// <list type="bullet">
/// <item><see cref="ChannelFailureCounter"/>：按 channel/service/operation 维度统计通道失败次数</item>
/// <item><see cref="ChannelDispatchCounter"/>：按 channel/service/operation 维度统计调度命中次数</item>
/// <item><see cref="ChannelCircuitStateGauge"/>：按 channel 维度统计熔断状态（0=Closed, 1=Open, 2=HalfOpen）</item>
/// </list>
/// </para>
/// </summary>
public static class AntiCorruptionMetrics
{
    public const string MeterNamePrefix = "Leno.";
    public const string ServiceLabel = "service";
    public const string OperationLabel = "operation";
    public const string ReasonLabel = "reason";
    public const string StatusCodeLabel = "status_code";
    public const string PathLabel = "path";
    public const string ChannelLabel = "channel";
    public const string FailureCounterName = "anticorruption_failure_total";
    public const string FallbackCounterName = "anticorruption_fallback_total";
    public const string CircuitOpenGaugeName = "anticorruption_circuit_open";
    public const string GrpcRequestCounterName = "anticorruption_grpc_request_total";
    public const string GrpcDurationHistogramName = "anticorruption_grpc_duration_seconds";
    public const string ChannelFailureCounterName = "anticorruption_channel_failure_total";
    public const string ChannelDispatchCounterName = "anticorruption_channel_dispatch_total";
    public const string ChannelCircuitStateGaugeName = "anticorruption_channel_circuit_state";

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
    /// ACL 通道失败次数计数器（阶段四 4.2）。
    /// 按 channel/service/operation 维度统计每次通道调用失败。
    /// </summary>
    public static Counter<int> ChannelFailureCounter { get; } =
        _meter.CreateCounter<int>(
            ChannelFailureCounterName,
            unit: "times",
            description: "ACL 通道调用失败次数（按 channel/service/operation 维度统计）");

    /// <summary>
    /// ACL 通道调度命中次数计数器（阶段四 4.2）。
    /// 按 channel/service/operation/result 维度统计策略链遍历命中（result=success/fail/business_fail）。
    /// </summary>
    public static Counter<int> ChannelDispatchCounter { get; } =
        _meter.CreateCounter<int>(
            ChannelDispatchCounterName,
            unit: "times",
            description: "ACL 通道调度命中次数（按 channel/service/operation/result 维度统计）");

    /// <summary>
    /// ACL 通道熔断状态 Gauge（阶段四 4.2）。
    /// 按 channel 维度统计三态熔断状态：0=Closed, 1=Open, 2=HalfOpen。
    /// 由 <see cref="AclChannelRegistry"/> 维护回调。
    /// </summary>
    public static ObservableGauge<int> ChannelCircuitStateGauge { get; private set; } = null!;

    /// <summary>
    /// 熔断器状态值回调表（service -> 0=Closed / 1=Open / 2=HalfOpen）。由 CircuitBreakerState 维护。
    /// T14 修复：原仅区分 Open(1)/Closed|HalfOpen(0)，现区分三态，运维可识别 HalfOpen 探测中。
    /// </summary>
    /// <remarks>使用 ConcurrentDictionary 保证多 BC 并发写入与 OTLP 枚举的线程安全。</remarks>
    private static readonly ConcurrentDictionary<string, int> _circuitOpenStates = new();

    /// <summary>
    /// 通道熔断状态值回调表（channel -> 0=Closed / 1=Open / 2=HalfOpen）。
    /// 阶段四 4.2 新增：由 <see cref="AclChannelRegistry"/> 维护，用于按通道维度监控熔断状态。
    /// </summary>
    private static readonly ConcurrentDictionary<string, int> _channelCircuitStates = new();

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

        ChannelCircuitStateGauge ??= _meter.CreateObservableGauge<int>(
            ChannelCircuitStateGaugeName,
            observeValues: () => _channelCircuitStates.Select(kv => new Measurement<int>(
                kv.Value,
                new KeyValuePair<string, object?>(ChannelLabel, kv.Key))),
            unit: "state",
            description: "ACL 通道熔断状态（0=Closed，1=Open，2=HalfOpen）");
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

    /// <summary>
    /// 记录一次 ACL 通道失败（阶段四 4.2）。
    /// 按 channel/service/operation 维度埋点。
    /// </summary>
    /// <param name="channel">通道名（如 "grpc", "http"）。</param>
    /// <param name="service">防腐层服务标识。</param>
    /// <param name="operation">操作名。</param>
    public static void RecordChannelFailure(string channel, string service, string operation)
    {
        if (string.IsNullOrEmpty(channel) || string.IsNullOrEmpty(service) || string.IsNullOrEmpty(operation))
        {
            return;
        }

        ChannelFailureCounter.Add(1,
            new KeyValuePair<string, object?>(ChannelLabel, channel),
            new KeyValuePair<string, object?>(ServiceLabel, service),
            new KeyValuePair<string, object?>(OperationLabel, operation));
    }

    /// <summary>
    /// 记录一次 ACL 通道调度命中（阶段四 4.2）。
    /// 按 channel/service/operation/result 维度埋点，用于分析策略链遍历命中率。
    /// </summary>
    /// <param name="channel">通道名。</param>
    /// <param name="service">防腐层服务标识。</param>
    /// <param name="operation">操作名。</param>
    /// <param name="result">调度结果：success / infra_failure / business_failure。</param>
    public static void RecordChannelDispatch(string channel, string service, string operation, string result)
    {
        if (string.IsNullOrEmpty(channel) || string.IsNullOrEmpty(service) || string.IsNullOrEmpty(operation))
        {
            return;
        }

        ChannelDispatchCounter.Add(1,
            new KeyValuePair<string, object?>(ChannelLabel, channel),
            new KeyValuePair<string, object?>(ServiceLabel, service),
            new KeyValuePair<string, object?>(OperationLabel, operation),
            new KeyValuePair<string, object?>("result", result ?? "unknown"));
    }

    /// <summary>
    /// 更新 ACL 通道熔断三态状态（阶段四 4.2）。
    /// 由 <see cref="AclChannelRegistry"/> 维护，与既有 <see cref="UpdateCircuitState"/> 独立。
    /// </summary>
    /// <param name="channel">通道名（如 "grpc", "http"）。</param>
    /// <param name="state">熔断器状态值：0=Closed, 1=Open, 2=HalfOpen。</param>
    public static void UpdateChannelCircuitState(string channel, int state)
    {
        if (string.IsNullOrEmpty(channel))
        {
            return;
        }
        _channelCircuitStates[channel] = state;
    }
}
