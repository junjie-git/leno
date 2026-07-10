namespace Leno.UserAuth.Application.DTOs;

/// <summary>
/// Token 刷新请求 DTO。
/// </summary>
public sealed class RefreshTokenDto
{
    /// <summary>已签发的刷新令牌。</summary>
    public string RefreshToken { get; init; } = string.Empty;
}
