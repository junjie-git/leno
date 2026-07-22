namespace Leno.Infrastructure.RateLimiting;

/// <summary>
/// 限流检查结果。
/// </summary>
public sealed class RateLimitResult
{
    /// <summary>是否允许通过。</summary>
    public bool Allowed { get; init; }

    /// <summary>当前窗口内已计数（含本次请求）。</summary>
    public int CurrentCount { get; init; }

    /// <summary>窗口许可上限。</summary>
    public int Limit { get; init; }

    /// <summary>窗口重置时间（UTC），供客户端在 Retry-After 头中使用。</summary>
    public DateTime? ResetAt { get; init; }

    /// <summary>创建允许结果。</summary>
    public static RateLimitResult Acquired(int currentCount, int limit, DateTime? resetAt) => new()
    {
        Allowed = true,
        CurrentCount = currentCount,
        Limit = limit,
        ResetAt = resetAt
    };

    /// <summary>创建拒绝结果。</summary>
    public static RateLimitResult Denied(int currentCount, int limit, DateTime? resetAt) => new()
    {
        Allowed = false,
        CurrentCount = currentCount,
        Limit = limit,
        ResetAt = resetAt
    };
}
