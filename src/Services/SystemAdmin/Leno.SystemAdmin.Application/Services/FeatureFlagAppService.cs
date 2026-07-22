using Leno.SystemAdmin.Application.DTOs;
using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Repositories;
using Leno.SystemAdmin.Domain.Services;
using Leno.SystemAdmin.Domain.ValueObjects;
using Leno.SystemAdmin.Application.Abstractions;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;

namespace Leno.SystemAdmin.Application.Services;

/// <summary>
/// 特性开关管理应用服务实现。
/// 启停/更新经聚合根附加 <see cref="Leno.SystemAdmin.Domain.Events.FeatureFlagChangedEvent"/> 领域事件，
/// 由工作单元的发件箱机制在同一事务内持久化并发布。
/// 写操作后主动失效 Redis 缓存，避免最长 30 分钟脏读。
/// </summary>
public sealed class FeatureFlagAppService : IFeatureFlagAppService
{
    private readonly IFeatureFlagRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFeatureFlagEvaluator _evaluator;
    private readonly IFeatureFlagCache _cache;
    private readonly ILogger<FeatureFlagAppService> _logger;

    public FeatureFlagAppService(
        IFeatureFlagRepository repository,
        IUnitOfWork unitOfWork,
        IFeatureFlagEvaluator evaluator,
        IFeatureFlagCache cache,
        ILogger<FeatureFlagAppService> logger)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(evaluator);
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(logger);
        _repository = repository;
        _unitOfWork = unitOfWork;
        _evaluator = evaluator;
        _cache = cache;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<FeatureFlagDto> CreateAsync(SaveFeatureFlagDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var flagId = Guid.NewGuid();
        var entity = FeatureFlag.Create(flagId, dto.Key, dto.Name, dto.Description, dto.Strategy, dto.Rules);

        await _repository.AddAsync(entity, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        _logger.LogInformation("特性开关已创建：{FlagId}（Key={FlagKey}）", flagId, entity.Key);
        return ToDto(entity);
    }

    /// <inheritdoc />
    public async Task<FeatureFlagDto> UpdateAsync(Guid flagId, UpdateFeatureFlagDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var entity = await RequireFlagAsync(flagId, ct);
        entity.Update(dto.Name, dto.Description, dto.Strategy, dto.Rules);

        await _repository.UpdateAsync(entity, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);
        await _cache.RemoveAsync(entity.Key, ct);

        _logger.LogInformation("特性开关已更新：{FlagId}（Key={FlagKey}）", flagId, entity.Key);
        return ToDto(entity);
    }

    /// <inheritdoc />
    public async Task EnableAsync(Guid flagId, CancellationToken ct = default)
    {
        var entity = await RequireFlagAsync(flagId, ct);
        entity.Enable();

        await _repository.UpdateAsync(entity, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);
        await _cache.RemoveAsync(entity.Key, ct);

        _logger.LogInformation("特性开关已启用：{FlagId}（Key={FlagKey}）", flagId, entity.Key);
    }

    /// <inheritdoc />
    public async Task DisableAsync(Guid flagId, CancellationToken ct = default)
    {
        var entity = await RequireFlagAsync(flagId, ct);
        entity.Disable();

        await _repository.UpdateAsync(entity, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);
        await _cache.RemoveAsync(entity.Key, ct);

        _logger.LogInformation("特性开关已停用：{FlagId}（Key={FlagKey}）", flagId, entity.Key);
    }

    /// <inheritdoc />
    public async Task<FeatureFlagDto?> GetByKeyAsync(string key, CancellationToken ct = default)
    {
        var entity = await _repository.GetByKeyAsync(key, ct);
        return entity is null ? null : ToDto(entity);
    }

    /// <inheritdoc />
    public async Task<FeatureFlagListResultDto> QueryAsync(string? key, FeatureFlagStatus? status, int page, int pageSize, CancellationToken ct = default)
    {
        var items = await _repository.QueryAsync(key, status, page, pageSize, ct);
        var total = await _repository.CountAsync(key, status, ct);

        return new FeatureFlagListResultDto
        {
            Items = items.Select(ToDto).ToList(),
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    /// <inheritdoc />
    public async Task<bool> EvaluateAsync(EvaluateFlagDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return await _evaluator.EvaluateAsync(dto.FlagKey, dto.Context, ct);
    }

    private async Task<FeatureFlag> RequireFlagAsync(Guid flagId, CancellationToken ct)
        => await _repository.GetByIdAsync(flagId, ct)
           ?? throw new InvalidOperationException($"特性开关 {flagId} 不存在");

    private static FeatureFlagDto ToDto(FeatureFlag entity)
        => new()
        {
            FlagId = entity.FlagId,
            Key = entity.Key,
            Name = entity.Name,
            Description = entity.Description,
            IsEnabled = entity.IsEnabled,
            Strategy = entity.Strategy,
            Rules = entity.Rules,
            UpdatedAt = entity.UpdatedAt
        };
}
