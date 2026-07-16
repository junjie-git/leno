namespace Leno.Infrastructure.Abstractions;

/// <summary>
/// 分布式缓存服务抽象，提供缓存穿透防护（布隆过滤器）、缓存击穿防护（互斥锁）、
/// 缓存雪崩防护（随机抖动过期时间）等能力。
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// 获取或设置缓存项。若缓存命中则直接返回；若未命中则调用工厂方法获取数据、写入缓存并返回。
    /// 内置布隆过滤器检查 —— 若 key 一定不存在，直接返回默认值，无需查询后端。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="key">缓存键。</param>
    /// <param name="factory">当缓存未命中时获取数据的工厂方法。</param>
    /// <param name="expiry">缓存过期时间。若为 null 则使用默认值（5 分钟）。</param>
    /// <param name="ct">取消令牌。</param>
    Task<T?> GetOrSetAsync<T>(string key, Func<CancellationToken, Task<T?>> factory, TimeSpan? expiry = null, CancellationToken ct = default) where T : class;

    /// <summary>
    /// 设置缓存项。
    /// </summary>
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default) where T : class;

    /// <summary>
    /// 获取缓存项。
    /// </summary>
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class;

    /// <summary>
    /// 移除缓存项。
    /// </summary>
    Task RemoveAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// 双删模式失效缓存：先删 → 执行业务写库 → 延迟 500ms → 再删一次，
    /// 缩小"先删→写库→并发读回填"脏读窗口。
    /// <para>
    /// 调用方应在执行 DB 写入时使用此方法，将写库委托传入：
    /// <code>
    /// await cache.InvalidateWithDoubleDeleteAsync(key, async ct =&gt;
    /// {
    ///     await repo.UpdateAsync(entity, ct);
    /// }, ct);
    /// </code>
    /// </para>
    /// </summary>
    /// <param name="key">缓存键。</param>
    /// <param name="writeAction">业务写库委托（在第一次删除与第二次删除之间执行）。</param>
    /// <param name="ct">取消令牌。</param>
    Task InvalidateWithDoubleDeleteAsync(
        string key,
        Func<CancellationToken, Task> writeAction,
        CancellationToken ct = default);

    /// <summary>
    /// 按 glob 模式批量失效缓存。使用 SCAN 游标迭代匹配 key（避免 <c>KEYS</c> 阻塞 Redis），
    /// 默认每 100 个 key 批量 <c>UNLINK</c>（异步删除，不阻塞 Redis 主线程）。
    /// <para>
    /// 性能要点：
    /// <list type="bullet">
    /// <item>使用 <c>SCAN</c> 而非 <c>KEYS</c>，避免在大 key 空间下阻塞 Redis。</item>
    /// <item>使用 <c>UNLINK</c> 而非 <c>DEL</c>，Redis 在后台异步释放内存，不阻塞主线程。</item>
    /// <item>批量 UNLINK（默认每批 100 个 key），减少网络往返。</item>
    /// </list>
    /// </para>
    /// </summary>
    /// <param name="pattern">glob 模式（如 <c>user:*</c>）。调用方负责包含必要的 key 前缀。</param>
    /// <param name="ct">取消令牌。</param>
    Task InvalidatePatternAsync(string pattern, CancellationToken ct = default);

    /// <summary>
    /// 预热布隆过滤器，将一批已知存在的 key 批量添加到过滤器。
    /// 在服务启动时调用。
    /// </summary>
    Task PreWarmBloomFilterAsync(IEnumerable<string> keys, CancellationToken ct = default);
}
