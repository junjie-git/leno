namespace Leno.Notification.Domain.Services;

/// <summary>
/// 分布式锁提供者接口，用于 Job 多实例并发时防止重复拾取同一记录。
/// 由基础设施层实现（如 Redis SET NX EX 模式）。
/// </summary>
public interface IDistributedLockProvider
{
    /// <summary>
    /// 尝试获取分布式锁。
    /// </summary>
    /// <param name="key">锁键。</param>
    /// <param name="expiry">锁过期时间（TTL），防止持锁进程崩溃后死锁。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>成功获取时返回锁令牌（非 null）；已被他人持有时返回 null。</returns>
    Task<string?> TryAcquireAsync(string key, TimeSpan expiry, CancellationToken ct = default);

    /// <summary>
    /// 释放分布式锁。仅当令牌匹配时才删除（防止误删他人持有的锁）。
    /// </summary>
    /// <param name="key">锁键。</param>
    /// <param name="token">获取时返回的锁令牌。</param>
    /// <param name="ct">取消令牌。</param>
    Task ReleaseAsync(string key, string token, CancellationToken ct = default);
}
