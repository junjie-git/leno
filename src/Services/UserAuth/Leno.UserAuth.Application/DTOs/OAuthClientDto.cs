namespace Leno.UserAuth.Application.DTOs;

/// <summary>
/// OAuth2 客户端配置列表项 DTO，ClientSecret 掩码显示。
/// </summary>
public sealed class OAuthClientDto
{
    /// <summary>OAuth2 提供方标识。</summary>
    public string Provider { get; init; } = string.Empty;

    /// <summary>第三方平台分配的 ClientId。</summary>
    public string ClientId { get; init; } = string.Empty;

    /// <summary>掩码后的 ClientSecret（****）。</summary>
    public string ClientSecret { get; init; } = string.Empty;

    /// <summary>OAuth2 回调地址。</summary>
    public string RedirectUri { get; init; } = string.Empty;

    /// <summary>是否启用。</summary>
    public bool Enabled { get; init; }
}