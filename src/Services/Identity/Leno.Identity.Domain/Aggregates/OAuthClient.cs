using Leno.SharedKernel.Abstractions;
using Leno.Identity.Domain.Exceptions;
using Leno.Identity.Domain.ValueObjects;

namespace Leno.Identity.Domain.Aggregates;

/// <summary>
/// OAuth2 客户端配置聚合根，管理第三方身份提供方的客户端参数。
/// ClientSecret 以 AES-256 加密存储，查询时掩码返回。
/// 每个 Provider 唯一，对应一个 OAuthClient 记录。
/// 从 UserAuth BC 迁入 Identity BC（3.6 AuthN/AuthZ 拆分）。
/// 3.7 OAuth/SSO 通用化：扩展 ProviderType / DiscoveryUrl / Scopes / ClaimMappings 字段，
/// 支持配置驱动的任意 OIDC 兼容 IdP 接入。
/// </summary>
public sealed class OAuthClient : AggregateRoot
{
    /// <summary>OAuth2 提供方标识：google / wechat / alipay / 自定义 slug。</summary>
    public string Provider { get; private set; } = string.Empty;

    /// <summary>
    /// 提供方协议类型，决定使用哪个 <c>IOAuth2ProviderAdapter</c> 实现。
    /// 取值：<c>Oidc</c>（标准 OIDC）/ <c>Google</c>（已废弃，建议改用 Oidc + DiscoveryUrl）/
    /// <c>WeChat</c> / <c>Saml2</c>。
    /// 大小写敏感，工厂方法会归一化为 PascalCase 形式（首字母大写）。
    /// </summary>
    public string ProviderType { get; private set; } = string.Empty;

    /// <summary>
    /// OIDC Discovery 端点 URL（.well-known/openid-configuration）。
    /// 仅当 <see cref="ProviderType"/> 为 <c>Oidc</c> 时必填，其它协议类型可空。
    /// </summary>
    public string? DiscoveryUrl { get; private set; }

    /// <summary>第三方平台分配的 ClientId。</summary>
    public string ClientId { get; private set; } = string.Empty;

    /// <summary>AES-256 加密的 ClientSecret（密文）。</summary>
    public string ClientSecret { get; private set; } = string.Empty;

    /// <summary>OAuth2 回调地址。</summary>
    public string RedirectUri { get; private set; } = string.Empty;

    /// <summary>
    /// 请求的 scopes 列表（如 openid/email/profile）。
    /// OIDC 协议至少应包含 openid；非 OIDC 协议可空。
    /// </summary>
    public string[] Scopes { get; private set; } = Array.Empty<string>();

    /// <summary>
    /// Claim 映射规则集合。从 IdP 返回的 source claim 映射到目标 claim。
    /// 空集合表示使用 <see cref="Services.OidcClaimMapping.Default"/>。
    /// </summary>
    public List<ClaimMapping> ClaimMappings { get; private set; } = new();

    /// <summary>是否启用该提供方。</summary>
    public bool Enabled { get; private set; }

