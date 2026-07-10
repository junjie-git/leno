namespace Leno.UserAuth.Application.DTOs;

/// <summary>
/// 修改密码 DTO。
/// </summary>
public sealed class ChangePasswordDto
{
    /// <summary>旧密码。</summary>
    public string OldPassword { get; init; } = string.Empty;

    /// <summary>新密码，8-64 位，至少含字母与数字。</summary>
    public string NewPassword { get; init; } = string.Empty;
}
