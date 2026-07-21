using System.Text.Json;
using Leno.SharedKernel.Abstractions;
using Leno.Infrastructure.Abstractions;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Leno.Infrastructure.Caching;

/// <summary>
/// 分布式缓存服务实现，基于 Redis 提供缓存穿透防护（布隆过滤器）、
/// 缓存击穿防护（互斥锁）、缓存雪崩防护（随机抖动过期时间）。
/// 空值缓存（2 分钟短过期）防止缓存穿透。
/// </summary>
public sealed class CacheService : ICacheService
{
    private readonly IDatabase _database;
    private readonly IConnectionMultiplexer _redis;
    private readonly IBloomFilter _bloomFilter;
    private readonly ILogger<CacheService> _logger;

    /// <summary>
    /// 默认缓存过期时间：5 分钟。
    /// </summary>
    private static readonly TimeSpan DefaultExpiry = TimeSpan.FromMinutes(5);

    /// <summary>
    /// 空值缓存过期时间：2 分钟。
    /// </summary>
    private static readonly TimeSpan NullValueExpiry = TimeSpan.FromMinutes(2);

    /// <summary>
    /// 缓存雪崩防护的随机抖动范围：30 到 120 秒。
    /// </summary>
    private static readonly TimeSpan JitterMin = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan JitterMax = TimeSpan.FromSeconds(120);

    /// <summary>
    /// 空值标记，用于区分"缓存不存在"和"缓存值为 null"。
    /// </summary>
    private const string NullMarker = "__NULL_MARKER__";

    /// <summary>
    /// 互斥锁键前缀，用于缓存击穿防护。
    /// </summary>
    private const string LockPrefix = "leno:lock:";

    /// <summary>
    /// T25：缓存 key 强制前缀。所有 CacheService 写入 Redis 的业务 key 均应携带此前缀，
    /// 以区分缓存 key 与其他用途（锁、布隆过滤器、幂等键等）的 key。
    /// InvalidatePatternAsync 内部强制拼接此前缀，避免传入裸 pattern（如 <c>user:*</c>）
    /// 误删非缓存 key。
    /// </summary>
    private const string KeyPrefix = "leno:cache:";

    /// <summary>
    /// 互斥锁超时时间。
    /// </summary>
    private static readonly TimeSpan LockTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// T27：缓存击穿防护中获取互斥锁失败后的指数退避重试间隔（毫秒）。
    /// 默认 3 次重试：50ms → 100ms → 200ms（总等待 350ms）。
    /// 每次重试前先检查缓存，若其他线程已回填则直接返回缓存值。
    /// </summary>
    private static readonly int[] LockRetryBackoffMs = { 50, 100, 200 };

    /// <summary>
    /// T27：重试间隔覆盖值（毫秒），测试时可设为全 0 或极小值以加速。null 时使用 <see cref="LockRetryBackoffMs"/>。
    /// </summary>
    internal int[]? LockRetryBackoffMsOverride { get; set; }

    /// <summary>
    /// T27：实际使用的重试间隔数组。返回覆盖值或默认值。
    /// </summary>
    private int[] EffectiveLockRetryBackoffMs => LockRetryBackoffMsOverride ?? LockRetryBackoffMs;

    public CacheService(
        IConnectionMultiplexer connectionMultiplexer,
        IBloomFilter bloomFilter,
        ILogger<CacheService> logger)
    {
        ArgumentNullException.ThrowIfNull(connectionMultiplexer);
        _redis = connectionMultiplexer;
        _database = connectionMultiplexer.GetDatabase();
        _bloomFilter = bloomFilter;
        _logger = logger;
    }

