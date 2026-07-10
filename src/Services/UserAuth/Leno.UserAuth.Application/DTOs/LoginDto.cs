namespace Leno.UserAuth.Application.DTOs;

/// <summary>
/// 登录请求 DTO，账号可为用户名、邮箱或手机号。
/// </summary>
public sealed class LoginDto
{
    /// <summary>登录账号（用户名/邮箱/手机号）。</summary>
    public string Account { get; init; } = string.Empty;

    /// <summary>明文密码。</summary>
    public string Password { get; init; } = string.Empty;
}
