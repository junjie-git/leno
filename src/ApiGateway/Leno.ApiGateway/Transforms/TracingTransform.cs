using System.Diagnostics;
using Yarp.ReverseProxy.Transforms;
using Yarp.ReverseProxy.Transforms.Builder;

namespace Leno.ApiGateway.Transforms;

/// <summary>
/// YARP 出站请求 Transform，在转发到后端微服务的请求上注入非标准的
/// <c>X-Trace-Id</c> 头（值为当前 <see cref="Activity.TraceId"/>）。
/// <para>
/// W3C 标准 <c>traceparent</c> 头由 OpenTelemetry 的 Http Instrumentation
/// 在 YARP 内部 HttpClient 发起出站请求时自动注入，本 Transform 不重复设置。
/// <c>X-Trace-Id</c> 仅为尚未集成 OTel SDK 的旧后端服务提供 TraceId 关联能力。
/// </para>
/// <para>
/// 本类型同时继承 <see cref="RequestTransform"/> 与实现 <see cref="ITransformProvider"/>，
/// 以便通过 <c>AddTransforms&lt;TracingTransform&gt;()</c> 注册到 YARP 管道，
/// 同时保留可直接调用的 <see cref="ApplyAsync"/> 方法供单元测试使用。
/// </para>
/// </summary>
public sealed class TracingTransform : RequestTransform, ITransformProvider
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

    /// <summary>
    /// 路由级校验钩子。本 Transform 不依赖路由配置数据，无需校验。
    /// </summary>
    public void ValidateRoute(TransformRouteValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
    }

    /// <summary>
    /// 集群级校验钩子。本 Transform 不依赖集群配置数据，无需校验。
    /// </summary>
    public void ValidateCluster(TransformClusterValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
    }

    /// <summary>
    /// 为每条路由注册 <see cref="ApplyAsync"/> 作为请求 Transform。
    /// YARP 在构建每条路由时调用此方法。
    /// </summary>
    public void Apply(TransformBuilderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.AddRequestTransform(ApplyAsync);
    }
}