    /// <summary>
    /// 已知协议类型的规范名称映射（处理 WeChat 等驼峰复合词）。
    /// 键为大小写不敏感的输入，值为 PascalCase 规范形式。
    /// </summary>
    private static readonly Dictionary<string, string> KnownProviderTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["oidc"] = "Oidc",
        ["google"] = "Google",
        ["wechat"] = "WeChat",
        ["saml2"] = "Saml2"
    };

    /// <summary>EF Core 无参构造。</summary>
    private OAuthClient() { }

    private OAuthClient(Guid id) : base(id) { }

    /// <summary>
    /// 工厂方法，创建 OAuth2 客户端配置。
    /// </summary>
    /// <param name="id">聚合根标识。</param>
    /// <param name="provider">提供方 slug，如 google / wechat / 自定义 IdP 标识。</param>
    /// <param name="providerType">协议类型：Oidc / Google / WeChat / Saml2。</param>
    /// <param name="clientId">第三方平台分配的 ClientId。</param>
    /// <param name="encryptedClientSecret">AES-256 加密的 ClientSecret 密文。</param>
    /// <param name="redirectUri">OAuth2 回调地址。</param>
    /// <param name="scopes">请求的 scopes 列表。</param>
    /// <param name="discoveryUrl">OIDC discovery 端点 URL，OIDC 类型必填。</param>
    /// <param name="claimMappings">Claim 映射规则，空表示使用默认映射。</param>
    /// <param name="enabled">是否启用。</param>
    public static OAuthClient Create(
        Guid id,
        string provider,
        string providerType,
        string clientId,
        string encryptedClientSecret,
        string redirectUri,
        string[]? scopes = null,
        string? discoveryUrl = null,
        List<ClaimMapping>? claimMappings = null,
        bool enabled = true)
    {
        if (id == Guid.Empty)
        {
            throw new IdentityDomainException("OAuth 客户端标识不可为空", "OAUTH_CLIENT_ID_EMPTY");
        }

        ValidateProvider(provider);
        ValidateProviderType(providerType);
        ValidateClientId(clientId);
        ValidateEncryptedSecret(encryptedClientSecret);
        ValidateRedirectUri(redirectUri);
        ValidateDiscoveryUrl(providerType, discoveryUrl);
        ValidateScopes(scopes);

        return new OAuthClient(id)
        {
            Provider = provider.Trim().ToLowerInvariant(),
            ProviderType = NormalizeProviderType(providerType),
            ClientId = clientId.Trim(),
            ClientSecret = encryptedClientSecret,
            RedirectUri = redirectUri.Trim(),
            Scopes = (scopes ?? Array.Empty<string>()).Select(s => s.Trim()).Where(s => s.Length > 0).ToArray(),
            DiscoveryUrl = string.IsNullOrWhiteSpace(discoveryUrl) ? null : discoveryUrl.Trim(),
            ClaimMappings = claimMappings ?? new List<ClaimMapping>(),
            Enabled = enabled
        };
    }

    /// <summary>
    /// 更新 OAuth2 客户端参数。ClientSecret 传入的已是密文。
    /// </summary>
    public void Update(
        string clientId,
        string encryptedClientSecret,
        string redirectUri,
        string[]? scopes = null,
        string? discoveryUrl = null,
        List<ClaimMapping>? claimMappings = null)
    {
        ValidateClientId(clientId);
        ValidateEncryptedSecret(encryptedClientSecret);
        ValidateRedirectUri(redirectUri);
        ValidateDiscoveryUrl(ProviderType, discoveryUrl);
        ValidateScopes(scopes);

        ClientId = clientId.Trim();
        ClientSecret = encryptedClientSecret;
        RedirectUri = redirectUri.Trim();
        Scopes = (scopes ?? Array.Empty<string>()).Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();
        DiscoveryUrl = string.IsNullOrWhiteSpace(discoveryUrl) ? null : discoveryUrl.Trim();
        ClaimMappings = claimMappings ?? new List<ClaimMapping>();
    }

    /// <summary>更新协议类型与 discovery URL（切换 IdP 协议时调用）。</summary>
    public void UpdateProviderType(string providerType, string? discoveryUrl)
    {
        ValidateProviderType(providerType);
        ValidateDiscoveryUrl(providerType, discoveryUrl);

        ProviderType = NormalizeProviderType(providerType);
        DiscoveryUrl = string.IsNullOrWhiteSpace(discoveryUrl) ? null : discoveryUrl.Trim();
    }

    /// <summary>启用该 OAuth2 提供方。</summary>
    public void Enable()
    {
        Enabled = true;
    }

    /// <summary>禁用该 OAuth2 提供方。</summary>
    public void Disable()
    {
        Enabled = false;
    }

    private static void ValidateProvider(string provider)
    {
        if (string.IsNullOrWhiteSpace(provider))
        {
            throw new IdentityDomainException("OAuth2 提供方不可为空", "OAUTH_CLIENT_PROVIDER_EMPTY");
        }

        if (provider.Trim().Length is < 2 or > 32)
        {
            throw new IdentityDomainException("OAuth2 提供方标识长度须为 2-32 字符", "OAUTH_CLIENT_PROVIDER_LENGTH");
        }
    }

    private static void ValidateProviderType(string providerType)
    {
        if (string.IsNullOrWhiteSpace(providerType))
        {
            throw new IdentityDomainException("OAuth2 提供方协议类型不可为空", "OAUTH_CLIENT_PROVIDER_TYPE_EMPTY");
        }

        var normalized = NormalizeProviderType(providerType);
        if (normalized is not ("Oidc" or "Google" or "WeChat" or "Saml2"))
        {
            throw new IdentityDomainException(
                $"不支持的协议类型：{providerType}，仅支持 Oidc / Google / WeChat / Saml2",
                "OAUTH_CLIENT_PROVIDER_TYPE_INVALID");
        }
    }

    private static string NormalizeProviderType(string providerType)
    {
        var trimmed = providerType.Trim();
        if (trimmed.Length == 0)
        {
            return string.Empty;
        }

        if (KnownProviderTypes.TryGetValue(trimmed, out var canonical))
        {
            return canonical;
        }

        // 通用 PascalCase：首字母大写，其余小写（未知类型，校验阶段会拒绝）
        return char.ToUpperInvariant(trimmed[0]) + trimmed[1..].ToLowerInvariant();
    }

    private static void ValidateClientId(string clientId)
    {
        if (string.IsNullOrWhiteSpace(clientId))
        {
            throw new IdentityDomainException("ClientId 不可为空", "OAUTH_CLIENT_CLIENT_ID_EMPTY");
        }

        if (clientId.Trim().Length > 256)
        {
            throw new IdentityDomainException("ClientId 长度不可超过 256 字符", "OAUTH_CLIENT_CLIENT_ID_LENGTH");
        }
    }

    private static void ValidateEncryptedSecret(string encryptedSecret)
    {
        if (string.IsNullOrWhiteSpace(encryptedSecret))
        {
            throw new IdentityDomainException("ClientSecret 不可为空", "OAUTH_CLIENT_SECRET_EMPTY");
        }
    }

    private static void ValidateRedirectUri(string redirectUri)
    {
        if (string.IsNullOrWhiteSpace(redirectUri))
        {
            throw new IdentityDomainException("RedirectUri 不可为空", "OAUTH_CLIENT_REDIRECT_URI_EMPTY");
        }

        if (!Uri.TryCreate(redirectUri, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new IdentityDomainException("RedirectUri 必须为有效的 HTTP/HTTPS 地址", "OAUTH_CLIENT_REDIRECT_URI_FORMAT");
        }
    }

    private static void ValidateDiscoveryUrl(string providerType, string? discoveryUrl)
    {
        if (string.IsNullOrWhiteSpace(discoveryUrl))
        {
            // OIDC 类型必须有 discovery URL
            if (NormalizeProviderType(providerType) == "Oidc")
            {
                throw new IdentityDomainException("OIDC 提供方必须配置 DiscoveryUrl", "OAUTH_CLIENT_DISCOVERY_URL_REQUIRED");
            }
            return;
        }

        if (!Uri.TryCreate(discoveryUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new IdentityDomainException("DiscoveryUrl 必须为有效的 HTTP/HTTPS 地址", "OAUTH_CLIENT_DISCOVERY_URL_FORMAT");
        }
    }

    private static void ValidateScopes(string[]? scopes)
    {
        if (scopes is null)
        {
            return;
        }

        foreach (var scope in scopes)
        {
            var trimmed = scope.Trim();
            if (trimmed.Length == 0)
            {
                // 空白 scope 由赋值阶段过滤，不抛异常（配置驱动，容忍冗余分隔符）
                continue;
            }

            if (trimmed.Length > 128)
            {
                throw new IdentityDomainException("Scope 长度不可超过 128 字符", "OAUTH_CLIENT_SCOPE_LENGTH");
            }
        }
    }
}
