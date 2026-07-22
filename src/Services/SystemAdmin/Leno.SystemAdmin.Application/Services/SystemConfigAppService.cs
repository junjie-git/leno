using Leno.SystemAdmin.Application.Abstractions;
using Leno.SystemAdmin.Application.DTOs;
using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Repositories;
using Leno.SystemAdmin.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;

namespace Leno.SystemAdmin.Application.Services;

/// <summary>
/// 系统配置管理应用服务实现。
/// 配置变更经聚合根附加 <see cref="Leno.SystemAdmin.Domain.Events.ConfigChangedEvent"/> 领域事件，
/// 由工作单元的发件箱机制在同一事务内持久化并发布。
/// 写操作后主动失效 Redis 缓存，避免最长 30 分钟脏读。
/// </summary>
public sealed class SystemConfigAppService : ISystemConfigAppService
{
    private const string MaskedValue = "******";

    private readonly ISystemConfigRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISystemConfigCache _cache;
    private readonly ILogger<SystemConfigAppService> _logger;

    public SystemConfigAppService(
        ISystemConfigRepository repository,
        IUnitOfWork unitOfWork,
        ISystemConfigCache cache,
        ILogger<SystemConfigAppService> logger)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(logger);
        _repository = repository;
        _unitOfWork = unitOfWork;
        _cache = cache;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<SystemConfigDto> CreateAsync(SaveSystemConfigDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var configId = Guid.NewGuid();
        var entity = SystemConfig.Create(configId, dto.Key, dto.Value, dto.Group, dto.Description, dto.IsEncrypted);

        await _repository.AddAsync(entity, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);
        await _cache.RemoveAsync(entity.Key, ct);

        _logger.LogInformation("系统配置已创建：{ConfigId}（Key={ConfigKey}）", configId, entity.Key);
        return ToDto(entity);
    }

    /// <inheritdoc />
    public async Task<SystemConfigDto> UpdateAsync(Guid configId, UpdateSystemConfigDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var entity = await RequireConfigAsync(configId, ct);
        entity.Update(dto.Value, dto.Description, dto.IsEncrypted);

        await _repository.UpdateAsync(entity, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);
        await _cache.RemoveAsync(entity.Key, ct);

        _logger.LogInformation("系统配置已更新：{ConfigId}（Key={ConfigKey}）", configId, entity.Key);
        return ToDto(entity);
    }

    /// <inheritdoc />
    public async Task EnableAsync(Guid configId, CancellationToken ct = default)
    {
        var entity = await RequireConfigAsync(configId, ct);
        entity.Enable();

        await _repository.UpdateAsync(entity, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);
        await _cache.RemoveAsync(entity.Key, ct);

        _logger.LogInformation("系统配置已启用：{ConfigId}（Key={ConfigKey}）", configId, entity.Key);
    }

    /// <inheritdoc />
    public async Task DisableAsync(Guid configId, CancellationToken ct = default)
    {
        var entity = await RequireConfigAsync(configId, ct);
        entity.Disable();

        await _repository.UpdateAsync(entity, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);
        await _cache.RemoveAsync(entity.Key, ct);

        _logger.LogInformation("系统配置已停用：{ConfigId}（Key={ConfigKey}）", configId, entity.Key);
    }

    /// <inheritdoc />
    public async Task<SystemConfigDto?> GetByKeyAsync(string key, CancellationToken ct = default)
    {
        var entity = await _repository.GetByKeyAsync(key, ct);
        return entity is null ? null : ToDto(entity);
    }

    /// <inheritdoc />
    public async Task<List<SystemConfigDto>> GetByGroupAsync(string group, CancellationToken ct = default)
    {
        var configs = await _repository.QueryByGroupAsync(group, ct);
        return configs.Select(ToDto).ToList();
    }

    /// <inheritdoc />
    public async Task<SystemConfigListResultDto> QueryAsync(string? key, string? group, ConfigStatus? status, int page, int pageSize, CancellationToken ct = default)
    {
        var items = await _repository.QueryAsync(key, group, status, page, pageSize, ct);
        var total = await _repository.CountAsync(key, group, status, ct);

        return new SystemConfigListResultDto
        {
            Items = items.Select(ToDto).ToList(),
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    /// <inheritdoc />
    public async Task<List<string>> GetDistinctGroupsAsync(CancellationToken ct = default)
    {
        return await _repository.GetDistinctGroupsAsync(ct);
    }

    private async Task<SystemConfig> RequireConfigAsync(Guid configId, CancellationToken ct)
        => await _repository.GetByIdAsync(configId, ct)
           ?? throw new InvalidOperationException($"系统配置 {configId} 不存在");

    private static SystemConfigDto ToDto(SystemConfig entity)
        => new()
        {
            ConfigId = entity.ConfigId,
            Key = entity.Key,
            Value = entity.IsEncrypted ? MaskedValue : entity.Value,
            Group = entity.Group,
            Description = entity.Description,
            IsEncrypted = entity.IsEncrypted,
            Status = entity.Status,
            UpdatedAt = entity.UpdatedAt
        };
}
