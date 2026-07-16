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
    private readonly IBloomFilter _bloomFilter;
    private readonly ILogger<CacheService> _logger;
    private readonly Random _random;

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
    /// 互斥锁超时时间。
    /// </summary>
    private static readonly TimeSpan LockTimeout = TimeSpan.FromSeconds(10);

    public CacheService(
        IConnectionMultiplexer connectionMultiplexer,
        IBloomFilter bloomFilter,
        ILogger<CacheService> logger)
    {
        ArgumentNullException.ThrowIfNull(connectionMultiplexer);
        _database = connectionMultiplexer.GetDatabase();
        _bloomFilter = bloomFilter;
        _logger = logger;
        _random = new Random();
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

        // 未获取到锁，等待后重试读取缓存
        _logger.LogDebug("未获取互斥锁，等待后重试: {Key}", key);
        await Task.Delay(100, ct);

        var retryValue = await _database.StringGetAsync(key);
        if (retryValue.HasValue)
        {
            var retryString = retryValue.ToString();
            if (retryString == NullMarker)
            {
                return null;
            }

            return JsonSerializer.Deserialize<T>(retryString);
        }

        return null;
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
    /// </summary>
    internal TimeSpan ApplyJitter(TimeSpan baseExpiry)
    {
        var jitterSeconds = _random.Next((int)JitterMin.TotalSeconds, (int)JitterMax.TotalSeconds + 1);
        return baseExpiry.Add(TimeSpan.FromSeconds(jitterSeconds));
    }
}