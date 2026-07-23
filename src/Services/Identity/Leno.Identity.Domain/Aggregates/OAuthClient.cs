using Leno.SharedKernel.Abstractions;
using Leno.Identity.Domain.Exceptions;

namespace Leno.Identity.Domain.Aggregates;

/// <summary>
/// OAuth2 客户端配置聚合根，管理第三方身份提供方的客户端参数。
/// ClientSecret 以 AES-256 加密存储，查询时掩码返回。
/// 每个 Provider 唯一，对应一个 OAuthClient 记录。
/// 从 UserAuth BC 迁入 Identity BC（3.6 AuthN/AuthZ 拆分）。
/// </summary>
public sealed class OAuthClient : AggregateRoot
{
    /// <summary>OAuth2 提供方标识：google / wechat / alipay。</summary>
    public string Provider { get; private set; } = string.Empty;

    /// <summary>第三方平台分配的 ClientId。</summary>
    public string ClientId { get; private set; } = string.Empty;

    /// <summary>AES-256 加密的 ClientSecret（密文）。</summary>
    public string ClientSecret { get; private set; } = string.Empty;

    /// <summary>OAuth2 回调地址。</summary>
    public string RedirectUri { get; private set; } = string.Empty;

    /// <summary>是否启用该提供方。</summary>
    public bool Enabled { get; private set; }

    /// <summary>EF Core 无参构造。</summary>
    private OAuthClient() { }

    private OAuthClient(Guid id) : base(id) { }

    /// <summary>
    /// 工厂方法，创建 OAuth2 客户端配置。
    /// </summary>
    public static OAuthClient Create(
        Guid id,
        string provider,
        string clientId,
        string encryptedClientSecret,
        string redirectUri,
        bool enabled = true)
    {
        if (id == Guid.Empty)
        {
            throw new IdentityDomainException("OAuth 客户端标识不可为空", "OAUTH_CLIENT_ID_EMPTY");
        }

        ValidateProvider(provider);
        ValidateClientId(clientId);
        ValidateEncryptedSecret(encryptedClientSecret);
        ValidateRedirectUri(redirectUri);

        return new OAuthClient(id)
        {
            Provider = provider.Trim().ToLowerInvariant(),
            ClientId = clientId.Trim(),
            ClientSecret = encryptedClientSecret,
            RedirectUri = redirectUri.Trim(),
            Enabled = enabled
        };
    }

    /// <summary>
    /// 更新 OAuth2 客户端参数。ClientSecret 传入的已是密文。
    /// </summary>
    public void Update(string clientId, string encryptedClientSecret, string redirectUri)
    {
        ValidateClientId(clientId);
        ValidateEncryptedSecret(encryptedClientSecret);
        ValidateRedirectUri(redirectUri);

        ClientId = clientId.Trim();
        ClientSecret = encryptedClientSecret;
        RedirectUri = redirectUri.Trim();
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
}
