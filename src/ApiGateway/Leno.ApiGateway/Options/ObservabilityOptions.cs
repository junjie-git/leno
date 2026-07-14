namespace Leno.ApiGateway.Options;

/// <summary>
/// 可观测性顶层配置节，对应 appsettings.json 中 <c>OpenTelemetry</c> 与 <c>Metrics</c> 节。
/// </summary>
public sealed class ObservabilityOptions
{
    /// <summary>OpenTelemetry 配置节。</summary>
    public OpenTelemetryOptions OpenTelemetry { get; set; } = new();

    /// <summary>Prometheus 指标暴露配置节。</summary>
    public MetricsOptions Metrics { get; set; } = new();
}

/// <summary>
/// OpenTelemetry 追踪导出配置。
/// </summary>
public sealed class OpenTelemetryOptions
{
    /// <summary>是否启用 OTel 追踪导出。</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Exporter 类型：otlp（默认）或 none。</summary>
    public string Exporter { get; set; } = "otlp";

    /// <summary>OTLP gRPC 端点（如 http://localhost:4317）。</summary>
    public string Endpoint { get; set; } = "http://localhost:4317";

    /// <summary>ServiceName 标识，用于在 Jaeger 中区分服务。</summary>
    public string ServiceName { get; set; } = "leno-api-gateway";
}

/// <summary>
/// Prometheus 指标暴露配置。
/// </summary>
public sealed class MetricsOptions
{
    /// <summary>是否启用 /metrics 端点。</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>指标暴露路径。</summary>
    public string Path { get; set; } = "/metrics";
}
