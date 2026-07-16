using Leno.Infrastructure.Abstractions;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Leno.Infrastructure.EventBus;

/// <summary>
/// 基于 Redis 的幂等去重存储，使用 SET NX + 24h TTL 实现集成事件消费幂等。
/// 同一 EventId 的事件重复投递时，仅第一次执行业务逻辑，后续直接跳过。
/// </summary>
public sealed class RedisIdempotencyStore : IIdempotencyStore
{
    /// <summary>幂等 key 默认 TTL：24 小时，覆盖大多数业务重试窗口。</summary>
    public static readonly TimeSpan DefaultTtl = TimeSpan.FromHours(24);

    /// <summary>幂等 key 默认前缀，可被业务覆盖。</summary>
    public const string DefaultKeyPrefix = "evt:processed";

    private static readonly TimeSpan KeyTtl = DefaultTtl;

    private readonly IConnectionMultiplexer _redisMultiplexer;
    private readonly ILogger<RedisIdempotencyStore>? _logger;

    /// <summary>Redis 幂等去重 key 前缀。</summary>
    public string KeyPrefix { get; } = DefaultKeyPrefix;

    public RedisIdempotencyStore(
        IConnectionMultiplexer redisMultiplexer,
        ILogger<RedisIdempotencyStore>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(redisMultiplexer);
        _redisMultiplexer = redisMultiplexer;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<bool> IsProcessedAsync(Guid eventId, CancellationToken ct = default)
    {
        var db = _redisMultiplexer.GetDatabase();
        var key = BuildKey(eventId);
        var exists = await db.KeyExistsAsync(key);
        return exists;
    }

    /// <inheritdoc />
    public async Task MarkAsProcessedAsync(Guid eventId, CancellationToken ct = default)
    {
        var db = _redisMultiplexer.GetDatabase();
        var key = BuildKey(eventId);
        // SET NX：仅当 key 不存在时设置，避免覆盖既有 TTL
        await db.StringSetAsync(key, "1", KeyTtl, when: When.NotExists);
    }

    private string BuildKey(Guid eventId) => $"{KeyPrefix}:{eventId}";
}
