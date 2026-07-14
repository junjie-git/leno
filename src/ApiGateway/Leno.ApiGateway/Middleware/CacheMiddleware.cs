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
/// 缓存 Key：<c>method:path:querystring:userId</c>（按用户隔离）。
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

        await _next(context);

        // 恢复原始 Body 流
        context.Response.Body = originalBodyStream;
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
    /// 判断响应是否可缓存：状态码 200 且无 <c>Cache-Control: no-store</c> 指令。
    /// </summary>
    internal static bool IsCacheableResponse(HttpResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (response.StatusCode != 200)
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
    /// 生成缓存 Key：<c>method:path:querystring:userId</c>。
    /// userId 从 Claims 的 <c>Sub</c> 读取，匿名用户为空字符串。
    /// </summary>
    internal static string GenerateCacheKey(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var method = context.Request.Method;
        var path = context.Request.Path.Value ?? "/";
        var query = context.Request.QueryString.Value ?? string.Empty;
        var userId = context.User.FindFirst("Sub")?.Value ?? string.Empty;

        return $"{method}:{path}{query}:{userId}";
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
