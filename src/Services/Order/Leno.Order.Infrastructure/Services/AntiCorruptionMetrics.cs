using System.Diagnostics.Metrics;

namespace Leno.Order.Infrastructure.Services;

/// <summary>
/// 防腐层可观测性指标（T17）。
/// 使用 <see cref="System.Diagnostics.Metrics.Meter"/> 暴露 Prometheus 兼容指标
/// <c>anticorruption_failure_total{service,operation}</c>，
/// 由 OpenTelemetry Prometheus exporter 或 dotnet-counters 订阅采集。
/// </summary>
/// <remarks>
/// 静态 <see cref="Meter"/> 实例由 <c>ServiceCollectionExtensions.AddOrderInfrastructure</c>
/// 注册到 <see cref="MeterListener"/> / OTel SDK（<c>builder.Services.AddSingleton&lt;Meter&gt;</c> 不必要，
/// OTel 通过 <c>AddMeter(MeterName)</c> 按名称订阅）。各防腐层实现共享此实例。
/// </remarks>
public static class AntiCorruptionMetrics
{
    /// <summary>Meter 名称，OTel SDK 须通过 <c>AddMeter(AntiCorruptionMetrics.MeterName)</c> 订阅。</summary>
    public const string MeterName = "Leno.Order.AntiCorruption";

    /// <summary>防腐层服务标识标签名。</summary>
    public const string ServiceLabel = "service";

    /// <summary>防腐层操作标识标签名。</summary>
    public const string OperationLabel = "operation";

    private static readonly Meter _meter = new(MeterName, "1.0.0");

    /// <summary>
    /// 防腐层远程失败计数器，标签 <c>service</c>（points/promotion）、<c>operation</c>（freeze/release/...）。
    /// 对应 Prometheus 指标 <c>anticorruption_failure_total</c>。
    /// </summary>
    public static Counter<int> FailureCounter { get; } =
        _meter.CreateCounter<int>(
            "anticorruption_failure_total",
            unit: "times",
            description: "防腐层远程调用失败次数（按 service/operation 维度统计）");

    /// <summary>
    /// 记录一次防腐层远程失败，按 service/operation 维度递增计数器。
    /// </summary>
    /// <param name="service">防腐层服务标识（如 <c>points</c>、<c>promotion</c>）。</param>
    /// <param name="operation">操作标识（如 <c>freeze</c>、<c>calculate_discount</c>）。</param>
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
