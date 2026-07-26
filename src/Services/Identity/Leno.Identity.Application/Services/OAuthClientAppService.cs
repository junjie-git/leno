using Leno.Identity.Application.Abstractions;
using Leno.Identity.Application.DTOs;
using Leno.Identity.Domain.Aggregates;
using Leno.Identity.Domain.Exceptions;
using Leno.Identity.Domain.Repositories;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;

namespace Leno.Identity.Application.Services;

/// <summary>
/// OAuth2 客户端配置管理应用服务实现（Identity BC，Task A2 补齐）。
/// <para>
/// 承载 OAuth2 客户端配置的查询、新建、更新与启停用例。
/// ClientSecret 在写入前由 <see cref="IClientSecretEncryptionService"/> 加密为 AES-256 密文存储，
/// 查询时统一掩码返回（仅保留首末 2 字符 + ****），避免明文泄露。
/// </para>
/// <para>
/// 新建默认 <c>Enabled=false</c>，需管理员显式调用 <see cref="EnableAsync"/> 启用，
/// 防止未经验证的 OAuth 提供方配置立即生效。
/// </para>
/// </summary>
public sealed class OAuthClientAppService : IOAuthClientAppService
{
    /// <summary>ClientSecret 掩码后保留的明文前缀长度。</summary>
    private const int SecretMaskPrefixLength = 2;

    /// <summary>ClientSecret 掩码后保留的明文后缀长度。</summary>
    private const int SecretMaskSuffixLength = 2;

    /// <summary>ClientSecret 短于此长度时直接全掩码，避免泄露过多信息。</summary>
    private const int SecretMaskMinLength = 8;

    private readonly IOAuthClientRepository _oauthClientRepository;
    private readonly IClientSecretEncryptionService _secretEncryptionService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<OAuthClientAppService> _logger;

    public OAuthClientAppService(
        IOAuthClientRepository oauthClientRepository,
        IClientSecretEncryptionService secretEncryptionService,
        IUnitOfWork unitOfWork,
        ILogger<OAuthClientAppService> logger)
    {
        _oauthClientRepository = oauthClientRepository ?? throw new ArgumentNullException(nameof(oauthClientRepository));
        _secretEncryptionService = secretEncryptionService ?? throw new ArgumentNullException(nameof(secretEncryptionService));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<OAuthClientDto>> GetAllAsync(CancellationToken ct = default)
    {
        var clients = await _oauthClientRepository.GetAllAsync(ct).ConfigureAwait(false);

        return clients.Select(ToMaskedDto).ToList();
    }

    /// <inheritdoc />
    public async Task CreateAsync(OAuthClientDto request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        ValidateCreateRequest(request);

        var normalizedProvider = request.Provider.Trim().ToLowerInvariant();

        // provider 唯一性校验，已存在则拒绝
        var existing = await _oauthClientRepository.GetByProviderAsync(normalizedProvider, ct)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            throw new IdentityDomainException(
                $"OAuth 提供方 {normalizedProvider} 已存在", "OAUTH_CLIENT_DUPLICATE");
        }

        var encryptedSecret = _secretEncryptionService.Encrypt(request.ClientSecret);

        var client = OAuthClient.Create(
            Guid.NewGuid(),
            normalizedProvider,
            request.ProviderType,
            request.ClientId,
            encryptedSecret,
            request.RedirectUri,
            scopes: request.Scopes.ToArray(),
            discoveryUrl: request.DiscoveryUrl,
            claimMappings: null,
            enabled: false); // 新建默认禁用，需显式启用

        await _oauthClientRepository.AddAsync(client, ct).ConfigureAwait(false);
        await _unitOfWork.SaveEntitiesAsync(ct).ConfigureAwait(false);

        _logger.LogInformation("OAuth 客户端配置创建成功，Provider={Provider}, ProviderType={ProviderType}",
            normalizedProvider, request.ProviderType);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(string provider, OAuthClientDto request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(provider))
        {
            throw new IdentityDomainException("OAuth2 提供方不可为空", "OAUTH_PROVIDER_EMPTY");
        }

        ArgumentNullException.ThrowIfNull(request);

        ValidateUpdateRequest(request);

        var normalizedProvider = provider.Trim().ToLowerInvariant();
        var client = await RequireClientAsync(normalizedProvider, ct).ConfigureAwait(false);

        var encryptedSecret = _secretEncryptionService.Encrypt(request.ClientSecret);

        client.Update(
            request.ClientId,
            encryptedSecret,
            request.RedirectUri,
            scopes: request.Scopes.ToArray(),
            discoveryUrl: request.DiscoveryUrl,
            claimMappings: null);

        // 若请求显式声明 ProviderType，则同步更新协议类型（切换 IdP 协议时使用）
        if (!string.IsNullOrWhiteSpace(request.ProviderType))
        {
            client.UpdateProviderType(request.ProviderType, request.DiscoveryUrl);
        }

        await _oauthClientRepository.UpdateAsync(client, ct).ConfigureAwait(false);
        await _unitOfWork.SaveEntitiesAsync(ct).ConfigureAwait(false);

        _logger.LogInformation("OAuth 客户端配置更新成功，Provider={Provider}", normalizedProvider);
    }

