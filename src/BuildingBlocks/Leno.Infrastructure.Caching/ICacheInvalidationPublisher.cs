namespace Leno.Infrastructure.Caching;

/// <summary>
/// 缓存失效通知发布者：通过 Redis Pub/Sub 广播 Key 失效消息，
/// 通知所有订阅实例清除本地 L1 缓存中对应的 Key。
/// <para>
/// 发布端由 <see cref="IMultiLevelCache.RemoveAsync"/> 触发；
/// 订阅端由 <see cref="ICacheInvalidationSubscriber"/> 接收并清本地 L1。
/// </para>
/// </summary>
public interface ICacheInvalidationPublisher
{
    /// <summary>
    /// 发布指定 Key 的失效通知到 Pub/Sub 通道。
    /// </summary>
    /// <param name="key">失效的缓存键。</param>
    /// <param name="ct">取消令牌。</param>
    Task PublishInvalidationAsync(string key, CancellationToken ct = default);
}
