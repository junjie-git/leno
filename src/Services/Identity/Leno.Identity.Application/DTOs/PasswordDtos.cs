namespace Leno.Identity.Application.DTOs;

/// <summary>
/// 忘记密码请求 DTO（Identity BC）。
/// </summary>
public sealed class ForgotPasswordDto
{
    /// <summary>账号（邮箱或手机号）。</summary>
    public string Account { get; init; } = string.Empty;
}

/// <summary>
/// 重置密码请求 DTO（Identity BC）。
/// </summary>
public sealed class ResetPasswordDto
{
    /// <summary>重置令牌。</summary>
    public string Token { get; init; } = string.Empty;

    /// <summary>新密码。</summary>
    public string NewPassword { get; init; } = string.Empty;
}
