using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Leno.ApiGateway.Services;

/// <summary>
/// 基于 Redis 的 JWT 黑名单实现。
/// Key 格式：leno:jwt:blacklist:{jti}，Value：1，TTL = token 剩余有效期。
/// 三层保障：
/// 1. Redis Pub/Sub 实时同步：RevokeAsync 后 Publish 通知所有网关实例更新本地缓存；
/// 2. 本地 MemoryCache 缓存：与 token TTL 对齐的过期时间，避免内存泄漏；
/// 3. 启动预热：StartAsync 时订阅 Pub/Sub 通道。
/// </summary>
public sealed class JwtBlacklistService : IJwtBlacklistService, IHostedService, IDisposable
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IMemoryCache _localCache;
    private readonly ILogger<JwtBlacklistService> _logger;
    private ISubscriber? _subscriber;

    /// <summary>Redis Pub/Sub 通道名，用于黑名单失效通知。</summary>
    public const string InvalidationChannel = "leno:jwt:blacklist:invalidate";

    /// <summary>本地缓存 key 前缀。</summary>
    private const string LocalCachePrefix = "jwt_bl:";

    /// <summary>Redis 黑名单 key 前缀。</summary>
    private const string RedisKeyPrefix = "leno:jwt:blacklist:";

    /// <summary>本地缓存兜底 TTL：Redis key 无 TTL 时使用。</summary>
    private static readonly TimeSpan FallbackCacheTtl = TimeSpan.FromMinutes(5);

    public JwtBlacklistService(
        IConnectionMultiplexer redis,
        IMemoryCache localCache,
        ILogger<JwtBlacklistService> logger)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _localCache = localCache ?? throw new ArgumentNullException(nameof(localCache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> IsRevokedAsync(string jti, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jti);

        var localKey = LocalCachePrefix + jti;
        // 第一层：本地 MemoryCache（有过期时间，不会泄漏）
        if (_localCache.TryGetValue(localKey, out bool cachedRevoked) && cachedRevoked)
        {
            return true;
        }

        // 第二层：Redis 查询
        var redisKey = RedisKeyPrefix + jti;
        var db = _redis.GetDatabase();
        var exists = await db.KeyExistsAsync(redisKey);
        if (exists)
        {
            // 回填本地缓存，TTL 与 Redis key 剩余时间对齐
            var ttl = await db.KeyTimeToLiveAsync(redisKey);
            var cacheTtl = ttl ?? FallbackCacheTtl;
            _localCache.Set(localKey, true, cacheTtl);
            return true;
        }
        return false;
    }

    public async Task RevokeAsync(string jti, TimeSpan ttl, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jti);

        var redisKey = RedisKeyPrefix + jti;
        var db = _redis.GetDatabase();
        await db.StringSetAsync(redisKey, "1", ttl);

        // 本地缓存同步
        _localCache.Set(LocalCachePrefix + jti, true, ttl);

        // Pub/Sub 通知所有网关实例
        var subscriber = _redis.GetSubscriber();
        var notification = JsonSerializer.Serialize(new
        {
            Jti = jti,
            TtlSeconds = (long)ttl.TotalSeconds
        });
        await subscriber.PublishAsync(RedisChannel.Literal(InvalidationChannel), notification);

        _logger.LogInformation("JWT 已吊销 Jti={Jti} Ttl={Ttl}分钟", jti, ttl.TotalMinutes);
    }

    /// <summary>
    /// 启动时订阅 Pub/Sub 通道，接收其他网关实例的黑名单失效通知。
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _subscriber = _redis.GetSubscriber();
        _subscriber.SubscribeAsync(
            RedisChannel.Literal(InvalidationChannel),
            (channel, message) => HandleInvalidationMessage(channel, message));

        _logger.LogInformation("JWT 黑名单 Pub/Sub 订阅已启动 Channel={Channel}", InvalidationChannel);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 处理 Pub/Sub 黑名单失效消息，更新本地缓存。
    /// </summary>
    internal void HandleInvalidationMessage(RedisChannel channel, RedisValue message)
    {
        try
        {
            if (!message.HasValue) return;

            var evt = JsonSerializer.Deserialize<BlacklistInvalidationPayload>(message.ToString());
            if (evt is null || string.IsNullOrEmpty(evt.Jti)) return;

            var ttl = evt.TtlSeconds > 0
                ? TimeSpan.FromSeconds(evt.TtlSeconds)
                : FallbackCacheTtl;
            _localCache.Set(LocalCachePrefix + evt.Jti, true, ttl);

            _logger.LogDebug("收到黑名单失效通知，已更新本地缓存 Jti={Jti}", evt.Jti);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理黑名单失效通知失败 Message={Message}", message);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _subscriber?.Unsubscribe(RedisChannel.Literal(InvalidationChannel));
        _logger.LogInformation("JWT 黑名单 Pub/Sub 订阅已停止");
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _subscriber?.Unsubscribe(RedisChannel.Literal(InvalidationChannel));
    }

    /// <summary>Pub/Sub 消息反序列化 DTO（PascalCase 属性名，与发布端一致）。</summary>
    private sealed class BlacklistInvalidationPayload
    {
        public string Jti { get; set; } = string.Empty;
        public long TtlSeconds { get; set; }
    }
}
