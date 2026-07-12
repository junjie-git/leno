using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;

namespace Leno.UserAuth.Domain.Events;

/// <summary>
/// 外部登录解绑成功集成事件。
/// 消费方：审计域。
/// </summary>
public sealed class ExternalLoginUnlinkedEvent : IntegrationEventBase, IDomainEvent
{
    /// <summary>用户标识。</summary>
    public Guid UserId { get; init; }

    /// <summary>OAuth2 提供方标识。</summary>
    public string Provider { get; init; } = string.Empty;

    /// <summary>第三方平台用户唯一标识。</summary>
    public string ProviderUserId { get; init; } = string.Empty;

    /// <summary>解绑时间（UTC）。</summary>
    public DateTime UnlinkedAt { get; init; }

    /// <summary>聚合根标识。</summary>
    public Guid AggregateId => UserId;

    /// <summary>供 System.Text.Json 反序列化使用的无参构造。</summary>
    public ExternalLoginUnlinkedEvent() : base()
    {
    }

    public ExternalLoginUnlinkedEvent(Guid userId, string provider, string providerUserId)
        : base()
    {
        UserId = userId;
        Provider = provider;
        ProviderUserId = providerUserId;
        UnlinkedAt = OccurredAt;
    }
}