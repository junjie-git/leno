using Leno.SystemAdmin.Application.DTOs;
using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Repositories;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;

namespace Leno.SystemAdmin.Application.Services;

/// <summary>
/// 限流规则管理应用服务实现。
/// </summary>
public sealed class RateLimitRuleAppService : IRateLimitRuleAppService
{
    private readonly IRateLimitRuleRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RateLimitRuleAppService> _logger;

    public RateLimitRuleAppService(
        IRateLimitRuleRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<RateLimitRuleAppService> logger)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(logger);
        _repository = repository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<RateLimitRuleListResultDto> QueryAsync(string? targetApi, bool? enabled, int page, int pageSize, CancellationToken ct = default)
    {
        var items = await _repository.QueryAsync(targetApi, enabled, page, pageSize, ct);
        var total = await _repository.CountAsync(targetApi, enabled, ct);

        return new RateLimitRuleListResultDto
        {
            Items = items.Select(ToDto).ToList(),
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    /// <inheritdoc />
    public async Task<RateLimitRuleDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var rule = await _repository.GetByIdAsync(id, ct);
        return rule is null ? null : ToDto(rule);
    }

    /// <inheritdoc />
    public async Task<RateLimitRuleDto> CreateAsync(SaveRateLimitRuleDto dto, CancellationToken ct = default)
    {
        var rule = RateLimitRule.Create(
            Guid.NewGuid(),
            dto.TargetApi,
            dto.TargetContext,
            dto.Limit,
            dto.WindowSeconds,
            dto.Algorithm,
            dto.Scope);

        await _repository.AddAsync(rule, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        _logger.LogInformation("限流规则已创建 RuleId={RuleId} TargetApi={TargetApi}", rule.Id, rule.TargetApi);
        return ToDto(rule);
    }

    /// <inheritdoc />
    public async Task<RateLimitRuleDto> UpdateAsync(Guid id, SaveRateLimitRuleDto dto, CancellationToken ct = default)
    {
        var rule = await _repository.GetByIdAsync(id, ct);
        if (rule is null)
        {
            throw new InvalidOperationException($"限流规则不存在 Id={id}");
        }

        rule.Update(
            dto.TargetApi,
            dto.TargetContext,
            dto.Limit,
            dto.WindowSeconds,
            dto.Algorithm,
            dto.Scope);

        await _repository.UpdateAsync(rule, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        _logger.LogInformation("限流规则已更新 RuleId={RuleId}", rule.Id);
        return ToDto(rule);
    }

    /// <inheritdoc />
    public async Task EnableAsync(Guid id, CancellationToken ct = default)
    {
        var rule = await _repository.GetByIdAsync(id, ct);
        if (rule is null)
        {
            throw new InvalidOperationException($"限流规则不存在 Id={id}");
        }

        rule.Enable();
        await _repository.UpdateAsync(rule, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        _logger.LogInformation("限流规则已启用 RuleId={RuleId}", rule.Id);
    }

    /// <inheritdoc />
    public async Task DisableAsync(Guid id, CancellationToken ct = default)
    {
        var rule = await _repository.GetByIdAsync(id, ct);
        if (rule is null)
        {
            throw new InvalidOperationException($"限流规则不存在 Id={id}");
        }

        rule.Disable();
        await _repository.UpdateAsync(rule, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        _logger.LogInformation("限流规则已停用 RuleId={RuleId}", rule.Id);
    }

    private static RateLimitRuleDto ToDto(RateLimitRule entity)
        => new()
        {
            RuleId = entity.RuleId,
            TargetApi = entity.TargetApi,
            TargetContext = entity.TargetContext,
            Limit = entity.Limit,
            WindowSeconds = entity.WindowSeconds,
            Algorithm = entity.Algorithm,
            Scope = entity.Scope,
            Enabled = entity.Enabled,
            Version = entity.Version
        };
}