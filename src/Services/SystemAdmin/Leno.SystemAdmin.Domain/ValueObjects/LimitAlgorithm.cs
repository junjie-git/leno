namespace Leno.SystemAdmin.Domain.ValueObjects;

/// <summary>
/// 限流算法枚举，决定接口限流的具体计算方式。
/// </summary>
public enum LimitAlgorithm
{
    /// <summary>固定窗口：在固定时间窗口内计数，窗口边界可能产生突发流量。</summary>
    FixedWindow = 0,

    /// <summary>滑动窗口：按细分时间窗口平滑计数，更精确地限制流量。</summary>
    SlidingWindow = 1,

    /// <summary>令牌桶：以恒定速率补充令牌，允许一定程度的突发流量。</summary>
    TokenBucket = 2
}