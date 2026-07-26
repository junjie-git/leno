namespace Leno.Identity.Application.DTOs;

/// <summary>
/// 修改个人资料 DTO（Identity BC）。
/// 从 UserAuth BC 迁入 Identity BC（3.6 AuthN/AuthZ 拆分）。
/// </summary>
public sealed class UpdateProfileDto
{
    /// <summary>昵称，1-32 字符。</summary>
    public string Nickname { get; init; } = string.Empty;

    /// <summary>头像 URL（HTTPS），可空。</summary>
    public string? AvatarUrl { get; init; }
}

/// <summary>
/// 修改密码 DTO（Identity BC）。
/// </summary>
public sealed class ChangePasswordDto
{
    /// <summary>旧密码。</summary>
    public string OldPassword { get; init; } = string.Empty;

    /// <summary>新密码，8-64 位，至少含字母与数字。</summary>
    public string NewPassword { get; init; } = string.Empty;
}
