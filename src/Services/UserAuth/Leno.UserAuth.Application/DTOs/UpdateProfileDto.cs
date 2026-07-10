namespace Leno.UserAuth.Application.DTOs;

/// <summary>
/// 修改个人资料 DTO。
/// </summary>
public sealed class UpdateProfileDto
{
    /// <summary>昵称，1-32 字符。</summary>
    public string Nickname { get; init; } = string.Empty;

    /// <summary>头像 URL（HTTPS），可空。</summary>
    public string? AvatarUrl { get; init; }
}
