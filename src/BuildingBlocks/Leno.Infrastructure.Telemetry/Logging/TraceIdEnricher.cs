using System.Diagnostics;
using Serilog.Core;
using Serilog.Events;

namespace Leno.Infrastructure.Logging;

/// <summary>
/// 统一的 TraceId 日志富化器：优先从 OTel <see cref="Activity.Current"/> 获取 TraceId，
/// 回退到 Serilog LogContext 中的 TraceId 属性。
/// <para>
/// 合并自原 SerilogConfig.cs 中的 TraceIdEnricher 与 OpenTelemetryExtensions.cs 中的同名 OTel 富化器，
/// 消除双份实现，统一支持 OTel Activity 与 Serilog LogContext 双来源。
/// </para>
/// 当 OTel Activity 可用时，同时注入 SpanId，保留分布式追踪上下文。
/// </summary>
public sealed class TraceIdEnricher : ILogEventEnricher
{
    private const string ZeroTraceId = "00000000000000000000000000000000";

    /// <inheritdoc />
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        ArgumentNullException.ThrowIfNull(propertyFactory);

        // 优先 OTel Activity
        var activity = Activity.Current;
        var traceId = activity?.TraceId.ToString();
        var hasOtelTrace = !string.IsNullOrEmpty(traceId) && traceId != ZeroTraceId;

        // 回退 Serilog LogContext：从日志事件属性中读取已有 TraceId（如由 LogContext.PushProperty 注入）
        if (!hasOtelTrace
            && logEvent.Properties.TryGetValue("TraceId", out var serilogTraceId))
        {
            traceId = serilogTraceId.ToString().Trim('"');
        }

        if (!string.IsNullOrEmpty(traceId) && traceId != ZeroTraceId)
        {
            logEvent.AddPropertyIfAbsent(
                propertyFactory.CreateProperty("TraceId", traceId));
        }

        // OTel Activity 可用时同时注入 SpanId，保留 Span 上下文
        if (hasOtelTrace)
        {
            var spanId = activity?.SpanId.ToString();
            if (!string.IsNullOrEmpty(spanId))
            {
                logEvent.AddPropertyIfAbsent(
                    propertyFactory.CreateProperty("SpanId", spanId));
            }
        }
    }
}
