using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using Leno.Identity.Domain.Aggregates;
using Leno.Identity.Domain.Exceptions;
using Leno.Identity.Domain.Services;
using Leno.Identity.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Leno.Identity.Infrastructure.OAuth;

/// <summary>
/// 通用 OIDC 协议适配器（Identity BC，3.7 OAuth/SSO 通用化）。
/// <para>
/// 通过 OAuthClient.DiscoveryUrl 拉取 <c>.well-known/openid-configuration</c>，
/// 获取 authorization_endpoint / token_endpoint / userinfo_endpoint 后构造标准 OIDC 请求。
/// </para>
/// <para>
/// 设计目标：<b>配置驱动</b>。新增任意 OIDC 兼容 IdP（Keycloak / Auth0 / Okta / Azure AD 等），
/// 仅需在 OAuthClient 表插入 ProviderType=Oidc 记录并配置 DiscoveryUrl，无需新增代码。
/// </para>
/// <para>
/// 依赖：<see cref="HttpClient"/>（由 <c>AddHttpClient&lt;OidcProviderAdapter&gt;</c> 注册），
/// <see cref="ILogger{TCategoryName}"/>。Discovery 文档内置内存缓存避免每次请求重复拉取。
/// </para>
/// </summary>
public sealed class OidcProviderAdapter : IOAuth2ProviderAdapter
{
    /// <summary>OIDC Discovery 文档内存缓存时长。Discovery 文档变更频率低，5 分钟足够。</summary>
    private static readonly TimeSpan DiscoveryCacheTtl = TimeSpan.FromMinutes(5);

    /// <summary>OIDC Discovery 文档缓存。Key 为 discovery URL，Value 为 (discovery, 拉取时刻)。</summary>
    /// <remarks>静态字段在多实例间共享，避免每个 OidcProviderAdapter 实例都重复拉取。</remarks>
    private static readonly Dictionary<string, (OidcDiscovery Doc, DateTime FetchedAt)> DiscoveryCache = new(StringComparer.Ordinal);

    /// <summary>静态缓存锁，保证并发拉取串行化但不阻塞读。</summary>
    private static readonly SemaphoreSlim CacheLock = new(1, 1);

    private readonly HttpClient _httpClient;
    private readonly ILogger<OidcProviderAdapter> _logger;

    public string ProviderType => "Oidc";

    public OidcProviderAdapter(HttpClient httpClient, ILogger<OidcProviderAdapter> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<AuthorizationUriResult> BuildAuthorizationUriAsync(
        OAuthClient client,
        string redirectUri,
        string state,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(client);
        if (string.IsNullOrWhiteSpace(redirectUri))
        {
            throw new IdentityDomainException("RedirectUri 不可为空", "OAUTH_REDIRECT_URI_EMPTY");
        }
        if (string.IsNullOrWhiteSpace(state))
        {
            throw new IdentityDomainException("State 不可为空", "OAUTH_STATE_EMPTY");
        }

        var discovery = await GetDiscoveryAsync(client, ct).ConfigureAwait(false);

        // OIDC 授权请求标准参数
        var scopes = client.Scopes.Length > 0 ? client.Scopes : new[] { "openid", "profile", "email" };
        var nonce = GenerateNonce();

        var queryParams = new Dictionary<string, string?>
        {
            ["response_type"] = "code",
            ["client_id"] = client.ClientId,
            ["redirect_uri"] = redirectUri,
            ["scope"] = string.Join(" ", scopes),
            ["state"] = state,
            ["nonce"] = nonce
        };

        var queryString = BuildQueryString(queryParams);
        var authorizationUri = $"{discovery.AuthorizationEndpoint}?{queryString}";

        _logger.LogDebug("构造 OIDC 授权 URL，Provider={Provider}, Endpoint={Endpoint}",
            client.Provider, discovery.AuthorizationEndpoint);

        return new AuthorizationUriResult
        {
            AuthorizationUri = authorizationUri,
            Nonce = nonce,
            State = state
        };
    }

    /// <inheritdoc />
    public async Task<TokenResponse> ExchangeCodeForTokenAsync(
        OAuthClient client,
        string code,
        string redirectUri,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(client);
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new IdentityDomainException("授权码不可为空", "OAUTH_CODE_EMPTY");
        }
        if (string.IsNullOrWhiteSpace(redirectUri))
        {
            throw new IdentityDomainException("RedirectUri 不可为空", "OAUTH_REDIRECT_URI_EMPTY");
        }

        var discovery = await GetDiscoveryAsync(client, ct).ConfigureAwait(false);

        // 标准授权码交换 form-urlencoded 请求体
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
            ["client_id"] = client.ClientId,
            ["client_secret"] = client.ClientSecret
        };

