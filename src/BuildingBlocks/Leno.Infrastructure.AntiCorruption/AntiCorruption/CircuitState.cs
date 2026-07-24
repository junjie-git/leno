namespace Leno.Infrastructure.AntiCorruption;

/// <summary>熔断器三状态机（M4 双轨方案）。</summary>
public enum CircuitState
{
    /// <summary>正常状态，gRPC 调用全量放行。</summary>
    Closed,

    /// <summary>熔断打开状态，gRPC 调用全部降级到 HttpClient。持续时间由 OpenDuration 决定。</summary>
    Open,

    /// <summary>半开放探测状态，允许少量 gRPC 调用，连续 SuccessThreshold 次成功切 Closed，任一失败切 Open。</summary>
    HalfOpen
}
