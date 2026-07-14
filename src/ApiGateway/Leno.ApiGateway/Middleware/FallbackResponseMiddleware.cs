using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace Leno.ApiGateway.Middleware;

/// <summary>
/// 熔断降级响应中间件。
/// <para>
/// YARP <c>CircuitBreaker</c> 触发时返回 503 空响应体。本中间件位于 <c>MapReverseProxy</c> 之前，
/// 通过响应体缓冲检测 503 状态码并改写为统一降级 JSON：
/// <code>
/// { "code": 503, "message": "服务暂时不可用，请稍后重试", "data": null }
/// </code>
/// </para>
/// 仅对反向代理转发的请求生效（通过 <c>X-Forwarded-By</c> 标记或非 <c>/health</c> 路径区分）。
/// </summary>
public sealed class FallbackResponseMiddleware
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly byte[] FallbackBody = Encoding.UTF8.GetBytes(
        JsonSerializer.Serialize(new
        {
            code = 503,
            message = "服务暂时不可用，请稍后重试",
            data = (object?)null
        }, SerializerOptions));

    private const string FallbackContentType = "application/json; charset=utf-8";

    private readonly RequestDelegate _next;
    private readonly ILogger<FallbackResponseMiddleware> _logger;

    public FallbackResponseMiddleware(
        RequestDelegate next,
        ILogger<FallbackResponseMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // 健康检查端点不参与降级（避免影响 K8s/Consul 探针）
        if (IsHealthEndpoint(context.Request.Path))
        {
            await _next(context);
            return;
        }

        // 缓冲响应体以便后续重写
        var originalBodyStream = context.Response.Body;
        using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        try
        {
            await _next(context);
        }
        finally
        {
            // 恢复原始响应流
            context.Response.Body = originalBodyStream;
        }

        if (context.Response.StatusCode == StatusCodes.Status503ServiceUnavailable)
        {
            await RewriteAsFallbackAsync(context, responseBody);
        }
        else
        {
            // 复制原始响应体回真实流
            responseBody.Seek(0, SeekOrigin.Begin);
            await responseBody.CopyToAsync(originalBodyStream);
        }
    }

    private async Task RewriteAsFallbackAsync(HttpContext context, MemoryStream responseBody)
    {
        _logger.LogWarning(
            "Returning fallback response for {Method} {Path} (origin: {StatusCode})",
            context.Request.Method,
            context.Request.Path,
            context.Response.StatusCode);

        // 清除原始 headers 中可能与 body 不一致的字段
        context.Response.ContentType = FallbackContentType;
        context.Response.ContentLength = FallbackBody.Length;

        // 清空缓冲区并写入降级 JSON
        responseBody.SetLength(0);
        await responseBody.WriteAsync(FallbackBody);

        responseBody.Seek(0, SeekOrigin.Begin);
        await responseBody.CopyToAsync(context.Response.Body);
    }

    private static bool IsHealthEndpoint(PathString path)
    {
        return path.StartsWithSegments("/health");
    }
}