        using var content = new FormUrlEncodedContent(form);
        using var response = await _httpClient.PostAsync(discovery.TokenEndpoint, content, ct)
            .ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("OIDC token 交换失败，Provider={Provider}, StatusCode={StatusCode}, Body={Body}",
                client.Provider, (int)response.StatusCode, body);
            throw new IdentityDomainException(
                $"OIDC token 交换失败：HTTP {(int)response.StatusCode}", "OAUTH_TOKEN_EXCHANGE_FAILED");
        }

        var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        var accessToken = root.TryGetProperty("access_token", out var atEl)
            ? atEl.GetString()
            : null;
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new IdentityDomainException("OIDC 未返回 access_token", "OAUTH_TOKEN_EMPTY");
        }

        return new TokenResponse
        {
            AccessToken = accessToken!,
            TokenType = root.TryGetProperty("token_type", out var ttEl) ? (ttEl.GetString() ?? "Bearer") : "Bearer",
            ExpiresIn = root.TryGetProperty("expires_in", out var eiEl) && eiEl.TryGetInt32(out var ei) ? ei : 0,
            IdToken = root.TryGetProperty("id_token", out var idEl) ? idEl.GetString() : null,
            RefreshToken = root.TryGetProperty("refresh_token", out var rtEl) ? rtEl.GetString() : null,
            Scope = root.TryGetProperty("scope", out var scEl) ? scEl.GetString() : null
        };
    }

    /// <inheritdoc />
    public async Task<UserInfoResponse> GetUserInfoAsync(
        OAuthClient client,
        string accessToken,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(client);
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new IdentityDomainException("AccessToken 不可为空", "OAUTH_ACCESS_TOKEN_EMPTY");
        }

        var discovery = await GetDiscoveryAsync(client, ct).ConfigureAwait(false);

        using var request = new HttpRequestMessage(HttpMethod.Get, discovery.UserInfoEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("OIDC userinfo 拉取失败，Provider={Provider}, StatusCode={StatusCode}, Body={Body}",
                client.Provider, (int)response.StatusCode, body);
            throw new IdentityDomainException(
                $"OIDC userinfo 拉取失败：HTTP {(int)response.StatusCode}", "OAUTH_USERINFO_FAILED");
        }

        var doc = JsonDocument.Parse(body);
        var rawClaims = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            // 跳过嵌套对象与数组，仅提取标量 claim（OIDC userinfo 标准均为标量）
            var value = prop.Value.ValueKind switch
            {
                JsonValueKind.String => prop.Value.GetString(),
                JsonValueKind.Number => prop.Value.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Null => null,
                _ => null
            };

            if (value is not null)
            {
                rawClaims[prop.Name] = value;
            }
        }

        if (!rawClaims.ContainsKey("sub"))
        {
            throw new IdentityDomainException("OIDC userinfo 未返回 sub claim", "OAUTH_USER_ID_EMPTY");
        }

        return new UserInfoResponse
        {
            Endpoint = discovery.UserInfoEndpoint,
            RawClaims = rawClaims
        };
    }

    /// <inheritdoc />
    public Task<ClaimsPrincipal> MapClaimsAsync(
        UserInfoResponse userInfo,
        OidcClaimMapping mapping,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(userInfo);
        ArgumentNullException.ThrowIfNull(mapping);

        // 应用映射规则：SourceClaim → TargetClaim
        var claims = new List<Claim>();
        foreach (var rule in mapping.Mappings)
        {
            if (userInfo.RawClaims.TryGetValue(rule.SourceClaim, out var value) && !string.IsNullOrEmpty(value))
            {
                claims.Add(new Claim(rule.TargetClaim, value));
            }
        }

        // 未映射但常见有用的 claim 也透传一份，便于下游消费
        // （仅当目标 claim 尚未存在时追加，避免覆盖映射结果）
        foreach (var kv in userInfo.RawClaims)
        {
            var claimType = kv.Key;
            if (claims.All(c => !string.Equals(c.Type, claimType, StringComparison.OrdinalIgnoreCase)))
            {
                claims.Add(new Claim(claimType, kv.Value));
            }
        }

        var identity = new ClaimsIdentity(claims, "Oidc", "name", "role");
        return Task.FromResult(new ClaimsPrincipal(identity));
    }

    /// <summary>
    /// 获取（缓存或拉取）OIDC discovery 文档。
    /// 多实例并发拉取同一 discovery URL 时通过 <see cref="CacheLock"/> 串行化，
    /// 避免重复 HTTP 请求；缓存命中时直接返回，无需获取锁。
    /// </summary>
    private async Task<OidcDiscovery> GetDiscoveryAsync(OAuthClient client, CancellationToken ct)
    {
        var discoveryUrl = client.DiscoveryUrl;
        if (string.IsNullOrWhiteSpace(discoveryUrl))
        {
            throw new IdentityDomainException(
                $"OAuthClient {client.Provider} 未配置 DiscoveryUrl，OIDC 协议必须提供 discovery 端点",
                "OAUTH_DISCOVERY_URL_MISSING");
        }

        // 快路径：缓存命中直接返回（无锁）
        if (DiscoveryCache.TryGetValue(discoveryUrl, out var cached)
            && DateTime.UtcNow - cached.FetchedAt < DiscoveryCacheTtl)
        {
            return cached.Doc;
        }

        await CacheLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // 双重检查锁定，避免等待锁期间其它线程已刷新缓存
            if (DiscoveryCache.TryGetValue(discoveryUrl, out cached)
                && DateTime.UtcNow - cached.FetchedAt < DiscoveryCacheTtl)
            {
                return cached.Doc;
            }

            using var response = await _httpClient.GetAsync(discoveryUrl, ct).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("OIDC discovery 拉取失败，Url={Url}, StatusCode={StatusCode}, Body={Body}",
                    discoveryUrl, (int)response.StatusCode, body);
                throw new IdentityDomainException(
                    $"OIDC discovery 拉取失败：HTTP {(int)response.StatusCode}", "OAUTH_DISCOVERY_FAILED");
            }

            var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            var authorizationEndpoint = root.TryGetProperty("authorization_endpoint", out var aeEl)
                ? aeEl.GetString()
                : null;
            var tokenEndpoint = root.TryGetProperty("token_endpoint", out var teEl)
                ? teEl.GetString()
                : null;
            var userInfoEndpoint = root.TryGetProperty("userinfo_endpoint", out var uiEl)
                ? uiEl.GetString()
                : null;

            if (string.IsNullOrWhiteSpace(authorizationEndpoint))
            {
                throw new IdentityDomainException("OIDC discovery 缺少 authorization_endpoint", "OAUTH_DISCOVERY_INVALID");
            }
            if (string.IsNullOrWhiteSpace(tokenEndpoint))
            {
                throw new IdentityDomainException("OIDC discovery 缺少 token_endpoint", "OAUTH_DISCOVERY_INVALID");
            }
            if (string.IsNullOrWhiteSpace(userInfoEndpoint))
            {
                throw new IdentityDomainException("OIDC discovery 缺少 userinfo_endpoint", "OAUTH_DISCOVERY_INVALID");
            }

            var discovery = new OidcDiscovery(
                authorizationEndpoint!,
                tokenEndpoint!,
                userInfoEndpoint!);

            DiscoveryCache[discoveryUrl] = (discovery, DateTime.UtcNow);

            _logger.LogInformation("OIDC discovery 拉取成功，Url={Url}, AuthorizationEndpoint={Ae}",
                discoveryUrl, discovery.AuthorizationEndpoint);

            return discovery;
        }
        finally
        {
            CacheLock.Release();
        }
    }

    /// <summary>构造 query string，对 key 与 value 均做 Uri.EscapeDataString 编码，跳过 null 值。</summary>
    private static string BuildQueryString(Dictionary<string, string?> parameters)
    {
        return string.Join("&", parameters
            .Where(kv => kv.Value is not null)
            .Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value!)}"));
    }

    /// <summary>
    /// 生成符合 OIDC 规范的 nonce（128 位随机数，Base64URL 编码，无填充）。
    /// 用于 mitigate authorization code injection 攻击（OIDC Core §15.5.2）。
    /// </summary>
    private static string GenerateNonce()
    {
        var bytes = new byte[16];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    /// <summary>OIDC discovery 文档摘要，仅保留本适配器需要的三个端点。</summary>
    private sealed record OidcDiscovery(
        string AuthorizationEndpoint,
        string TokenEndpoint,
        string UserInfoEndpoint);
}
