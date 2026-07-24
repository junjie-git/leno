using Leno.Infrastructure.Abstractions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leno.Infrastructure.Caching;

/// <summary>
/// 多级缓存实现：L1 进程内本地缓存（<see cref="IMemoryCache"/>，短 TTL）+ L2 分布式缓存（<see cref="ICacheService"/>，Redis，长 TTL）。
/// <para>
/// 读路径（<see cref="GetAsync{T}"/>）：
/// <list type="number">
/// <item>L1 命中 → 直接返回（最快，无网络往返）。</item>
/// <item>L1 未命中 → 查 L2；L2 命中 → 回填 L1 后返回。</item>
/// <item>L1/L2 均未命中 → 调用 <paramref name="factory"/> 回源 → 同时回填 L1+L2（仅当值非 null）。</item>
/// </list>
/// </para>
/// <para>
/// 写路径（<see cref="SetAsync{T}"/>）：同时写入 L1（按 <see cref="MultiLevelCacheOptions.L1Ttl"/>）+ L2（按 <paramref name="l2Ttl"/> 或 <see cref="MultiLevelCacheOptions.L2Ttl"/>）。
/// </para>
/// <para>
/// 失效路径（<see cref="RemoveAsync"/>）：删 L1 + 删 L2 + 通过 <see cref="ICacheInvalidationPublisher"/> 发布 Pub/Sub 失效通知，
/// 通知其他实例删 L1。L1 短 TTL（默认 5s）兜底 Pub/Sub 消息丢失场景。
/// </para>
/// <para>
/// L1 启用策略：通过 <see cref="MultiLevelCacheOptions.IsL1EnabledForKey"/> 按 Key 前缀切流。
/// 当 L1 未启用时，所有读写仅走 L2（与原 <see cref="CacheService"/> 行为一致）。
/// </para>
/// </summary>
public sealed class MultiLevelCache : IMultiLevelCache
{
    private readonly IMemoryCache _l1;
    private readonly ICacheService _l2;
    private readonly ICacheInvalidationPublisher _invalidationPublisher;
    private readonly MultiLevelCacheOptions _options;
    private readonly ILogger<MultiLevelCache> _logger;

    public MultiLevelCache(
        IMemoryCache l1,
        ICacheService l2,
        ICacheInvalidationPublisher invalidationPublisher,
        IOptions<MultiLevelCacheOptions> options,
        ILogger<MultiLevelCache> logger)
    {
        ArgumentNullException.ThrowIfNull(l1);
        ArgumentNullException.ThrowIfNull(l2);
        ArgumentNullException.ThrowIfNull(invalidationPublisher);
        ArgumentNullException.ThrowIfNull(options);
        _l1 = l1;
        _l2 = l2;
        _invalidationPublisher = invalidationPublisher;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<T?> GetAsync<T>(
        string key,
        Func<CancellationToken, Task<T?>> factory,
        TimeSpan? l2Ttl = null,
        CancellationToken ct = default) where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(factory);

        var l1Enabled = _options.IsL1EnabledForKey(key);

        // L1 命中：直接返回（仅当 L1 启用时检查）
        if (l1Enabled && _l1.TryGetValue(key, out T? l1Value))
        {
            _logger.LogDebug("L1 命中: Key={Key}", key);
            return l1Value;
        }

        // L2 命中：回填 L1 后返回
        var l2Value = await _l2.GetAsync<T>(key, ct).ConfigureAwait(false);
        if (l2Value is not null)
        {
            _logger.LogDebug("L2 命中: Key={Key}", key);
            if (l1Enabled)
            {
                _l1.Set(key, l2Value, _options.L1Ttl);
            }
            return l2Value;
        }

        // 双 miss：回源 + 回填 L1+L2
        var value = await factory(ct).ConfigureAwait(false);
        if (value is not null)
        {
            var effectiveL2Ttl = l2Ttl ?? _options.L2Ttl;
            await _l2.SetAsync(key, value, effectiveL2Ttl, ct).ConfigureAwait(false);
            if (l1Enabled)
            {
                _l1.Set(key, value, _options.L1Ttl);
            }
            _logger.LogDebug("双 miss 回源并回填: Key={Key}, L2Ttl={L2Ttl}", key, effectiveL2Ttl);
        }
        else
        {
            _logger.LogDebug("双 miss 回源返回 null，不回填缓存: Key={Key}", key);
        }

        return value;
    }

    /// <inheritdoc />
    public async Task SetAsync<T>(
        string key,
        T value,
        TimeSpan? l2Ttl = null,
        CancellationToken ct = default) where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);

        var effectiveL2Ttl = l2Ttl ?? _options.L2Ttl;
        await _l2.SetAsync(key, value, effectiveL2Ttl, ct).ConfigureAwait(false);

        if (_options.IsL1EnabledForKey(key))
        {
            _l1.Set(key, value, _options.L1Ttl);
        }

        _logger.LogDebug("已写入 L1+L2: Key={Key}, L2Ttl={L2Ttl}", key, effectiveL2Ttl);
    }

    /// <inheritdoc />
    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        // 先删本地 L1（同步），再删 L2（异步），最后 Pub/Sub 通知其他实例删 L1
        _l1.Remove(key);
        await _l2.RemoveAsync(key, ct).ConfigureAwait(false);
        await _invalidationPublisher.PublishInvalidationAsync(key, ct).ConfigureAwait(false);

        _logger.LogDebug("已失效 L1+L2 并发布 Pub/Sub 通知: Key={Key}", key);
    }
}
