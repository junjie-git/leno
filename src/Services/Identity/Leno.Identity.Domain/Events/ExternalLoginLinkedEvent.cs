using Leno.SharedKernel.Abstractions;

namespace Leno.Identity.Domain.Events;

/// <summary>
/// 外部登录绑定事件。
/// 从 UserAuth BC 迁入 Identity BC（3.6 AuthN/AuthZ 拆分）。
/// </summary>
public sealed class ExternalLoginLinkedEvent : DomainEventBase
{
    public Guid UserId { get; init; }
    public string Provider { get; init; } = string.Empty;
    public string ProviderUserId { get; init; } = string.Empty;

    public ExternalLoginLinkedEvent(Guid userId, string provider, string providerUserId)
        : base(userId)
    {
        UserId = userId;
        Provider = provider;
        ProviderUserId = providerUserId;
    }
}
