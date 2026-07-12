namespace Leno.SystemAdmin.Domain.Services;

/// <summary>
/// 限流计数器接口，定义在领域层，由基础设施层实现。
/// 基于原子计数实现固定窗口限流。
/// </summary>
public interface IRateLimitCounter
{
    /// <summary>
    /// 检查是否超过限流阈值，并原子递增计数。
    /// 如果未超过限制，返回 true 并递增计数；如果超过限制，返回 false 不递增。
    /// </summary>
    /// <param name="key">限流键，如 "api:login:user123"。</param>
    /// <param name="limit">窗口内最大请求数。</param>
    /// <param name="windowSeconds">时间窗口（秒）。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>true 表示未超限（请求允许），false 表示已超限（请求被限流）。</returns>
    Task<bool> CheckAndIncrementAsync(string key, int limit, int windowSeconds, CancellationToken ct = default);

    /// <summary>
    /// 获取指定键的当前计数。
    /// </summary>
    /// <param name="key">限流键。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>当前窗口内的请求计数。</returns>
    Task<long> GetCountAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// 重置指定键的计数。
    /// </summary>
    /// <param name="key">限流键。</param>
    /// <param name="ct">取消令牌。</param>
    Task ResetAsync(string key, CancellationToken ct = default);
}