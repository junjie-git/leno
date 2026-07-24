namespace Leno.Infrastructure.RateLimiting;

/// <summary>
/// 通用分布式限流器接口，供各 BC 复用。
/// 基于 Redis 滑动窗口实现，支持 per-key 粒度限流。
/// 各 BC 通过 DI 注入 <see cref="IRateLimiter"/>，按业务键（如 userId、orderId）调用 <see cref="TryAcquireAsync"/>。
/// </summary>
public interface IRateLimiter
{
    /// <summary>
    /// 尝试获取限流许可。
    /// </summary>
    /// <param name="key">限流分区键（如 "seckill:user-123"），需包含策略名 + 实体标识。</param>
    /// <param name="permitLimit">窗口内最大许可数。</param>
    /// <param name="window">滑动窗口时长。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>限流结果，包含是否允许及当前计数。</returns>
    Task<RateLimitResult> TryAcquireAsync(string key, int permitLimit, TimeSpan window, CancellationToken cancellationToken = default);
}
