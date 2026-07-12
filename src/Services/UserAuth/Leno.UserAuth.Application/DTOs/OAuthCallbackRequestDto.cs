namespace Leno.UserAuth.Application.DTOs;

/// <summary>
/// OAuth 回调请求 DTO，由前端从第三方授权回调 URL 的 query string 中提取后提交。
/// </summary>
public sealed class OAuthCallbackRequestDto
{
    /// <summary>第三方返回的授权码。</summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>防 CSRF 的 state 参数。</summary>
    public string State { get; init; } = string.Empty;
}