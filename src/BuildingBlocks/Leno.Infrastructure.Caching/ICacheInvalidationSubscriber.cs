namespace Leno.Infrastructure.Caching;

/// <summary>
/// 缓存失效通知订阅者：订阅 Redis Pub/Sub 通道，收到失效通知时清除本地 L1 缓存中对应的 Key。
/// <para>
/// 实现通常作为 <c>IHostedService</c> 后台运行，应用启动时订阅通道，
/// 收到消息时调用 <c>IMemoryCache.Remove(key)</c> 仅清进程内 L1（不影响 L2 Redis）。
/// </para>
/// <para>
/// L1 短 TTL（默认 5s）兜底：即使 Pub/Sub 消息丢失，5s 后 L1 自动过期回源 L2。
/// </para>
/// </summary>
public interface ICacheInvalidationSubscriber
{
    /// <summary>启动订阅。</summary>
    Task StartAsync(CancellationToken ct);

    /// <summary>停止订阅。</summary>
    Task StopAsync(CancellationToken ct);
}
