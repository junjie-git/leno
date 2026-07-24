namespace Leno.Infrastructure.Caching;

/// <summary>
/// 多级缓存（L1 本地 + L2 Redis）配置选项，对应 appsettings.json 中 <c>Cache:MultiLevel</c> 节。
/// <para>
/// L1 默认 TTL 5s（短 TTL 兜底 Pub/Sub 消息丢失场景），L2 默认 TTL 30min。
/// L1 跨实例失效通过 Redis Pub/Sub 通道 <see cref="InvalidationChannel"/> 广播。
/// </para>
/// </summary>
public sealed class MultiLevelCacheOptions
{
    public const string SectionName = "Cache:MultiLevel";

    /// <summary>
    /// L1 本地缓存（IMemoryCache）默认 TTL。默认 5 秒。
    /// <para>
    /// 短 TTL 用于兜底：即使 Pub/Sub 跨实例失效消息丢失，5s 后 L1 自动过期，
    /// 后续请求回源 L2（Redis），保证最终一致性窗口 ≤ 5s。
    /// </para>
    /// </summary>
    public TimeSpan L1Ttl { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// L2 Redis 缓存默认 TTL。默认 30 分钟。
    /// 由 <see cref="ICacheService"/> 内置随机抖动（30-120s）防雪崩。
    /// </summary>
    public TimeSpan L2Ttl { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Redis Pub/Sub 失效通知通道名。所有实例订阅同一通道，
    /// 任一实例 <c>RemoveAsync</c> 时通过此通道广播失效消息，其他实例收到后清本地 L1。
    /// </summary>
    public string InvalidationChannel { get; set; } = "leno:cache:invalidation";

    /// <summary>
    /// 启用 L1 的 Key 前缀列表（feature flag 按 Key 前缀切流）。
    /// <para>
    /// - 空集合（默认）：所有 Key 均启用 L1（全局多级缓存）。
    /// - 非空集合：仅当 Key 以任一前缀开头时启用 L1，其余 Key 仅走 L2。
    /// 例如 <c>["product:", "promotion:seckill:"]</c> 表示仅热点 Key 启用 L1。
    /// </para>
    /// </summary>
    public IReadOnlyList<string> L1EnabledPrefixes { get; set; } = Array.Empty<string>();

    /// <summary>
    /// 判断指定 Key 是否启用 L1 本地缓存。
    /// <para>
    /// 当 <see cref="L1EnabledPrefixes"/> 为空时，所有 Key 均启用 L1（默认全局多级缓存）。
    /// 否则仅当 Key 以任一配置前缀开头时返回 true。
    /// </para>
    /// </summary>
    /// <param name="key">缓存键。</param>
    /// <returns>true 表示启用 L1；false 表示仅走 L2。</returns>
    public bool IsL1EnabledForKey(string key)
    {
        if (L1EnabledPrefixes.Count == 0)
        {
            return true;
        }

        if (string.IsNullOrEmpty(key))
        {
            return false;
        }

        foreach (var prefix in L1EnabledPrefixes)
        {
            if (!string.IsNullOrEmpty(prefix) && key.StartsWith(prefix, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
