using System.Security.Cryptography;
using Leno.Identity.Application.Abstractions;
using Leno.Identity.Application.DTOs;
using Leno.Identity.Domain.Aggregates;
using Leno.Identity.Domain.Exceptions;
using Leno.Identity.Domain.Repositories;
using Leno.Identity.Domain.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leno.Identity.Application.Services;

/// <summary>
/// OAuth2 第三方授权登录应用服务实现（Identity BC，Task A2 补齐）。
/// <para>
/// GetLoginUrlAsync：校验 redirectUri 白名单 → 查找 OAuthClient 配置 → 生成密码学安全 state →
/// 存储 state → 通过适配器构造授权 URL。
/// </para>
/// <para>
/// HandleCallbackAsync：消费 state 校验 CSRF → 获取 redirectUri → 委托
/// <see cref="AuthenticationAppService.HandleOAuthCallbackAsync"/> 完成授权码交换与用户绑定/创建 → 签发令牌。
/// </para>
/// </summary>
public sealed class OAuthService : IOAuthService
{
    /// <summary>OAuth state 默认有效期：5 分钟。</summary>
    private static readonly TimeSpan StateTtl = TimeSpan.FromMinutes(5);

    private readonly IOAuthClientRepository _oauthClientRepository;
    private readonly IOAuth2ProviderFactory _providerFactory;
    private readonly IOAuthStateStore _oauthStateStore;
    private readonly OAuth2Options _oauth2Options;
    private readonly AuthenticationAppService _authenticationAppService;
    private readonly ILogger<OAuthService> _logger;

    public OAuthService(
        IOAuthClientRepository oauthClientRepository,
        IOAuth2ProviderFactory providerFactory,
        IOAuthStateStore oauthStateStore,
        IOptions<OAuth2Options> oauth2Options,
        AuthenticationAppService authenticationAppService,
        ILogger<OAuthService> logger)
    {
        _oauthClientRepository = oauthClientRepository ?? throw new ArgumentNullException(nameof(oauthClientRepository));
        _providerFactory = providerFactory ?? throw new ArgumentNullException(nameof(providerFactory));
        _oauthStateStore = oauthStateStore ?? throw new ArgumentNullException(nameof(oauthStateStore));
        _oauth2Options = oauth2Options?.Value ?? throw new ArgumentNullException(nameof(oauth2Options));
        _authenticationAppService = authenticationAppService ?? throw new ArgumentNullException(nameof(authenticationAppService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<string> GetLoginUrlAsync(string provider, string? redirectUri, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(provider))
        {
            throw new IdentityDomainException("OAuth2 提供方不可为空", "OAUTH_PROVIDER_EMPTY");
        }

        var normalizedProvider = provider.Trim().ToLowerInvariant();
        var oauthClient = await _oauthClientRepository.GetByProviderAsync(normalizedProvider, ct).ConfigureAwait(false);
        if (oauthClient is null)
        {
            throw new IdentityDomainException($"未配置的 OAuth 提供方：{provider}", "OAUTH_CLIENT_NOT_FOUND");
        }

        if (!oauthClient.Enabled)
        {
            throw new IdentityDomainException($"OAuth 提供方已禁用：{provider}", "OAUTH_CLIENT_DISABLED");
        }

        // redirectUri 为空时使用 OAuthClient 默认配置
        var effectiveRedirectUri = string.IsNullOrWhiteSpace(redirectUri)
            ? oauthClient.RedirectUri
            : redirectUri!;

        // 白名单校验，防止开放重定向攻击（P1-8）
        if (_oauth2Options.AllowedRedirectUris.Count > 0
            && !_oauth2Options.AllowedRedirectUris.Contains(effectiveRedirectUri, StringComparer.OrdinalIgnoreCase))
        {
            throw new IdentityDomainException("redirectUri 不在白名单", "OAUTH_REDIRECT_URI_NOT_ALLOWED");
        }

        var adapter = _providerFactory.GetAdapter(oauthClient.ProviderType);

        // 生成密码学安全 state（256 位熵），替代 Guid.NewGuid 防止被预测/碰撞（P2-11）
        var state = GenerateSecureToken(32);

        await _oauthStateStore.StoreAsync(
            state,
            normalizedProvider,
            effectiveRedirectUri,
            StateTtl,
            ct).ConfigureAwait(false);

        var result = await adapter.BuildAuthorizationUriAsync(oauthClient, effectiveRedirectUri, state, ct)
            .ConfigureAwait(false);

        _logger.LogInformation("生成 OAuth 授权 URL，Provider={Provider}, State={State}",
            normalizedProvider, state);

        return result.AuthorizationUri;
    }

    /// <inheritdoc />
    public async Task<TokenDto> HandleCallbackAsync(string provider, string code, string? state, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(provider))
        {
            throw new IdentityDomainException("OAuth2 提供方不可为空", "OAUTH_PROVIDER_EMPTY");
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            throw new IdentityDomainException("授权码不可为空", "OAUTH_CODE_EMPTY");
        }

        if (string.IsNullOrWhiteSpace(state))
        {
            throw new IdentityDomainException("State 参数不可为空", "OAUTH_STATE_EMPTY");
        }

        // 校验并消费 state（原子 GETDEL 防止重放）
        var stateData = await _oauthStateStore.ConsumeAsync(state, ct).ConfigureAwait(false);
        if (stateData is null)
        {
            throw new IdentityDomainException("State 已过期或无效", "OAUTH_STATE_EXPIRED");
        }

        var normalizedProvider = provider.Trim().ToLowerInvariant();

        // 校验 state 内 provider 与 callback provider 一致，防止跨 OAuth 提供方的 CSRF
        if (!string.Equals(stateData.Provider, normalizedProvider, StringComparison.OrdinalIgnoreCase))
        {
            throw new IdentityDomainException("State 与 provider 不匹配", "OAUTH_STATE_PROVIDER_MISMATCH");
        }

        var redirectUri = stateData.RedirectUri;

        _logger.LogInformation("处理 OAuth 回调，Provider={Provider}, State={State}",
            normalizedProvider, state);

        // 委托 AuthenticationAppService 完成授权码交换、用户绑定/创建与令牌签发
        return await _authenticationAppService.HandleOAuthCallbackAsync(
            normalizedProvider, code, redirectUri, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// 生成 URL 安全的密码学安全随机令牌（P2-11）。
    /// 使用 <see cref="RandomNumberGenerator"/> 生成指定字节数的随机数据，
    /// 经 Base64url 编码（去除 padding）适用于 OAuth state、CSRF token 等安全敏感场景。
    /// </summary>
    /// <param name="byteLength">随机字节数，默认 32（256 位熵）。</param>
    /// <returns>URL 安全且无 padding 的 Base64url 编码字符串。</returns>
    private static string GenerateSecureToken(int byteLength = 32)
    {
        if (byteLength <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(byteLength), "字节数必须大于零");
        }

        var bytes = RandomNumberGenerator.GetBytes(byteLength);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace("+", "-")
            .Replace("/", "_");
    }
}