    public async Task<T?> GetOrSetAsync<T>(
        string key,
        Func<CancellationToken, Task<T?>> factory,
        TimeSpan? expiry = null,
        CancellationToken ct = default) where T : class
    {
        ArgumentNullException.ThrowIfNull(key);

        // 布隆过滤器检查：若 key 一定不存在，直接返回 null
        if (!await _bloomFilter.MightContainAsync(key, ct))
        {
            _logger.LogDebug("BloomFilter 判定 key 不存在，跳过缓存查询: {Key}", key);
            return null;
        }

        // 尝试从缓存获取
        var cachedValue = await _database.StringGetAsync(key);
        if (cachedValue.HasValue)
        {
            var cachedString = cachedValue.ToString();
            if (cachedString == NullMarker)
            {
                _logger.LogDebug("缓存命中空值标记: {Key}", key);
                return null;
            }

            _logger.LogDebug("缓存命中: {Key}", key);
            return JsonSerializer.Deserialize<T>(cachedString);
        }

        // 缓存未命中，使用互斥锁防止缓存击穿
        var lockKey = $"{LockPrefix}{key}";
        var lockToken = Guid.NewGuid().ToString("N");

        if (await _database.LockTakeAsync(lockKey, lockToken, LockTimeout))
        {
            try
            {
                // 双重检查：获取锁后再次检查缓存
                var doubleCheck = await _database.StringGetAsync(key);
                if (doubleCheck.HasValue)
                {
                    var doubleCheckString = doubleCheck.ToString();
                    if (doubleCheckString == NullMarker)
                    {
                        return null;
                    }

                    return JsonSerializer.Deserialize<T>(doubleCheckString);
                }

                // 调用工厂获取数据
                var value = await factory(ct);

                if (value is null)
                {
                    // 缓存空值，使用短过期时间，防止缓存穿透
                    await SetNullValueAsync(key, ct);
                    _logger.LogDebug("工厂返回 null，缓存空值: {Key}, Expiry={Expiry}", key, NullValueExpiry);
                }
                else
                {
                    await SetWithJitterAsync(key, value, expiry ?? DefaultExpiry, ct);
                    _logger.LogDebug("缓存写入: {Key}", key);
                }

                // 将 key 添加到布隆过滤器
                await _bloomFilter.AddAsync(key, ct);

                return value;
            }
            finally
            {
                await _database.LockReleaseAsync(lockKey, lockToken);
            }
        }

        // T27：未获取到锁，按指数退避重试（50ms → 100ms → 200ms，最多 3 次）。
        // 每次重试前先检查缓存，若其他线程已回填则直接返回缓存值，避免不必要的回源。
        // 重试耗尽后仍无缓存值时，记 warning 并直接回源（factory），保证可用性优先于防击穿。
        var backoffMs = EffectiveLockRetryBackoffMs;
        foreach (var delayMs in backoffMs)
        {
            _logger.LogDebug("未获取互斥锁，{Delay}ms 后重试读取缓存: {Key}", delayMs, key);
            await Task.Delay(delayMs, ct);

            var retryValue = await _database.StringGetAsync(key);
            if (retryValue.HasValue)
            {
                var retryString = retryValue.ToString();
                if (retryString == NullMarker)
                {
                    _logger.LogDebug("重试命中空值标记: {Key}", key);
                    return null;
                }

                _logger.LogDebug("重试命中缓存: {Key}", key);
                return JsonSerializer.Deserialize<T>(retryString);
            }
        }

        // T27：重试耗尽仍无缓存值，记 warning 并直接回源。
        // 注意：此处不设置缓存（未持有锁，避免与持锁线程的写回竞争导致脏写），
        // 也不重新尝试获取锁（避免无限等待），保证请求可用性。
        _logger.LogWarning(
            "缓存击穿防护：{RetryCount} 次指数退避重试后仍未获取互斥锁且缓存未回填，直接回源 Key={Key}",
            backoffMs.Length, key);

        return await factory(ct);
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default) where T : class
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);

        await SetWithJitterAsync(key, value, expiry ?? DefaultExpiry, ct);
        await _bloomFilter.AddAsync(key, ct);
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
    {
        ArgumentNullException.ThrowIfNull(key);

        var cachedValue = await _database.StringGetAsync(key);
        if (!cachedValue.HasValue)
        {
            return null;
        }

        var cachedString = cachedValue.ToString();
        if (cachedString == NullMarker)
        {
            return null;
        }

        return JsonSerializer.Deserialize<T>(cachedString);
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(key);

        await _database.KeyDeleteAsync(key);
        _logger.LogDebug("缓存移除: {Key}", key);
    }

    /// <summary>
    /// 双删模式延迟时间：500ms。缩小"先删→写库→并发读回填"脏读窗口。
    /// 测试可通过 <see cref="DoubleDeleteDelayOverride"/> 覆盖以加速。
    /// </summary>
    private static readonly TimeSpan DefaultDoubleDeleteDelay = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// 双删延迟覆盖值，测试时设为较短时间以加速。null 时使用 <see cref="DefaultDoubleDeleteDelay"/>。
    /// </summary>
    internal TimeSpan? DoubleDeleteDelayOverride { get; set; }

    private TimeSpan DoubleDeleteDelay => DoubleDeleteDelayOverride ?? DefaultDoubleDeleteDelay;

    /// <summary>
    /// 双删模式失效缓存：先删 → 执行业务写库 → 延迟 500ms → 再删一次。
    /// <para>
    /// 调用方应将 DB 写入委托传入，由本方法在两次删除之间执行写库操作，
    /// 确保即使有并发读在写库提交后立即回填缓存，第二次删除也能清除脏数据。
    /// </para>
    /// </summary>
    public async Task InvalidateWithDoubleDeleteAsync(
        string key,
        Func<CancellationToken, Task> writeAction,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(writeAction);

        // 阶段 1：第一次删除缓存
        await _database.KeyDeleteAsync(key);
        _logger.LogDebug("缓存双删-第一次删除: {Key}", key);

        try
        {
            // 阶段 2：执行业务写库（调用方委托）
            await writeAction(ct);
        }
        finally
        {
            // 阶段 3：延迟 500ms 后再次删除，覆盖并发读回填的脏数据
            // 即使写库抛异常也执行第二次删除，避免脏缓存残留
            try
            {
                await Task.Delay(DoubleDeleteDelay, ct);
                await _database.KeyDeleteAsync(key);
                _logger.LogDebug("缓存双删-第二次删除: {Key}", key);
            }
            catch (OperationCanceledException)
            {
                // 取消时不影响已抛出的写库异常
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "缓存双删-第二次删除失败: {Key}", key);
            }
        }
    }

    public async Task PreWarmBloomFilterAsync(IEnumerable<string> keys, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(keys);

        var keyList = keys.ToList();
        _logger.LogInformation("布隆过滤器预热开始，共 {Count} 个 key", keyList.Count);

        foreach (var key in keyList)
        {
            ct.ThrowIfCancellationRequested();
            await _bloomFilter.AddAsync(key, ct);
        }

        _logger.LogInformation("布隆过滤器预热完成，共 {Count} 个 key", keyList.Count);
    }

    // ===== T23: InvalidatePatternAsync — UNLINK + 分批 SCAN =====

    /// <summary>
    /// T23：Pattern 失效批量 UNLINK 的默认批次大小（每批 100 个 key）。
    /// </summary>
    private const int DefaultPatternInvalidationBatchSize = 100;

    /// <summary>
    /// T23：批次大小覆盖值，测试时可设为较小值以加速验证分批行为。null 时使用默认值 100。
    /// </summary>
    internal int? PatternInvalidationBatchSizeOverride { get; set; }

    /// <summary>
    /// T23：实际使用的批次大小。
    /// </summary>
    private int PatternInvalidationBatchSize => PatternInvalidationBatchSizeOverride ?? DefaultPatternInvalidationBatchSize;

    /// <summary>
    /// 按 glob 模式批量失效缓存（T23 性能优化）。
    /// <para>
    /// 实现要点：
    /// <list type="bullet">
    /// <item>使用 <c>SCAN</c> 游标迭代匹配 key（<see cref="IServer.KeysAsync"/> 内部使用 SCAN），
    /// 避免 <c>KEYS</c> 在大 key 空间下阻塞 Redis 主线程。</item>
    /// <item>使用 <c>UNLINK</c> 异步删除（Redis 4.0+），而非 <c>DEL</c> 同步删除。
    /// UNLINK 将实际内存释放放到后台线程，不阻塞 Redis 主线程。</item>
    /// <item>批量 UNLINK：默认每 100 个 key 合并为一次 <c>UNLINK key1 key2 ...</c> 调用，
    /// 减少网络往返。批次大小可通过 <see cref="PatternInvalidationBatchSizeOverride"/> 覆盖。</item>
    /// </list>
    /// </para>
    /// </summary>
    /// <param name="pattern">glob 模式（如 <c>user:*</c> 或 <c>leno:cache:user:*</c>）。
    /// 内部强制拼接 <see cref="KeyPrefix"/>（<c>leno:cache:</c>），调用方无需手动添加前缀；
    /// 若传入 pattern 已以 <c>leno:cache:</c> 开头则不重复拼接。</param>
    /// <param name="ct">取消令牌。</param>
    /// <exception cref="ArgumentException">pattern 包含 <c>..</c> 路径穿越片段或为空字符串。</exception>
    public async Task InvalidatePatternAsync(string pattern, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        if (string.IsNullOrWhiteSpace(pattern))
        {
            throw new ArgumentException("pattern 不能为空字符串或仅空白字符", nameof(pattern));
        }

        // T25：拒绝路径穿越片段（虽 Redis key 不解析路径，但防止调用方误传文件系统语义 pattern）
        if (pattern.Contains("..", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "pattern 包含非法片段 \"..\"，拒绝执行以防误删", nameof(pattern));
        }

        // T25：强制拼接 KeyPrefix，确保只删除缓存 key，避免误删锁/幂等键/布隆过滤器等其他用途的 key
        var effectivePattern = pattern.StartsWith(KeyPrefix, StringComparison.Ordinal)
            ? pattern
            : KeyPrefix + pattern;

        // 获取主节点（非副本），SCAN 必须在主节点上执行以保证一致性
        var servers = _redis.GetServers();
        var server = servers.FirstOrDefault(s => !s.IsReplica);
        if (server is null)
        {
            _logger.LogWarning("无可用主 Redis 节点，跳过 Pattern 失效: {Pattern}", effectivePattern);
            return;
        }

        var batchSize = PatternInvalidationBatchSize;
        if (batchSize <= 0)
        {
            batchSize = DefaultPatternInvalidationBatchSize;
        }

        var batch = new List<RedisKey>(batchSize);
        var deleted = 0L;

        // SCAN 游标迭代：StackExchange.Redis 的 KeysAsync 内部使用 SCAN，不会阻塞 Redis
        await foreach (var key in server.KeysAsync(pattern: effectivePattern).WithCancellation(ct))
        {
            batch.Add(key);
            if (batch.Count >= batchSize)
            {
                deleted += await UnlinkBatchAsync(batch);
                batch.Clear();
            }
        }

        // 处理最后一批不足 batchSize 的 key
        if (batch.Count > 0)
        {
            deleted += await UnlinkBatchAsync(batch);
        }

        _logger.LogInformation(
            "Pattern 失效完成: 删除 {Count} 个匹配 key, Pattern={Pattern}",
            deleted, effectivePattern);
    }

    /// <summary>
    /// 批量 UNLINK 一组 key。
    /// <para>
    /// 使用 <c>UNLINK key1 key2 ...</c> 命令一次性删除多个 key，
    /// Redis 在后台线程异步释放内存，不阻塞主线程。
    /// 返回值是 UNLINK 命令返回的实际删除数量。
    /// </para>
    /// </summary>
    private async Task<long> UnlinkBatchAsync(List<RedisKey> keys)
    {
        if (keys.Count == 0)
        {
            return 0;
        }

        // 将 RedisKey 转为字符串作为 UNLINK 命令参数
        // （RedisKey 可隐式转换为 string/byte[]，这里用 ToString() 确保非 null）
        var args = new List<object>(keys.Count);
        foreach (var key in keys)
        {
            args.Add(key.ToString());
        }

        var result = await _database.ExecuteAsync("UNLINK", args, CommandFlags.None);
        // UNLINK 返回整数：实际删除的 key 数量
        return (long)result;
    }

    /// <summary>
    /// 设置空值缓存（短过期时间，防止缓存穿透）。
    /// </summary>
    private async Task SetNullValueAsync(string key, CancellationToken ct)
    {
        await _database.StringSetAsync(key, NullMarker, NullValueExpiry);
    }

    /// <summary>
    /// 设置缓存值并添加随机抖动（防止缓存雪崩）。
    /// </summary>
    private async Task SetWithJitterAsync<T>(string key, T value, TimeSpan baseExpiry, CancellationToken ct) where T : class
    {
        var jitteredExpiry = ApplyJitter(baseExpiry);
        var serialized = JsonSerializer.Serialize(value);
        await _database.StringSetAsync(key, serialized, jitteredExpiry);
    }

    /// <summary>
    /// 在原有过期时间上添加 30-120 秒的随机抖动，防止缓存雪崩。
    /// 使用 <see cref="Random.Shared"/>（.NET 6+ 线程安全零分配全局实例），
    /// 避免单例 CacheService 中实例字段 Random 的并发竞态。
    /// </summary>
    internal TimeSpan ApplyJitter(TimeSpan baseExpiry)
    {
        var jitterSeconds = Random.Shared.Next((int)JitterMin.TotalSeconds, (int)JitterMax.TotalSeconds + 1);
        return baseExpiry.Add(TimeSpan.FromSeconds(jitterSeconds));
    }
}