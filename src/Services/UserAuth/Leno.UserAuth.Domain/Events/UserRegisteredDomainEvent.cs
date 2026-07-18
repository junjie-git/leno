using Leno.SharedKernel.Abstractions;

namespace Leno.UserAuth.Domain.Events;

/// <summary>
/// 用户注册成功领域事件，由 User 聚合在创建账户时收集。
/// mapper 翻译为 <see cref="Leno.SharedContracts.Events.UserRegisteredEvent"/> 集成事件对外发布。
/// </summary>
public sealed class UserRegisteredDomainEvent : DomainEventBase
{
    /// <summary>注册用户标识。</summary>
    public Guid UserId { get; init; }

    /// <summary>用户名。</summary>
    public string Username { get; init; } = string.Empty;

    /// <summary>注册邮箱（OAuth 注册可空）。</summary>
    public string? Email { get; init; }

    /// <summary>注册手机号（OAuth 注册可空）。</summary>
    public string? PhoneNumber { get; init; }

    /// <summary>注册时间（UTC）。</summary>
    public DateTime RegisteredAt { get; init; }

    public UserRegisteredDomainEvent(Guid userId, string username, string? email, string? phoneNumber)
        : base(userId)
    {
        UserId = userId;
        Username = username;
        Email = email;
        PhoneNumber = phoneNumber;
        RegisteredAt = OccurredAt;
    }
}
