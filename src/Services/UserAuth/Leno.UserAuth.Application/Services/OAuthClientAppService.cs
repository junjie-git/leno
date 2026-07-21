using System.Text.Json;
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
/// 写操作在事务内写入 <see cref="AuditLog"/> 审计日志，确保 OAuth 提供方配置变更可追溯。
/// </summary>
public sealed class OAuthClientAppService : IOAuthClientAppService
{
    private readonly IOAuthClientRepository _repository;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClientSecretEncryptionService? _encryptionService;

    public OAuthClientAppService(
        IOAuthClientRepository repository,
        IAuditLogRepository auditLogRepository,
        IUnitOfWork unitOfWork,
        IClientSecretEncryptionService? encryptionService = null)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(auditLogRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        _repository = repository;
        _auditLogRepository = auditLogRepository;
        _unitOfWork = unitOfWork;
        _encryptionService = encryptionService;
    }

    public async Task<IReadOnlyList<OAuthClientDto>> GetAllAsync(CancellationToken ct = default)
    {
        var clients = await _repository.GetAllAsync(ct);
        return clients.Select(MapToDto).ToList();
    }

    public async Task CreateAsync(string provider, UpdateOAuthClientDto dto, Guid operatorId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(provider))
        {
            throw new UserAuthDomainException("OAuth2 提供方不可为空", "OAUTH_PROVIDER_EMPTY");
        }

        var normalizedProvider = provider.Trim().ToLowerInvariant();
        var existing = await _repository.GetByProviderAsync(normalizedProvider, ct);
        if (existing is not null)
        {
            throw new UserAuthDomainException(
                $"OAuth2 提供方 {provider} 已存在", "OAUTH_CLIENT_ALREADY_EXISTS");
        }

        var encryptedSecret = GetEncryptedSecret(dto.ClientSecret);
        // 新建默认 Enabled=false，避免误传未校验的 provider 自动启用污染 OAuth 解析器。
        var client = OAuthClient.Create(
            Guid.NewGuid(),
            normalizedProvider,
            dto.ClientId,
            encryptedSecret,
            dto.RedirectUri,
            enabled: false);
        await _repository.AddAsync(client, ct);
        await WriteAuditAsync("OAuthClientCreate", operatorId, client.Id, null, Snapshot(client), ct);
        await _unitOfWork.SaveEntitiesAsync(ct);
    }

    public async Task UpdateAsync(string provider, UpdateOAuthClientDto dto, Guid operatorId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(provider))
        {
            throw new UserAuthDomainException("OAuth2 提供方不可为空", "OAUTH_PROVIDER_EMPTY");
        }

        var normalizedProvider = provider.Trim().ToLowerInvariant();
        var client = await _repository.GetByProviderAsync(normalizedProvider, ct);
        if (client is null)
        {
            // PUT 严格幂等：不存在则抛异常，不自动创建
            throw new UserAuthDomainException(
                $"OAuth2 提供方 {provider} 未配置，请先调用 CreateAsync", "OAUTH_CLIENT_NOT_FOUND");
        }

        var beforeSnapshot = Snapshot(client);
        var encryptedSecret = GetEncryptedSecret(dto.ClientSecret);
        client.Update(dto.ClientId, encryptedSecret, dto.RedirectUri);
        await _repository.UpdateAsync(client, ct);

        await WriteAuditAsync("OAuthClientUpdate", operatorId, client.Id, beforeSnapshot, Snapshot(client), ct);
        await _unitOfWork.SaveEntitiesAsync(ct);
    }

    public async Task EnableAsync(string provider, Guid operatorId, CancellationToken ct = default)
    {
        var client = await GetClientOrThrowAsync(provider, ct);
        var before = Snapshot(client);
        client.Enable();
        await _repository.UpdateAsync(client, ct);
        await WriteAuditAsync("OAuthClientEnable", operatorId, client.Id, before, Snapshot(client), ct);
        await _unitOfWork.SaveEntitiesAsync(ct);
    }

    public async Task DisableAsync(string provider, Guid operatorId, CancellationToken ct = default)
    {
        var client = await GetClientOrThrowAsync(provider, ct);
        var before = Snapshot(client);
        client.Disable();
        await _repository.UpdateAsync(client, ct);
        await WriteAuditAsync("OAuthClientDisable", operatorId, client.Id, before, Snapshot(client), ct);
        await _unitOfWork.SaveEntitiesAsync(ct);
    }

    private async Task WriteAuditAsync(
        string action,
        Guid operatorId,
        Guid resourceId,
        string? beforeSnapshot,
        string? afterSnapshot,
        CancellationToken ct)
    {
        var auditLog = AuditLog.Create(
            Guid.NewGuid(),
            operatorId,
            action,
            "OAuthClient",
            resourceId.ToString(),
            beforeSnapshot,
            afterSnapshot);

        await _auditLogRepository.AddAsync(auditLog, ct);
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

    private static string Snapshot(OAuthClient client)
        => JsonSerializer.Serialize(new
        {
            client.Id,
            client.Provider,
            client.ClientId,
            client.RedirectUri,
            client.Enabled
        });

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
        // 短密钥（含空值与小于 8 字符）无足够信息量可保留，统一返回 "****" 防止泄露部分字符被猜测。
        // 长密钥保留前 4 与后 4 字符，便于管理员核对配置但不暴露完整密钥。
        if (string.IsNullOrEmpty(secret) || secret.Length < 8)
        {
            return "****";
        }

        return $"{secret[..4]}****{secret[^4..]}";
    }
}
