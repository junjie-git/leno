using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Leno.Infrastructure.Caching;

/// <summary>
/// 基于 Redis Pub/Sub 的缓存失效订阅者，作为 <see cref="IHostedService"/> 后台运行。
/// <para>
/// 应用启动时订阅 <see cref="MultiLevelCacheOptions.InvalidationChannel"/> 通道，
/// 收到失效消息时反序列化出 Key，调用 <see cref="IMemoryCache.Remove"/> 清除本地 L1 中对应的 Key。
/// 仅清进程内 L1，不影响 L2 Redis（L2 由发布端 <see cref="IMultiLevelCache.RemoveAsync"/> 已删除）。
/// </para>
/// <para>
/// 异常处理：消息反序列化失败仅记日志不抛出，避免单条坏消息中断订阅通道。
/// L1 短 TTL（默认 5s）兜底 Pub/Sub 消息丢失场景。
/// </para>
/// </summary>
public sealed class CacheInvalidationSubscriber : ICacheInvalidationSubscriber, IHostedService, IDisposable
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IMemoryCache _l1;
    private readonly MultiLevelCacheOptions _options;
    private readonly ILogger<CacheInvalidationSubscriber> _logger;
    private ISubscriber? _subscriber;
    private bool _disposed;

    public CacheInvalidationSubscriber(
        IConnectionMultiplexer redis,
        IMemoryCache l1,
        IOptions<MultiLevelCacheOptions> options,
        ILogger<CacheInvalidationSubscriber> logger)
    {
        ArgumentNullException.ThrowIfNull(redis);
        ArgumentNullException.ThrowIfNull(l1);
        ArgumentNullException.ThrowIfNull(options);
        _redis = redis;
        _l1 = l1;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _subscriber = _redis.GetSubscriber();
        _subscriber.Subscribe(
            RedisChannel.Literal(_options.InvalidationChannel),
            (channel, message) => HandleInvalidationMessage(channel, message));

        _logger.LogInformation(
            "缓存失效 Pub/Sub 订阅已启动 Channel={Channel}",
            _options.InvalidationChannel);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        if (_subscriber is not null)
        {
            _subscriber.Unsubscribe(RedisChannel.Literal(_options.InvalidationChannel));
            _logger.LogInformation(
                "缓存失效 Pub/Sub 订阅已停止 Channel={Channel}",
                _options.InvalidationChannel);
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// 处理 Pub/Sub 失效消息：反序列化 Key 并清除本地 L1。
    /// <para>
    /// internal 便于单元测试直接调用验证消息处理逻辑。
    /// </para>
    /// </summary>
    internal void HandleInvalidationMessage(RedisChannel channel, RedisValue message)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            if (!message.HasValue)
            {
                return;
            }

            var payload = JsonSerializer.Deserialize<CacheInvalidationPublisher.CacheInvalidationPayload>(message.ToString());
            if (payload is null || string.IsNullOrEmpty(payload.Key))
            {
                _logger.LogWarning("收到无效的缓存失效消息: Message={Message}", message);
                return;
            }

            _l1.Remove(payload.Key);
            _logger.LogDebug(
                "收到缓存失效通知，已清除本地 L1: Key={Key}, Origin={Origin}",
                payload.Key, payload.Origin);
        }
        catch (Exception ex)
        {
            // 单条消息处理失败不中断订阅通道，记录错误后继续等待下一条消息。
            _logger.LogError(ex, "处理缓存失效通知失败 Message={Message}", message);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _subscriber?.Unsubscribe(RedisChannel.Literal(_options.InvalidationChannel));
    }
}
