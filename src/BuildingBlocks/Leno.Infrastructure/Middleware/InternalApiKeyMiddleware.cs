using System.Text.Json;
using Leno.Infrastructure.Auth;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leno.Infrastructure.Middleware;

/// <summary>
/// 内部服务间鉴权中间件，校验 internal/ 前缀路由的 X-Internal-Key 请求头。
/// </summary>
public sealed class InternalApiKeyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<InternalApiKeyMiddleware> _logger;
    private readonly InternalApiKeyOptions _options;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public InternalApiKeyMiddleware(
        RequestDelegate next,
        ILogger<InternalApiKeyMiddleware> logger,
        IOptions<InternalApiKeyOptions> options)
    {
        _next = next;
        _logger = logger;
        _options = options.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        var prefix = "/" + _options.RoutePrefix;

        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        if (string.IsNullOrEmpty(_options.ApiKey))
        {
            _logger.LogWarning("内部鉴权密钥未配置，跳过校验 Path={Path}", path);
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue("X-Internal-Key", out var providedKey) ||
            !string.Equals(providedKey, _options.ApiKey, StringComparison.Ordinal))
        {
            _logger.LogWarning("内部鉴权失败 Path={Path}", path);
            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json; charset=utf-8";
            var response = ApiResponse.Fail(StatusCodes.Status401Unauthorized, "内部服务鉴权失败");
            var json = JsonSerializer.Serialize(response, response.GetType(), JsonOptions);
            await context.Response.WriteAsync(json);
            return;
        }

        await _next(context);
    }
}
