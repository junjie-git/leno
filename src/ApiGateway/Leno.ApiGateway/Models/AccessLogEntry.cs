using System.Text.Json.Serialization;

namespace Leno.ApiGateway.Models;

/// <summary>
/// 统一访问日志结构化数据载体，对应 Spec 6.2 中定义的 10 个标准字段。
/// 经 Serilog 以 JSON 文档形式输出到 Console(stdout) 与 File。
/// </summary>
public sealed record AccessLogEntry
{
    /// <summary>请求时间（UTC，ISO 8601）。</summary>
    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; init; }

    /// <summary>分布式追踪 TraceId（关联 OpenTelemetry Span）。</summary>
    [JsonPropertyName("traceId")]
    public string? TraceId { get; init; }

    /// <summary>HTTP 方法（GET/POST/...）。</summary>
    [JsonPropertyName("method")]
    public string Method { get; init; } = string.Empty;

    /// <summary>请求路径（不含 QueryString）。</summary>
    [JsonPropertyName("path")]
    public string Path { get; init; } = string.Empty;

    /// <summary>响应状态码。</summary>
    [JsonPropertyName("statusCode")]
    public int StatusCode { get; init; }

    /// <summary>请求耗时（毫秒）。</summary>
    [JsonPropertyName("duration")]
    public long Duration { get; init; }

    /// <summary>客户端 IP（优先取 X-Forwarded-For）。</summary>
    [JsonPropertyName("clientIp")]
    public string? ClientIp { get; init; }

    /// <summary>用户 ID（来自 HttpContext.Items["UserId"] 或 X-User-Id 头）。</summary>
    [JsonPropertyName("userId")]
    public string? UserId { get; init; }

    /// <summary>目标微服务（YARP ClusterId）。</summary>
    [JsonPropertyName("targetService")]
    public string? TargetService { get; init; }

    /// <summary>客户端 User-Agent。</summary>
    [JsonPropertyName("userAgent")]
    public string? UserAgent { get; init; }
}
