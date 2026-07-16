using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Leno.Infrastructure.Auth;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leno.Infrastructure.Middleware;

/// <summary>
/// 内部服务间鉴权中间件，校验 internal/ 前缀路由的 X-Internal-Key 请求头。
/// </summary>
/// <remarks>
/// 安全策略：
/// <list type="bullet">
/// <item>路由边界精确匹配：<c>/internal</c> 或 <c>/internal/...</c> 才视为内部路由，避免 <c>/internalinfo</c> 误判。</item>
/// <item>ApiKey 比较使用 <see cref="CryptographicOperations.FixedTimeEquals"/>，防止计时侧信道。</item>
/// <item>ApiKey 未配置时 fail-closed：生产/Staging 等环境返回 500 拒绝请求；Development 放行便于本地开发。</item>
/// </list>
/// 运行时兜底之外，仍建议在各 BC 的 Program.cs 启动时调用 <c>app.EnsureInternalApiKeyConfigured()</c> 做启动校验。
/// </remarks>
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

    public async Task InvokeAsync(HttpContext context, IHostEnvironment hostEnvironment)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        var prefix = NormalizePrefix(_options.RoutePrefix);

        if (!IsInternalPath(path, prefix))
        {
            await _next(context);
            return;
        }

        if (string.IsNullOrEmpty(_options.ApiKey))
        {
            if (hostEnvironment.IsDevelopment())
            {
                _logger.LogWarning("内部鉴权密钥未配置，开发环境跳过校验 Path={Path}", path);
                await _next(context);
                return;
            }

            _logger.LogCritical("生产环境未配置 InternalAuth:ApiKey，拒绝请求 Path={Path}", path);
            await WriteJsonAsync(context.Response, StatusCodes.Status500InternalServerError, "内部服务鉴权未配置");
            return;
        }

        if (!context.Request.Headers.TryGetValue("X-Internal-Key", out var providedKey) ||
            !FixedTimeEqualsKey(providedKey, _options.ApiKey))
        {
            _logger.LogWarning("内部鉴权失败 Path={Path}", path);
            await WriteJsonAsync(context.Response, StatusCodes.Status401Unauthorized, "内部服务鉴权失败");
            return;
        }

        await _next(context);
    }

    private static string NormalizePrefix(string routePrefix)
    {
        var trimmed = (routePrefix ?? string.Empty).Trim('/');
        return trimmed.Length == 0 ? string.Empty : "/" + trimmed;
    }

    private static bool IsInternalPath(string path, string prefix)
    {
        if (prefix.Length == 0)
        {
            return false;
        }

        return path.Equals(prefix, StringComparison.OrdinalIgnoreCase) ||
               path.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool FixedTimeEqualsKey(string? provided, string expected)
    {
        if (string.IsNullOrEmpty(provided))
        {
            return false;
        }

        var providedBytes = Encoding.UTF8.GetBytes(provided);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        return CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes);
    }

    private static async Task WriteJsonAsync(HttpResponse response, int statusCode, string message)
    {
        response.Clear();
        response.StatusCode = statusCode;
        response.ContentType = "application/json; charset=utf-8";
        var apiResponse = ApiResponse.Fail(statusCode, message);
        var json = JsonSerializer.Serialize(apiResponse, apiResponse.GetType(), JsonOptions);
        await response.WriteAsync(json);
    }
}
