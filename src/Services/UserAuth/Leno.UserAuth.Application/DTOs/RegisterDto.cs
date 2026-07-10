namespace Leno.UserAuth.Application.DTOs;

/// <summary>
/// 注册请求 DTO，账号为用户名，可选邮箱或手机号作为登录凭证。
/// </summary>
public sealed class RegisterDto
{
    /// <summary>用户名，3-32 字符，仅字母、数字与下划线。</summary>
    public string Username { get; init; } = string.Empty;

    /// <summary>邮箱（二选一），RFC 5322 格式。</summary>
    public string? Email { get; init; }

    /// <summary>手机号（二选一），E.164 格式。</summary>
    public string? PhoneNumber { get; init; }

    /// <summary>密码，8-64 位，至少含字母与数字。</summary>
    public string Password { get; init; } = string.Empty;

    /// <summary>昵称，1-32 字符。</summary>
    public string Nickname { get; init; } = string.Empty;

    /// <summary>头像 URL（HTTPS），可空。</summary>
    public string? AvatarUrl { get; init; }
}
