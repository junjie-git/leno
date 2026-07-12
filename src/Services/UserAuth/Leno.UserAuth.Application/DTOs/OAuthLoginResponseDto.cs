namespace Leno.UserAuth.Application.DTOs;

/// <summary>
/// OAuth 登录授权 URL 响应 DTO。
/// 前端收到此响应后可跳转至第三方授权页面。
/// </summary>
public sealed class OAuthLoginResponseDto
{
    /// <summary>第三方授权页面 URL。</summary>
    public string AuthorizationUrl { get; init; } = string.Empty;
}