using System.Text.Json;
using Leno.ApiGateway.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Leno.ApiGateway.Middleware;

/// <summary>
/// 响应缓存中间件。位于 JWT 验签之后、YARP 代理之前。
/// <para>
/// 缓存条件：仅 GET/HEAD 方法，响应状态码 200 且无 <c>Cache-Control: no-store</c>。
/// 缓存 Key：<c>method:path:querystring:userId:role:shopId</c>（按用户、角色、店铺隔离，避免越权命中）。
/// 命中缓存时直接返回缓存的响应体与 Header，不转发到后端。
/// 缓存存储于 Redis，TTL 由 <see cref="CacheOptions.GetTtlForPath"/> 决定。
/// </para>
/// </summary>
public sealed class CacheMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IDatabase _redis;
    private readonly CacheOptions _options;

    /// <summary>Redis Key 前缀，避免与其他业务 Key 冲突。</summary>
    internal const string KeyPrefix = "leno:cache:";

    private static readonly HashSet<string> CacheableMethods =
        new(StringComparer.OrdinalIgnoreCase) { "GET", "HEAD" };

    /// <summary>
    /// T17：可缓存响应状态码集合。
    /// 包含 200(OK)、203(Non-Authoritative)、204(No Content)、206(Partial Content)、
    /// 300(Multiple Choices)、301(Moved Permanently)、
    /// 405(Method Not Allowed)、410(Gone)、414(URI Too Long)、501(Not Implemented)。
    /// 参照 RFC 7234 §5.2.2 与 Nginx proxy_cache_valid 常见配置。
    /// <para>
    /// 注意：404(Not Found) 负缓存因既有测试 <c>IsCacheableResponse_With404_ReturnsFalse</c>
    /// 断言 404 不可缓存而暂未包含，标记为 [SKIPPED-CONFLICT]。
    /// </para>
    /// </summary>
    internal static readonly HashSet<int> CacheableStatusCodes = new()
    {
        200, 203, 204, 206, 300, 301, 405, 410, 414, 501
    };

    public CacheMiddleware(
        RequestDelegate next,
        IConnectionMultiplexer redis,
        IOptions<CacheOptions> options)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        ArgumentNullException.ThrowIfNull(redis);
        _redis = redis.GetDatabase();
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!_options.Enabled || !IsCacheableRequest(context.Request))
        {
            await _next(context);
            return;
        }

        var cacheKey = GenerateCacheKey(context);
        var redisKey = KeyPrefix + cacheKey;

        // 尝试命中缓存
        var cached = await _redis.StringGetAsync(redisKey);
        if (cached.HasValue)
        {
            await WriteCachedResponseAsync(context, cached.ToString());
            return;
        }

        // 缓存未命中：替换 Response.Body 捕获响应，转发到 YARP
        var originalBodyStream = context.Response.Body;
        using var memoryStream = new MemoryStream();
        context.Response.Body = memoryStream;

        try
        {
            await _next(context);
        }
        finally
        {
            // 无论 _next 成功或抛异常，都必须恢复原始 Body 流。
            // 否则异常传播到上层中间件时，Response.Body 仍指向 memoryStream，
            // 上层错误处理中间件写入错误的流，导致响应损坏或连接挂起。
            context.Response.Body = originalBodyStream;
        }

        memoryStream.Seek(0, SeekOrigin.Begin);
        var responseBytes = memoryStream.ToArray();

        // 若响应可缓存，写入 Redis
        if (IsCacheableResponse(context.Response))
        {
            var ttl = _options.GetTtlForPath(context.Request.Path.Value ?? "/");
            var serialized = SerializeResponse(
                context.Response.StatusCode, context.Response.Headers, responseBytes);
            await _redis.StringSetAsync(redisKey, serialized, ttl);
        }

        // 将捕获的响应写回客户端
        memoryStream.Seek(0, SeekOrigin.Begin);
        await memoryStream.CopyToAsync(originalBodyStream);
    }

    /// <summary>
    /// 判断请求是否可缓存：仅 GET/HEAD 方法。
    /// </summary>
    internal static bool IsCacheableRequest(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return CacheableMethods.Contains(request.Method);
    }

    /// <summary>
    /// 判断响应是否可缓存：状态码在 <see cref="CacheableStatusCodes"/> 集合中且无 <c>Cache-Control: no-store</c> 指令。
    /// <para>
    /// T17：扩展可缓存状态码，除 200 外还包括 203/204/206/300/301（重定向）、
    /// 405/410/414/501，提升缓存命中率。
    /// </para>
    /// </summary>
    internal static bool IsCacheableResponse(HttpResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (!CacheableStatusCodes.Contains(response.StatusCode))
        {
            return false;
        }

        if (response.Headers.TryGetValue("Cache-Control", out var cc)
            && cc.ToString().Contains("no-store", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// 生成缓存 Key：<c>method:path:querystring:userId:role:shopId</c>。
    /// userId/role/shopId 分别从 Claims 的 <c>Sub</c>、<c>Role</c>、<c>shop_id</c> 读取，
    /// 与 <see cref="Leno.ApiGateway.Transforms.UserContextTransformProvider"/> 保持一致。
    /// 匿名用户使用占位值 <c>anonymous</c>/<c>guest</c>/<c>none</c>，
    /// 确保不同身份维度的请求不会命中同一缓存条目（防越权）。
    /// </summary>
    internal static string GenerateCacheKey(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var method = context.Request.Method;
        var path = context.Request.Path.Value ?? "/";
        var query = context.Request.QueryString.Value ?? string.Empty;

        var userId = context.User.FindFirst("Sub")?.Value ?? "anonymous";
        var role = context.User.FindFirst("Role")?.Value ?? "guest";
        var shopId = context.User.FindFirst("shop_id")?.Value ?? "none";

        return $"{method}:{path}{query}:{userId}:{role}:{shopId}";
    }

    private static string SerializeResponse(
        int statusCode, IHeaderDictionary headers, byte[] body)
    {
        // 排除 Transfer-Encoding / Content-Length，写入时由框架重新计算
        var headerDict = headers
            .Where(h => !string.Equals(h.Key, "Transfer-Encoding", StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(h.Key, "Content-Length", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(h => h.Key, h => h.Value.ToArray());

        var cached = new CachedResponse
        {
            StatusCode = statusCode,
            Headers = headerDict,
            Body = body
        };

        return JsonSerializer.Serialize(cached);
    }

    private static async Task WriteCachedResponseAsync(HttpContext context, string cachedJson)
    {
        var cached = JsonSerializer.Deserialize<CachedResponse>(cachedJson);
        if (cached is null)
        {
            // 反序列化失败，回退到正常转发
            return;
        }

        context.Response.StatusCode = cached.StatusCode;

        foreach (var (key, values) in cached.Headers)
        {
            context.Response.Headers[key] = values;
        }

        if (cached.Body.Length > 0)
        {
            await context.Response.Body.WriteAsync(cached.Body);
        }
    }

    private sealed class CachedResponse
    {
        public int StatusCode { get; set; }
        public Dictionary<string, string?[]> Headers { get; set; } = new();
        public byte[] Body { get; set; } = Array.Empty<byte>();
    }
}
