using Leno.SharedKernel.Abstractions;

namespace Leno.Identity.Domain.Events;

/// <summary>
/// 用户已认证领域事件（登录成功）。
/// 消费方：审计域、风控域。
/// 从 UserAuth BC 迁入 Identity BC（3.6 AuthN/AuthZ 拆分）。
/// </summary>
public sealed class UserAuthenticatedEvent : DomainEventBase
{
    /// <summary>用户标识。</summary>
    public Guid UserId { get; init; }

    /// <summary>登录方式：Password / OAuth / TwoFactor / RefreshToken。</summary>
    public string AuthMethod { get; init; } = string.Empty;

    /// <summary>登录时间（UTC）。</summary>
    public DateTime AuthenticatedAt { get; init; }

    /// <summary>登录来源 IP，可空。</summary>
    public string? IpAddress { get; init; }

    /// <summary>User-Agent，可空。</summary>
    public string? UserAgent { get; init; }

    public UserAuthenticatedEvent(Guid userId, string authMethod, string? ipAddress = null, string? userAgent = null)
        : base(userId)
    {
        UserId = userId;
        AuthMethod = authMethod;
        AuthenticatedAt = OccurredAt;
        IpAddress = ipAddress;
        UserAgent = userAgent;
    }
}
