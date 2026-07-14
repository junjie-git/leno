using System.Diagnostics;
using Leno.ApiGateway.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Yarp.ReverseProxy.Model;

namespace Leno.ApiGateway.Middleware;

/// <summary>
/// 统一访问日志中间件。
/// <para>
/// 在请求管道中包装 <c>next</c>，捕获请求进入与响应返回的元数据，
/// 构造 <see cref="AccessLogEntry"/> 后通过 Serilog 输出结构化 JSON 日志。
/// 字段符合 Spec 6.2 定义：timestamp/traceId/method/path/statusCode/duration/clientIp/userId/targetService/userAgent。
/// </para>
/// </summary>
public sealed class AccessLoggingMiddleware
{
    private const string UserIdItemsKey = "UserId";
    private const string UserIdHeader = "X-User-Id";
    private const string ForwardedForHeader = "X-Forwarded-For";

    private readonly RequestDelegate _next;
    private readonly ILogger<AccessLoggingMiddleware> _logger;

    public AccessLoggingMiddleware(RequestDelegate next, ILogger<AccessLoggingMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var stopwatch = Stopwatch.StartNew();
        var timestamp = DateTimeOffset.UtcNow;
        var method = context.Request.Method;
        var path = context.Request.Path.Value ?? string.Empty;
        var userAgent = context.Request.Headers.UserAgent.ToString();
        var clientIp = ResolveClientIp(context);
        var traceId = Activity.Current?.TraceId.ToString();

        int statusCode;
        try
        {
            await _next(context);
            statusCode = context.Response.StatusCode;
        }
        catch
        {
            // 下游抛异常时仍记录访问日志（状态码记为 500），异常继续向上抛出
            stopwatch.Stop();
            LogAccess(timestamp, traceId, method, path, 500, stopwatch.ElapsedMilliseconds,
                clientIp, ResolveUserId(context), ResolveTargetService(context), userAgent);
            throw;
        }

        stopwatch.Stop();
        LogAccess(timestamp, traceId, method, path, statusCode, stopwatch.ElapsedMilliseconds,
            clientIp, ResolveUserId(context), ResolveTargetService(context), userAgent);
    }

    private void LogAccess(
        DateTimeOffset timestamp,
        string? traceId,
        string method,
        string path,
        int statusCode,
        long duration,
        string? clientIp,
        string? userId,
        string? targetService,
        string? userAgent)
    {
        var entry = new AccessLogEntry
        {
            Timestamp = timestamp,
            TraceId = traceId,
            Method = method,
            Path = path,
            StatusCode = statusCode,
            Duration = duration,
            ClientIp = clientIp,
            UserId = userId,
            TargetService = targetService,
            UserAgent = userAgent
        };

        _logger.LogInformation("{@AccessLog}", entry);
    }

    private static string? ResolveClientIp(HttpContext context)
    {
        var forwardedFor = context.Request.Headers[ForwardedForHeader].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwardedFor))
        {
            // X-Forwarded-For 可能是 "client, proxy1, proxy2" 形式，取第一个
            var first = forwardedFor.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (first.Length > 0)
            {
                return first[0];
            }
        }

        return context.Connection.RemoteIpAddress?.ToString();
    }

    private static string? ResolveUserId(HttpContext context)
    {
        // 优先：HttpContext.Items["UserId"]（由 JwtAuthMiddleware 写入，阶段二实现）
        if (context.Items.TryGetValue(UserIdItemsKey, out var itemValue) && itemValue is string itemUserId)
        {
            return itemUserId;
        }

        // 兜底：直接读 X-User-Id 头（由 YARP UserContextTransform 注入）
        var headerValue = context.Request.Headers[UserIdHeader].FirstOrDefault();
        return string.IsNullOrWhiteSpace(headerValue) ? null : headerValue;
    }

    private static string? ResolveTargetService(HttpContext context)
    {
        // YARP 的 IReverseProxyFeature 在 YARP 管道执行后才会填充，
        // 此处容错读取——未路由到 YARP 时返回 null。
        // YARP 2.2.0 中 IReverseProxyFeature.Cluster 类型为 ClusterModel，其 Config 属性即 ClusterConfig。
        var feature = context.Features.Get<IReverseProxyFeature>();
        return feature?.Cluster?.Config?.ClusterId;
    }
}
