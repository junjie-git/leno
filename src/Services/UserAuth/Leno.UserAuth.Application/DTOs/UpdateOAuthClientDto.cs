namespace Leno.UserAuth.Application.DTOs;

/// <summary>
/// 更新 OAuth2 客户端配置请求 DTO。
/// ClientSecret 传入明文，由应用层加密后存储。
/// </summary>
public sealed class UpdateOAuthClientDto
{
    /// <summary>第三方平台分配的 ClientId。</summary>
    public string ClientId { get; init; } = string.Empty;

    /// <summary>第三方平台分配的 ClientSecret（明文）。</summary>
    public string ClientSecret { get; init; } = string.Empty;

    /// <summary>OAuth2 回调地址。</summary>
    public string RedirectUri { get; init; } = string.Empty;
}