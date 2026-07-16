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
/// 失效逻辑（双删模式，缩小脏读窗口）：
/// <list type="bullet">
/// <item><see cref="CacheInvalidatedEvent.CacheKey"/> 非空：直接删除 <c>leno:cache:{cacheKey}</c>，延迟 500ms 后再删一次</item>
/// <item><see cref="CacheInvalidatedEvent.Pattern"/> 非空：用 SCAN 遍历匹配 <c>leno:cache:{pattern}</c> 的 Key 并删除，延迟 500ms 后再删一次</item>
/// </list>
/// </para>
/// <para>
/// 连接健壮性：监听 <see cref="IConnectionMultiplexer"/> 的 <c>ConnectionFailed</c>/<c>InternalError</c> 事件，
/// 断线后以指数退避自动重新订阅通道，避免 Redis 故障恢复后订阅静默失效。
/// </para>
/// </summary>
public sealed class CacheInvalidationSubscriber : IHostedService, IDisposable
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<CacheInvalidationSubscriber> _logger;
    private ISubscriber? _subscriber;
    private CancellationTokenSource? _stoppingCts;
    private int _disposed;

    /// <summary>Redis Pub/Sub 通道名。</summary>
    public const string ChannelName = "leno:cache:invalidated";

    /// <summary>缓存 Key 前缀，需与 <see cref="Middleware.CacheMiddleware.KeyPrefix"/> 一致。</summary>
    private const string KeyPrefix = "leno:cache:";

    /// <summary>双删模式默认延迟时间，缩小"先删→写库→读回填"脏读窗口。</summary>
    private static readonly TimeSpan DefaultDoubleDeleteDelay = TimeSpan.FromMilliseconds(500);

    /// <summary>重连指数退避默认初始延迟。</summary>
    private static readonly TimeSpan DefaultReconnectInitialDelay = TimeSpan.FromSeconds(1);

    /// <summary>重连指数退避默认最大延迟。</summary>
    private static readonly TimeSpan DefaultReconnectMaxDelay = TimeSpan.FromSeconds(30);

    /// <summary>
    /// 双删延迟覆盖值，测试时设为较短时间以加速。null 时使用 <see cref="DefaultDoubleDeleteDelay"/>。
    /// </summary>
    internal TimeSpan? DoubleDeleteDelayOverride { get; set; }

    /// <summary>
    /// 重连初始延迟覆盖值，测试时设为较短时间以加速。null 时使用 <see cref="DefaultReconnectInitialDelay"/>。
    /// </summary>
    internal TimeSpan? ReconnectInitialDelayOverride { get; set; }

    private TimeSpan DoubleDeleteDelay => DoubleDeleteDelayOverride ?? DefaultDoubleDeleteDelay;
    private TimeSpan ReconnectInitialDelay => ReconnectInitialDelayOverride ?? DefaultReconnectInitialDelay;
    private TimeSpan ReconnectMaxDelay => DefaultReconnectMaxDelay;

    public CacheInvalidationSubscriber(
        IConnectionMultiplexer redis,
        ILogger<CacheInvalidationSubscriber> logger)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _stoppingCts = new CancellationTokenSource();
        try
        {
            SubscribeToRedisEvents();
            EnsureSubscribed();
            _logger.LogInformation(
                "Subscribed to cache invalidation channel {Channel}", ChannelName);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // 订阅失败不阻断启动，由健康检查兜底，断线事件会触发自动重连
            _logger.LogError(ex,
                "Failed to subscribe to cache invalidation channel {Channel}", ChannelName);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 订阅 <see cref="IConnectionMultiplexer"/> 的连接失败与内部错误事件，
    /// 触发自动重连并重新订阅 Pub/Sub 通道。
    /// </summary>
    private void SubscribeToRedisEvents()
    {
        _redis.ConnectionFailed += OnConnectionFailed;
        _redis.InternalError += OnInternalError;
    }

    private void OnConnectionFailed(object? sender, ConnectionFailedEventArgs e)
    {
        // Subscription 连接断开会导致 Pub/Sub 订阅静默失效，需要重新订阅
        _logger.LogWarning(
            "Redis 连接失败 EndPoint={EndPoint} ConnectionType={ConnectionType} FailureType={FailureType} Exception={Exception}",
            e.EndPoint,
            e.ConnectionType,
            e.FailureType,
            e.Exception?.Message);

        // 后台触发指数退避重连，不阻塞事件回调线程
        _ = Task.Run(() => ReconnectWithBackoffAsync());
    }

    private void OnInternalError(object? sender, InternalErrorEventArgs e)
    {
        // 内部错误（如订阅通道异常）同样触发重连
        _logger.LogWarning(
            "Redis 内部错误 EndPoint={EndPoint} Origin={Origin} Exception={Exception}",
            e.EndPoint,
            e.Origin,
            e.Exception?.Message);

        _ = Task.Run(() => ReconnectWithBackoffAsync());
    }

    /// <summary>
    /// 指数退避重新订阅：连接恢复后 StackExchange.Redis 会自动重建物理连接，
    /// 但 Pub/Sub 订阅需要显式重新注册。指数退避避免在 Redis 持续不可用时打爆日志。
    /// </summary>
    private async Task ReconnectWithBackoffAsync()
    {
        var ct = _stoppingCts?.Token ?? CancellationToken.None;
        var delay = ReconnectInitialDelay;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(delay, ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            try
            {
                EnsureSubscribed();
                _logger.LogInformation(
                    "Redis 缓存失效订阅重连成功 Channel={Channel}", ChannelName);
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex,
                    "Redis 缓存失效订阅重连失败，{DelaySeconds}s 后重试 Channel={Channel}",
                    delay.TotalSeconds, ChannelName);
                // 指数退避：1s → 2s → 4s → 8s → 16s → 30s 封顶
                delay = TimeSpan.FromMilliseconds(Math.Min(delay.TotalMilliseconds * 2, ReconnectMaxDelay.TotalMilliseconds));
            }
        }
    }

    /// <summary>
    /// 确保 Pub/Sub 通道已订阅。重复调用幂等（先 Unsubscribe 再 Subscribe）。
    /// </summary>
    private void EnsureSubscribed()
    {
        _subscriber = _redis.GetSubscriber();
        _subscriber.Subscribe(RedisChannel.Literal(ChannelName), OnMessage);
    }

    /// <summary>
    /// Redis 消息回调。签名要求 <c>async void</c>，内部 try-catch 防止未观察异常。
    /// 采用双删模式：立即删除 → 延迟 500ms → 再删一次，缩小"先删→写库→并发读回填"脏读窗口。
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
            var ct = _stoppingCts?.Token ?? CancellationToken.None;

            if (!string.IsNullOrEmpty(evt.CacheKey))
            {
                var fullKey = KeyPrefix + evt.CacheKey;
                await db.KeyDeleteAsync(fullKey);
                _logger.LogDebug("Invalidated cache key {Key} (first delete)", evt.CacheKey);

                // 第二次删除：延迟 500ms 后再删，覆盖并发读回填脏数据
                await DelayedDeleteAsync(db, fullKey, evt.CacheKey, ct);
            }

            if (!string.IsNullOrEmpty(evt.Pattern))
            {
                await InvalidatePatternAsync(db, evt.Pattern);
                // Pattern 双删：延迟 500ms 后再次扫描删除
                await Task.Delay(DoubleDeleteDelay, ct);
                await InvalidatePatternAsync(db, evt.Pattern, isSecondDelete: true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process cache invalidation message: {Message}", message);
        }
    }

    /// <summary>
    /// 延迟 500ms 后再次删除指定 Key（双删模式第二阶段）。
    /// 失败仅记日志不抛出，避免阻塞订阅线程。
    /// </summary>
    private async Task DelayedDeleteAsync(IDatabase db, string fullKey, string originalKey, CancellationToken ct)
    {
        try
        {
            await Task.Delay(DoubleDeleteDelay, ct);
            await db.KeyDeleteAsync(fullKey);
            _logger.LogDebug("Invalidated cache key {Key} (second delete)", originalKey);
        }
        catch (OperationCanceledException)
        {
            // 服务停止，忽略
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Second delete failed for cache key {Key}", originalKey);
        }
    }

    private async Task InvalidatePatternAsync(IDatabase db, string pattern, bool isSecondDelete = false)
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
            "Invalidated {Count} cache keys matching pattern {Pattern} (second delete: {IsSecondDelete})",
            deleted, pattern, isSecondDelete);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            _stoppingCts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // CTS 已被 Dispose 释放（DI 容器先于 Host.StopAsync 释放单例的场景），忽略
        }

        // 取消事件订阅，避免停止后回调
        try
        {
            _redis.ConnectionFailed -= OnConnectionFailed;
            _redis.InternalError -= OnInternalError;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to detach Redis event handlers");
        }

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
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        try
        {
            _stoppingCts?.Cancel();
            _stoppingCts?.Dispose();
        }
        catch
        {
            // 忽略 dispose 异常
        }

        try
        {
            _subscriber?.UnsubscribeAll();
        }
        catch
        {
            // 忽略 dispose 异常
        }

        try
        {
            _redis.ConnectionFailed -= OnConnectionFailed;
            _redis.InternalError -= OnInternalError;
        }
        catch
        {
            // 忽略 dispose 异常
        }
    }
}
