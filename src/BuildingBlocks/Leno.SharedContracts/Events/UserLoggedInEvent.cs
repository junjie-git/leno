namespace Leno.SharedContracts.Events;

/// <summary>
/// 用户登录完成事件：由 Identity 在登录成功或失败后发布，SystemAdmin.LoginLogConsumer 消费写入 LoginLog 聚合。
/// 仅携带原始 UserAgent 字符串，UA 解析在 SystemAdmin 消费者侧完成，保持事件契约精简。
/// </summary>
public sealed record UserLoggedInEvent
{
    /// <summary>事件唯一标识，用于幂等去重。</summary>
    public Guid EventId { get; init; } = Guid.NewGuid();

    /// <summary>事件发生时间（UTC）。</summary>
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;

    /// <summary>登录用户名（用于失败登录时仍可记录）。</summary>
    public string Username { get; init; } = string.Empty;

    /// <summary>用户标识，失败登录时为 null。</summary>
    public Guid? UserId { get; init; }

    /// <summary>登录来源 IP 地址。</summary>
    public string IpAddress { get; init; } = string.Empty;

    /// <summary>原始 User-Agent 字符串（不在 Identity 端解析）。</summary>
    public string UserAgent { get; init; } = string.Empty;

    /// <summary>Referer URL，可空。</summary>
    public string? RefererUrl { get; init; }

    /// <summary>链路追踪标识。</summary>
    public string TraceId { get; init; } = string.Empty;

    /// <summary>登录耗时（毫秒）。</summary>
    public int DurationMs { get; init; }

    /// <summary>是否登录成功。</summary>
    public bool Success { get; init; }

    /// <summary>登录失败原因（Success=false 时必填）。</summary>
    public string? FailureReason { get; init; }
}