    /// <inheritdoc />
    public async Task EnableAsync(string provider, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(provider))
        {
            throw new IdentityDomainException("OAuth2 提供方不可为空", "OAUTH_PROVIDER_EMPTY");
        }

        var normalizedProvider = provider.Trim().ToLowerInvariant();
        var client = await RequireClientAsync(normalizedProvider, ct).ConfigureAwait(false);

        client.Enable();

        await _oauthClientRepository.UpdateAsync(client, ct).ConfigureAwait(false);
        await _unitOfWork.SaveEntitiesAsync(ct).ConfigureAwait(false);

        _logger.LogInformation("OAuth 客户端已启用，Provider={Provider}", normalizedProvider);
    }

    /// <inheritdoc />
    public async Task DisableAsync(string provider, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(provider))
        {
            throw new IdentityDomainException("OAuth2 提供方不可为空", "OAUTH_PROVIDER_EMPTY");
        }

        var normalizedProvider = provider.Trim().ToLowerInvariant();
        var client = await RequireClientAsync(normalizedProvider, ct).ConfigureAwait(false);

        client.Disable();

        await _oauthClientRepository.UpdateAsync(client, ct).ConfigureAwait(false);
        await _unitOfWork.SaveEntitiesAsync(ct).ConfigureAwait(false);

        _logger.LogInformation("OAuth 客户端已禁用，Provider={Provider}", normalizedProvider);
    }

    private async Task<OAuthClient> RequireClientAsync(string normalizedProvider, CancellationToken ct)
    {
        var client = await _oauthClientRepository.GetByProviderAsync(normalizedProvider, ct)
            .ConfigureAwait(false);
        if (client is null)
        {
            throw new IdentityDomainException(
                $"OAuth 提供方 {normalizedProvider} 不存在", "OAUTH_CLIENT_NOT_FOUND");
        }

        return client;
    }

    private static void ValidateCreateRequest(OAuthClientDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Provider))
        {
            throw new IdentityDomainException("OAuth2 提供方不可为空", "OAUTH_CLIENT_PROVIDER_EMPTY");
        }

        if (string.IsNullOrWhiteSpace(request.ProviderType))
        {
            throw new IdentityDomainException("OAuth2 提供方协议类型不可为空", "OAUTH_CLIENT_PROVIDER_TYPE_EMPTY");
        }

        if (string.IsNullOrWhiteSpace(request.ClientId))
        {
            throw new IdentityDomainException("ClientId 不可为空", "OAUTH_CLIENT_CLIENT_ID_EMPTY");
        }

        if (string.IsNullOrWhiteSpace(request.ClientSecret))
        {
            throw new IdentityDomainException("ClientSecret 不可为空", "OAUTH_CLIENT_SECRET_EMPTY");
        }

        if (string.IsNullOrWhiteSpace(request.RedirectUri))
        {
            throw new IdentityDomainException("RedirectUri 不可为空", "OAUTH_CLIENT_REDIRECT_URI_EMPTY");
        }
    }

    private static void ValidateUpdateRequest(OAuthClientDto request)
    {
        if (string.IsNullOrWhiteSpace(request.ClientId))
        {
            throw new IdentityDomainException("ClientId 不可为空", "OAUTH_CLIENT_CLIENT_ID_EMPTY");
        }

        if (string.IsNullOrWhiteSpace(request.ClientSecret))
        {
            throw new IdentityDomainException("ClientSecret 不可为空", "OAUTH_CLIENT_SECRET_EMPTY");
        }

        if (string.IsNullOrWhiteSpace(request.RedirectUri))
        {
            throw new IdentityDomainException("RedirectUri 不可为空", "OAUTH_CLIENT_REDIRECT_URI_EMPTY");
        }
    }

    /// <summary>
    /// 将聚合根转换为 ClientSecret 已掩码的 DTO，供查询接口返回。
    /// 掩码策略：长度 ≥ 8 时保留首 2 + 末 2 字符 + ****；长度 &lt; 8 时统一返回 ****。
    /// </summary>
    private static OAuthClientDto ToMaskedDto(OAuthClient client)
        => new()
        {
            Provider = client.Provider,
            ProviderType = client.ProviderType,
            DiscoveryUrl = client.DiscoveryUrl,
            ClientId = client.ClientId,
            ClientSecret = MaskSecret(client.ClientSecret),
            RedirectUri = client.RedirectUri,
            Scopes = client.Scopes,
            Enabled = client.Enabled
        };

    /// <summary>
    /// 掩码 ClientSecret 密文，避免明文返回。
    /// 注意：传入的是 AES-256 密文，掩码仅用于展示；如需明文使用应通过 IClientSecretEncryptionService.Decrypt 解密。
    /// </summary>
    private static string MaskSecret(string secret)
    {
        if (string.IsNullOrEmpty(secret))
        {
            return string.Empty;
        }

        if (secret.Length < SecretMaskMinLength)
        {
            return "****";
        }

        var prefix = secret[..SecretMaskPrefixLength];
        var suffix = secret[^SecretMaskSuffixLength..];
        return $"{prefix}****{suffix}";
    }
}
