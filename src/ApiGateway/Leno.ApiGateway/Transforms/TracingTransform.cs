using System.Diagnostics;
using Yarp.ReverseProxy.Transforms;

namespace Leno.ApiGateway.Transforms;

/// <summary>
/// YARP 出站请求 Transform，在转发到后端微服务的请求上注入非标准的
/// <c>X-Trace-Id</c> 头（值为当前 <see cref="Activity.TraceId"/>）。
/// <para>
/// W3C 标准 <c>traceparent</c> 头由 OpenTelemetry 的 Http Instrumentation
/// 在 YARP 内部 HttpClient 发起出站请求时自动注入，本 Transform 不重复设置。
/// <c>X-Trace-Id</c> 仅为尚未集成 OTel SDK 的旧后端服务提供 TraceId 关联能力。
/// </para>
/// </summary>
public sealed class TracingTransform : RequestTransform
{
    private const string XTraceIdHeader = "X-Trace-Id";

    /// <summary>
    /// 在 YARP 构造出站 <see cref="HttpRequestMessage"/> 时调用，
    /// 若当前存在 <see cref="Activity"/> 且未已设置 <c>X-Trace-Id</c> 头，则注入 TraceId。
    /// </summary>
    public override ValueTask ApplyAsync(RequestTransformContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var activity = Activity.Current;
        if (activity is null)
        {
            return ValueTask.CompletedTask;
        }

        // 不覆盖既有值（上游可能已显式设置）
        if (context.ProxyRequest.Headers.Contains(XTraceIdHeader))
        {
            return ValueTask.CompletedTask;
        }

        var traceId = activity.TraceId.ToString();
        if (!string.IsNullOrEmpty(traceId))
        {
            context.ProxyRequest.Headers.TryAddWithoutValidation(XTraceIdHeader, traceId);
        }

        return ValueTask.CompletedTask;
    }
}
