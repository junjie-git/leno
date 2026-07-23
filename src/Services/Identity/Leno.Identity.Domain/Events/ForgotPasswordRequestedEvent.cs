using Leno.SharedKernel.Abstractions;

namespace Leno.Identity.Domain.Events;

/// <summary>
/// 忘记密码请求事件，触发重置链接/验证码下发。
/// 从 UserAuth BC 迁入 Identity BC（3.6 AuthN/AuthZ 拆分）。
/// </summary>
public sealed class ForgotPasswordRequestedEvent : DomainEventBase
{
    public Guid UserId { get; init; }
    public string? Email { get; init; }
    public string? PhoneNumber { get; init; }
    public string ResetToken { get; init; } = string.Empty;

    public ForgotPasswordRequestedEvent(Guid userId, string? email, string? phoneNumber, string resetToken)
        : base(userId)
    {
        UserId = userId;
        Email = email;
        PhoneNumber = phoneNumber;
        ResetToken = resetToken;
    }
}
