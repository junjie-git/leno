using Leno.Notification.Domain.ValueObjects;

namespace Leno.Notification.Domain.Services;

/// <summary>
/// 频率限制器接口，定义通知发送的频率控制。
/// 由基础设施层实现（如 Redis 滑动窗口）。
/// </summary>
public interface IRateLimiter
{
    /// <summary>
    /// 尝试获取发送许可。
    /// </summary>
    /// <param name="recipient">接收人标识（如 UserId 或邮箱/手机号）。</param>
    /// <param name="templateCode">模板编码。</param>
    /// <param name="channel">通知渠道。</param>
    /// <returns>获取结果，包含是否允许发送及拒绝原因。</returns>
    Task<RateLimitResult> AcquireAsync(string recipient, string templateCode, NotificationChannel channel);
}

/// <summary>
/// 频率限制检查结果。
/// </summary>
public sealed class RateLimitResult
{
    /// <summary>是否允许发送。</summary>
    public bool Allowed { get; set; } = true;

    /// <summary>拒绝原因错误码（如 RATE_LIMITED）。</summary>
    public string? ErrorCode { get; set; }

    /// <summary>拒绝原因描述。</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>当前计数（用于调试）。</summary>
    public int CurrentCount { get; set; }

    /// <summary>限制值。</summary>
    public int Limit { get; set; }

    /// <summary>窗口重置时间（UTC）。</summary>
    public DateTime? ResetAt { get; set; }

    /// <summary>创建允许结果。</summary>
    public static RateLimitResult AllowedResult() => new() { Allowed = true };

    /// <summary>创建拒绝结果。</summary>
    public static RateLimitResult DeniedResult(string errorCode, string errorMessage, int currentCount, int limit, DateTime? resetAt = null)
    {
        return new RateLimitResult
        {
            Allowed = false,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
            CurrentCount = currentCount,
            Limit = limit,
            ResetAt = resetAt
        };
    }
}