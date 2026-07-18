using Leno.SharedKernel.Abstractions;

namespace Leno.UserAuth.Domain.Events;

/// <summary>
/// 用户请求密码重置领域事件。
/// 消费方：消息通知域（发送验证码/重置链接）。
/// </summary>
public sealed class ForgotPasswordRequestedEvent : DomainEventBase
{
    /// <summary>用户标识。</summary>
    public Guid UserId { get; init; }

    /// <summary>用户邮箱（用于发送重置链接）。</summary>
    public string? Email { get; init; }

    /// <summary>用户手机号（用于发送短信验证码）。</summary>
    public string? PhoneNumber { get; init; }

    /// <summary>重置令牌（Redis 存储，仅用于通知中携带）。</summary>
    public string ResetToken { get; init; } = string.Empty;

    /// <summary>请求时间（UTC）。</summary>
    public DateTime RequestedAt { get; init; }

    public ForgotPasswordRequestedEvent(Guid userId, string? email, string? phoneNumber, string resetToken)
        : base(userId)
    {
        UserId = userId;
        Email = email;
        PhoneNumber = phoneNumber;
        ResetToken = resetToken;
        RequestedAt = OccurredAt;
    }
}
