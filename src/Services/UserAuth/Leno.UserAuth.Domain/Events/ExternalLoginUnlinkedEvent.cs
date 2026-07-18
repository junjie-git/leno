using Leno.SharedKernel.Abstractions;

namespace Leno.UserAuth.Domain.Events;

/// <summary>
/// 外部登录解绑成功领域事件。
/// 消费方：审计域。
/// </summary>
public sealed class ExternalLoginUnlinkedEvent : DomainEventBase
{
    /// <summary>用户标识。</summary>
    public Guid UserId { get; init; }

    /// <summary>OAuth2 提供方标识。</summary>
    public string Provider { get; init; } = string.Empty;

    /// <summary>第三方平台用户唯一标识。</summary>
    public string ProviderUserId { get; init; } = string.Empty;

    /// <summary>解绑时间（UTC）。</summary>
    public DateTime UnlinkedAt { get; init; }

    public ExternalLoginUnlinkedEvent(Guid userId, string provider, string providerUserId)
        : base(userId)
    {
        UserId = userId;
        Provider = provider;
        ProviderUserId = providerUserId;
        UnlinkedAt = OccurredAt;
    }
}
