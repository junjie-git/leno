using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Leno.Infrastructure.Caching;

/// <summary>
/// 基于 Redis Pub/Sub 的缓存失效通知发布者。
/// <para>
/// 通过 <see cref="IConnectionMultiplexer"/> 获取 <see cref="ISubscriber"/>，
/// 将失效 Key 序列化为 JSON 后发布到 <see cref="MultiLevelCacheOptions.InvalidationChannel"/> 通道。
/// 所有订阅该通道的实例（<see cref="CacheInvalidationSubscriber"/>）收到消息后清本地 L1。
/// </para>
/// <para>
/// 消息格式（JSON）：
/// <code>
/// { "key": "product:spu:123", "origin": "instance-abc" }
/// </code>
/// <c>origin</c> 字段为发布实例的唯一标识，订阅端可用于调试与监控（当前不用于过滤，
/// 因为发布实例本身已在 <see cref="IMultiLevelCache.RemoveAsync"/> 中清过本地 L1）。
/// </para>
/// </summary>
public sealed class CacheInvalidationPublisher : ICacheInvalidationPublisher
{
    private readonly IConnectionMultiplexer _redis;
    private readonly MultiLevelCacheOptions _options;
    private readonly ILogger<CacheInvalidationPublisher> _logger;
    private readonly string _origin;

    public CacheInvalidationPublisher(
        IConnectionMultiplexer redis,
        IOptions<MultiLevelCacheOptions> options,
        ILogger<CacheInvalidationPublisher> logger)
    {
        ArgumentNullException.ThrowIfNull(redis);
        ArgumentNullException.ThrowIfNull(options);
        _redis = redis;
        _options = options.Value;
        _logger = logger;
        // origin 用于日志与监控：机器名 + 进程 ID，便于排查跨实例失效链路
        _origin = $"{Environment.MachineName}:{Environment.ProcessId}";
    }

    /// <inheritdoc />
    public async Task PublishInvalidationAsync(string key, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var payload = new CacheInvalidationPayload(key, _origin);
        var message = JsonSerializer.Serialize(payload);
        var subscriber = _redis.GetSubscriber();

        await subscriber.PublishAsync(
            RedisChannel.Literal(_options.InvalidationChannel),
            message).ConfigureAwait(false);

        _logger.LogDebug(
            "缓存失效通知已发布: Key={Key}, Channel={Channel}, Origin={Origin}",
            key, _options.InvalidationChannel, _origin);
    }

    /// <summary>Pub/Sub 失效消息载荷。</summary>
    public sealed class CacheInvalidationPayload
    {
        /// <summary>失效的缓存键。</summary>
        public string Key { get; init; }

        /// <summary>发布实例标识（机器名:进程ID），用于日志与监控。</summary>
        public string Origin { get; init; }

        public CacheInvalidationPayload(string key, string origin)
        {
            Key = key;
            Origin = origin;
        }
    }
}
