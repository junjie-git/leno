using Leno.SharedContracts.Events;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Leno.Infrastructure.EventBus;

/// <summary>
/// 基于 Redis 的集成事件消费者基类，使用 SET NX 实现幂等去重。
/// 事件处理完成后写入 Redis key（TTL 24 小时），重复事件直接跳过。
/// </summary>
/// <typeparam name="T">集成事件类型。</typeparam>
public abstract class RedisIntegrationEventConsumerBase<T> : IntegrationEventConsumerBase<T>
    where T : class, IIntegrationEvent
{
    private static readonly TimeSpan KeyTtl = TimeSpan.FromHours(24);

    private readonly IConnectionMultiplexer _redisMultiplexer;

    /// <summary>Redis 幂等去重 key 前缀。</summary>
    protected virtual string IdempotencyKeyPrefix => "evt:processed";

    protected RedisIntegrationEventConsumerBase(
        ILogger logger,
        IConnectionMultiplexer redisMultiplexer)
        : base(logger)
    {
        ArgumentNullException.ThrowIfNull(redisMultiplexer);
        _redisMultiplexer = redisMultiplexer;
    }

    /// <inheritdoc />
    protected override async Task<bool> IsProcessedAsync(Guid eventId, CancellationToken ct)
    {
        var db = _redisMultiplexer.GetDatabase();
        var key = BuildKey(eventId);
        var exists = await db.KeyExistsAsync(key);
        return exists;
    }

    /// <inheritdoc />
    protected override async Task MarkAsProcessedAsync(Guid eventId, CancellationToken ct)
    {
        var db = _redisMultiplexer.GetDatabase();
        var key = BuildKey(eventId);
        await db.StringSetAsync(key, "1", KeyTtl);
    }

    private string BuildKey(Guid eventId)
        => $"{IdempotencyKeyPrefix}:{eventId}";
}
