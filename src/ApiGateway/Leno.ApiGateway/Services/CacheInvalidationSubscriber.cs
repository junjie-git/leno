using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Leno.ApiGateway.Services;

/// <summary>
/// 缓存失效事件格式，由后端服务通过 Redis Pub/Sub 发布。
/// </summary>
public sealed record CacheInvalidatedEvent
{
    [JsonPropertyName("eventType")]
    public string EventType { get; init; } = "CacheInvalidated";

    /// <summary>精确失效的缓存 Key（不含前缀，如 <c>GET:/api/products/123::42</c>）。</summary>
    [JsonPropertyName("cacheKey")]
    public string? CacheKey { get; init; }

    /// <summary>Glob 模式批量失效（如 <c>/api/product/sku/123*</c>），匹配的 Key 全部删除。</summary>
    [JsonPropertyName("pattern")]
    public string? Pattern { get; init; }
}

/// <summary>
/// 订阅 Redis Pub/Sub <c>leno:cache:invalidated</c> 通道，收到缓存失效事件后删除对应缓存。
/// <para>
/// 失效逻辑：
/// <list type="bullet">
/// <item><see cref="CacheInvalidatedEvent.CacheKey"/> 非空：直接删除 <c>leno:cache:{cacheKey}</c></item>
/// <item><see cref="CacheInvalidatedEvent.Pattern"/> 非空：用 SCAN 遍历匹配 <c>leno:cache:{pattern}</c> 的 Key 并删除</item>
/// </list>
/// </para>
/// </summary>
public sealed class CacheInvalidationSubscriber : IHostedService, IDisposable
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<CacheInvalidationSubscriber> _logger;
    private ISubscriber? _subscriber;

    /// <summary>Redis Pub/Sub 通道名。</summary>
    public const string ChannelName = "leno:cache:invalidated";

    /// <summary>缓存 Key 前缀，需与 <see cref="Middleware.CacheMiddleware.KeyPrefix"/> 一致。</summary>
    private const string KeyPrefix = "leno:cache:";

    public CacheInvalidationSubscriber(
        IConnectionMultiplexer redis,
        ILogger<CacheInvalidationSubscriber> logger)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _subscriber = _redis.GetSubscriber();
            _subscriber.Subscribe(RedisChannel.Literal(ChannelName), OnMessage);
            _logger.LogInformation(
                "Subscribed to cache invalidation channel {Channel}", ChannelName);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // 订阅失败不阻断启动，由健康检查兜底
            _logger.LogError(ex,
                "Failed to subscribe to cache invalidation channel {Channel}", ChannelName);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Redis 消息回调。签名要求 <c>async void</c>，内部 try-catch 防止未观察异常。
    /// </summary>
    private async void OnMessage(RedisChannel channel, RedisValue message)
    {
        try
        {
            if (!message.HasValue)
            {
                return;
            }

            var evt = JsonSerializer.Deserialize<CacheInvalidatedEvent>(message.ToString());
            if (evt is null)
            {
                return;
            }

            var db = _redis.GetDatabase();

            if (!string.IsNullOrEmpty(evt.CacheKey))
            {
                var fullKey = KeyPrefix + evt.CacheKey;
                await db.KeyDeleteAsync(fullKey);
                _logger.LogDebug("Invalidated cache key {Key}", evt.CacheKey);
            }

            if (!string.IsNullOrEmpty(evt.Pattern))
            {
                await InvalidatePatternAsync(db, evt.Pattern);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process cache invalidation message: {Message}", message);
        }
    }

    private async Task InvalidatePatternAsync(IDatabase db, string pattern)
    {
        var servers = _redis.GetServers();
        var server = servers.FirstOrDefault(s => !s.IsReplica);
        if (server is null)
        {
            _logger.LogWarning("No primary Redis server available for pattern invalidation");
            return;
        }

        var fullPattern = KeyPrefix + pattern;
        var deleted = 0;

        await foreach (var key in server.KeysAsync(pattern: fullPattern))
        {
            await db.KeyDeleteAsync(key);
            deleted++;
        }

        _logger.LogInformation(
            "Invalidated {Count} cache keys matching pattern {Pattern}", deleted, pattern);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        if (_subscriber is not null)
        {
            try
            {
                _subscriber.UnsubscribeAll();
                _logger.LogInformation("Unsubscribed from cache invalidation channel");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Failed to unsubscribe from cache invalidation channel");
            }
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        try
        {
            _subscriber?.UnsubscribeAll();
        }
        catch
        {
            // 忽略 dispose 异常
        }
    }
}
