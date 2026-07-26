using Leno.Points.Application.DTOs;
using Leno.Points.Domain.Aggregates.PointsRule;
using Leno.Points.Domain.Exceptions;
using Leno.Points.Domain.Repositories;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;
using PointsRuleAggregate = Leno.Points.Domain.Aggregates.PointsRule.PointsRule;

namespace Leno.Points.Application.Services;

/// <summary>
/// 积分规则管理应用服务实现，编排运营端规则 CRUD、启停用例。
/// 编码唯一性在应用层预校验（返回 409），同时由 EF Core 唯一索引兜底防并发冲突。
/// </summary>
public sealed class PointsRuleAppService : IPointsRuleAppService
{
    private readonly IPointsRuleRepository _ruleRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<PointsRuleAppService> _logger;

    public PointsRuleAppService(
        IPointsRuleRepository ruleRepository,
        IUnitOfWork unitOfWork,
        ILogger<PointsRuleAppService> logger)
    {
        ArgumentNullException.ThrowIfNull(ruleRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(logger);
        _ruleRepository = ruleRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<List<PointsRuleDto>> GetRulesAsync(CancellationToken ct = default)
    {
        var rules = await _ruleRepository.GetAllAsync(ct);
        return (rules ?? new List<PointsRuleAggregate>()).Select(ToDto).ToList();
    }

    /// <inheritdoc />
    public async Task<PointsRuleDto> CreateRuleAsync(CreatePointsRuleDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        // 编码唯一性预校验，并发场景由数据库唯一索引兜底
        var existing = await _ruleRepository.GetByCodeAsync(dto.Code, ct);
        if (existing is not null)
        {
            throw new PointsDomainException(
                $"积分规则编码 {dto.Code} 已存在",
                "POINTS_RULE_CODE_EXISTS");
        }

        var rule = PointsRuleAggregate.Create(
            Guid.NewGuid(),
            dto.Code,
            dto.Name,
            dto.ActionType,
            dto.Points,
            dto.DailyLimit,
            dto.Status);

        await _ruleRepository.AddAsync(rule, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        _logger.LogInformation(
            "积分规则创建成功 RuleId={RuleId} Code={Code}",
            rule.Id, rule.Code);

        return ToDto(rule);
    }

    /// <inheritdoc />
    public async Task<PointsRuleDto> UpdateRuleAsync(Guid ruleId, UpdatePointsRuleDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var rule = await RequireRuleAsync(ruleId, ct);
        rule.Update(dto.Name, dto.ActionType, dto.Points, dto.DailyLimit);

        await _unitOfWork.SaveEntitiesAsync(ct);

        _logger.LogInformation(
            "积分规则更新成功 RuleId={RuleId} Code={Code}",
            rule.Id, rule.Code);

        return ToDto(rule);
    }

    /// <inheritdoc />
    public async Task EnableRuleAsync(Guid ruleId, CancellationToken ct = default)
    {
        var rule = await RequireRuleAsync(ruleId, ct);
        rule.Enable();
        await _unitOfWork.SaveEntitiesAsync(ct);

        _logger.LogInformation("积分规则启用 RuleId={RuleId}", ruleId);
    }

    /// <inheritdoc />
    public async Task DisableRuleAsync(Guid ruleId, CancellationToken ct = default)
    {
        var rule = await RequireRuleAsync(ruleId, ct);
        rule.Disable();
        await _unitOfWork.SaveEntitiesAsync(ct);

        _logger.LogInformation("积分规则停用 RuleId={RuleId}", ruleId);
    }

    private async Task<PointsRuleAggregate> RequireRuleAsync(Guid ruleId, CancellationToken ct)
        => await _ruleRepository.GetByIdAsync(ruleId, ct)
           ?? throw new PointsDomainException(
               $"积分规则 {ruleId} 不存在",
               "POINTS_RULE_NOT_FOUND");

    private static PointsRuleDto ToDto(PointsRuleAggregate rule)
        => new()
        {
            Id = rule.Id,
            Code = rule.Code,
            Name = rule.Name,
            ActionType = rule.ActionType,
            Points = rule.Points,
            DailyLimit = rule.DailyLimit,
            Status = rule.Status,
            UpdatedAt = rule.UpdatedAt
        };
}
