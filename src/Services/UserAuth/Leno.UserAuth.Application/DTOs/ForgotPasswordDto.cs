namespace Leno.UserAuth.Application.DTOs;

/// <summary>
/// 忘记密码请求 DTO。
/// </summary>
public sealed class ForgotPasswordDto
{
    /// <summary>账号（邮箱或手机号）。</summary>
    public string Account { get; init; } = string.Empty;
}