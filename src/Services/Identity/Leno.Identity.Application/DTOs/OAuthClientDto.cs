namespace Leno.Identity.Application.DTOs;

/// <summary>
/// OAuth2 客户端配置 DTO（Identity BC，3.7 OAuth/SSO 通用化）。
/// 承载新建/更新/查询 OAuth2 客户端配置的数据契约。
/// ClientSecret 在写入前由应用层加密，查询时掩码返回。
/// </summary>
public sealed class OAuthClientDto
{
    /// <summary>OAuth2 提供方标识（如 google / wechat / 自定义 slug）。</summary>
    public string Provider { get; init; } = string.Empty;

    /// <summary>
    /// 提供方协议类型：Oidc / Google / WeChat / Saml2。
    /// 新建时必填，决定使用哪个 IOAuth2ProviderAdapter 实现。
    /// </summary>
    public string ProviderType { get; init; } = "Oidc";

    /// <summary>OIDC Discovery 端点 URL，ProviderType=Oidc 时必填。</summary>
    public string? DiscoveryUrl { get; init; }

    /// <summary>第三方平台分配的 ClientId。</summary>
    public string ClientId { get; init; } = string.Empty;

    /// <summary>
    /// 第三方平台分配的 ClientSecret。
    /// 写入时为明文（应用层加密后存储）；查询时为掩码（****）。
    /// </summary>
    public string ClientSecret { get; init; } = string.Empty;

    /// <summary>OAuth2 回调地址。</summary>
    public string RedirectUri { get; init; } = string.Empty;

    /// <summary>请求的 scopes 列表（如 openid/email/profile）。</summary>
    public IReadOnlyList<string> Scopes { get; init; } = Array.Empty<string>();

    /// <summary>是否启用。</summary>
    public bool Enabled { get; init; }
}
