using Leno.SharedKernel.Abstractions;

namespace Leno.Identity.Domain.Events;

/// <summary>
/// 用户注册领域事件。
/// 从 UserAuth BC 迁入 Identity BC（3.6 AuthN/AuthZ 拆分）。
/// </summary>
public sealed class UserRegisteredEvent : DomainEventBase
{
    public Guid UserId { get; init; }
    public string Username { get; init; } = string.Empty;
    public string? Email { get; init; }
    public string? PhoneNumber { get; init; }

    public UserRegisteredEvent(Guid userId, string username, string? email, string? phoneNumber)
        : base(userId)
    {
        UserId = userId;
        Username = username;
        Email = email;
        PhoneNumber = phoneNumber;
    }
}
