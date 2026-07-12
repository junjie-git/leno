namespace Leno.UserAuth.Application.DTOs;

/// <summary>
/// 重置密码请求 DTO。
/// </summary>
public sealed class ResetPasswordDto
{
    /// <summary>重置令牌。</summary>
    public string Token { get; init; } = string.Empty;

    /// <summary>新密码。</summary>
    public string NewPassword { get; init; } = string.Empty;
}