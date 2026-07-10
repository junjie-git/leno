namespace Leno.UserAuth.Application.DTOs;

/// <summary>
/// 登录/注册成功返回的令牌 DTO。
/// </summary>
public sealed class TokenDto
{
    /// <summary>用户标识。</summary>
    public Guid UserId { get; init; }

    /// <summary>用户名。</summary>
    public string Username { get; init; } = string.Empty;

    /// <summary>访问令牌。</summary>
    public string AccessToken { get; init; } = string.Empty;

    /// <summary>刷新令牌。</summary>
    public string RefreshToken { get; init; } = string.Empty;

    /// <summary>访问令牌有效期（秒）。</summary>
    public int ExpiresIn { get; init; }

    /// <summary>令牌类型，固定 Bearer。</summary>
    public string TokenType { get; init; } = "Bearer";
}
