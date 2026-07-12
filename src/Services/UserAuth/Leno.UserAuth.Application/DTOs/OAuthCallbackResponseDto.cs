namespace Leno.UserAuth.Application.DTOs;

/// <summary>
/// OAuth 回调响应 DTO，包含登录/注册后的令牌。
/// </summary>
public sealed class OAuthCallbackResponseDto
{
    /// <summary>登录/注册后颁发的令牌。</summary>
    public TokenDto Token { get; init; } = default!;
}