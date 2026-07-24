using Leno.Infrastructure.Abstractions;

namespace Leno.Infrastructure.Caching;

/// <summary>
/// 多级缓存抽象：L1 进程内本地缓存（IMemoryCache，短 TTL）+ L2 分布式缓存（Redis，长 TTL）。
/// <para>
/// 读路径：
/// <list type="number">
/// <item>L1 命中 → 直接返回（最快，无网络往返）。</item>
/// <item>L1 未命中 → 查 L2；L2 命中 → 回填 L1 后返回（一次网络往返）。</item>
/// <item>L1/L2 均未命中 → 调用 <paramref name="factory"/> 回源 → 同时回填 L1+L2。</item>
/// </list>
/// </para>
/// <para>
/// 失效路径：
/// <list type="number">
/// <item><see cref="RemoveAsync"/> 删 L1 + L2 + 通过 Pub/Sub 通知其他实例删 L1。</item>
/// <item>L1 短 TTL（默认 5s）兜底 Pub/Sub 消息丢失场景。</item>
/// </list>
/// </para>
/// <para>
/// 类型约束 <c>T : class</c> 与 L2 <see cref="ICacheService"/> 对齐，
/// 因为 L2 基于 JSON 序列化，仅支持引用类型。
/// </para>
/// </summary>
public interface IMultiLevelCache
{
    /// <summary>
    /// 获取缓存项，未命中则调用工厂方法回源并回填 L1+L2。
    /// </summary>
    /// <typeparam name="T">缓存值类型（引用类型）。</typeparam>
    /// <param name="key">缓存键。</param>
    /// <param name="factory">L1+L2 均未命中时获取数据的工厂方法。</param>
    /// <param name="l2Ttl">L2 缓存 TTL 覆盖值；为 null 时使用 <see cref="MultiLevelCacheOptions.L2Ttl"/> 默认值。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>缓存值；若工厂返回 null 则不回填缓存并返回 null。</returns>
    Task<T?> GetAsync<T>(string key, Func<CancellationToken, Task<T?>> factory, TimeSpan? l2Ttl = null, CancellationToken ct = default) where T : class;

    /// <summary>
    /// 同时写入 L1 + L2。
    /// </summary>
    /// <typeparam name="T">缓存值类型（引用类型）。</typeparam>
    /// <param name="key">缓存键。</param>
    /// <param name="value">缓存值。为 null 时仅写入 L2 空值标记（防穿透），不写入 L1。</param>
    /// <param name="l2Ttl">L2 缓存 TTL 覆盖值；为 null 时使用 <see cref="MultiLevelCacheOptions.L2Ttl"/> 默认值。</param>
    /// <param name="ct">取消令牌。</param>
    Task SetAsync<T>(string key, T value, TimeSpan? l2Ttl = null, CancellationToken ct = default) where T : class;

    /// <summary>
    /// 移除缓存项：删 L1 + 删 L2 + 通过 Pub/Sub 通知其他实例删 L1。
    /// </summary>
    /// <param name="key">缓存键。</param>
    /// <param name="ct">取消令牌。</param>
    Task RemoveAsync(string key, CancellationToken ct = default);
}
