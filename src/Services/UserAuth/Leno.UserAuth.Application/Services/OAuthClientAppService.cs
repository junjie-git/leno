using Leno.SharedKernel.Abstractions;
using Leno.UserAuth.Application.Abstractions;
using Leno.UserAuth.Application.DTOs;
using Leno.UserAuth.Domain.Aggregates;
using Leno.UserAuth.Domain.Exceptions;
using Leno.UserAuth.Domain.Repositories;

namespace Leno.UserAuth.Application.Services;

/// <summary>
/// OAuth2 客户端配置管理应用服务实现。
/// ClientSecret 加密存储，返回时掩码。
/// </summary>
public sealed class OAuthClientAppService : IOAuthClientAppService
{
    private readonly IOAuthClientRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClientSecretEncryptionService? _encryptionService;

    public OAuthClientAppService(
        IOAuthClientRepository repository,
        IUnitOfWork unitOfWork,
        IClientSecretEncryptionService? encryptionService = null)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        _repository = repository;
        _unitOfWork = unitOfWork;
        _encryptionService = encryptionService;
    }

    public async Task<IReadOnlyList<OAuthClientDto>> GetAllAsync(CancellationToken ct = default)
    {
        var clients = await _repository.GetAllAsync(ct);
        return clients.Select(MapToDto).ToList();
    }

    public async Task UpdateAsync(string provider, UpdateOAuthClientDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(provider))
        {
            throw new UserAuthDomainException("OAuth2 提供方不可为空", "OAUTH_PROVIDER_EMPTY");
        }

        var normalizedProvider = provider.Trim().ToLowerInvariant();
        var client = await _repository.GetByProviderAsync(normalizedProvider, ct);

        if (client is null)
        {
            // 不存在则创建
            var encryptedSecret = GetEncryptedSecret(dto.ClientSecret);
            client = OAuthClient.Create(
                Guid.NewGuid(),
                normalizedProvider,
                dto.ClientId,
                encryptedSecret,
                dto.RedirectUri);
            await _repository.AddAsync(client, ct);
        }
        else
        {
            var encryptedSecret = GetEncryptedSecret(dto.ClientSecret);
            client.Update(dto.ClientId, encryptedSecret, dto.RedirectUri);
            await _repository.UpdateAsync(client, ct);
        }

        await _unitOfWork.SaveEntitiesAsync(ct);
    }

    public async Task EnableAsync(string provider, CancellationToken ct = default)
    {
        var client = await GetClientOrThrowAsync(provider, ct);
        client.Enable();
        await _repository.UpdateAsync(client, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);
    }

    public async Task DisableAsync(string provider, CancellationToken ct = default)
    {
        var client = await GetClientOrThrowAsync(provider, ct);
        client.Disable();
        await _repository.UpdateAsync(client, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);
    }

    private async Task<OAuthClient> GetClientOrThrowAsync(string provider, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(provider))
        {
            throw new UserAuthDomainException("OAuth2 提供方不可为空", "OAUTH_PROVIDER_EMPTY");
        }

        var normalizedProvider = provider.Trim().ToLowerInvariant();
        var client = await _repository.GetByProviderAsync(normalizedProvider, ct);
        if (client is null)
        {
            throw new UserAuthDomainException(
                $"OAuth2 提供方 {provider} 未配置", "OAUTH_CLIENT_NOT_FOUND");
        }

        return client;
    }

    private string GetEncryptedSecret(string plainSecret)
    {
        if (_encryptionService is null)
        {
            throw new InvalidOperationException("AES 加密服务未配置，无法加密 ClientSecret。请配置 OAuth2:AesKey。");
        }

        return _encryptionService.Encrypt(plainSecret);
    }

    private static OAuthClientDto MapToDto(OAuthClient client)
    {
        return new OAuthClientDto
        {
            Provider = client.Provider,
            ClientId = client.ClientId,
            ClientSecret = MaskSecret(client.ClientSecret),
            RedirectUri = client.RedirectUri,
            Enabled = client.Enabled
        };
    }

    private static string MaskSecret(string secret)
    {
        if (string.IsNullOrEmpty(secret))
        {
            return "****";
        }

        return "****";
    }
}