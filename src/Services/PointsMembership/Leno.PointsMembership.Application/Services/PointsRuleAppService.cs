using Leno.PointsMembership.Application.DTOs;
using Leno.PointsMembership.Domain.Exceptions;
using Leno.PointsMembership.Domain.Repositories;
using Leno.SharedKernel.Abstractions;
using PointsRuleAggregate = Leno.PointsMembership.Domain.Aggregates.PointsRule;

namespace Leno.PointsMembership.Application.Services;

/// <summary>
/// 积分规则管理应用服务实现，编排运营端规则 CRUD、启停用例。
/// 编码唯一性在应用层预校验（返回 409），同时由 EF Core 唯一索引兜底防并发冲突。
/// </summary>
public sealed class PointsRuleAppService : IPointsRuleAppService
{
    private readonly IPointsRuleRepository _ruleRepository;
    private readonly IUnitOfWork _unitOfWork;

    public PointsRuleAppService(
        IPointsRuleRepository ruleRepository,
        IUnitOfWork unitOfWork)
    {
        _ruleRepository = ruleRepository;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public async Task<List<PointsRuleDto>> GetRulesAsync(CancellationToken ct = default)
    {
        var rules = await _ruleRepository.GetAllAsync(ct);
        return rules.Select(ToDto).ToList();
    }

    /// <inheritdoc />
    public async Task<PointsRuleDto> CreateRuleAsync(CreatePointsRuleDto dto, CancellationToken ct = default)
    {
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
        return ToDto(rule);
    }

    /// <inheritdoc />
    public async Task<PointsRuleDto> UpdateRuleAsync(Guid ruleId, UpdatePointsRuleDto dto, CancellationToken ct = default)
    {
        var rule = await RequireRuleAsync(ruleId, ct);
        rule.Update(dto.Name, dto.ActionType, dto.Points, dto.DailyLimit);

        await _unitOfWork.SaveEntitiesAsync(ct);
        return ToDto(rule);
    }

    /// <inheritdoc />
    public async Task EnableRuleAsync(Guid ruleId, CancellationToken ct = default)
    {
        var rule = await RequireRuleAsync(ruleId, ct);
        rule.Enable();
        await _unitOfWork.SaveEntitiesAsync(ct);
    }

    /// <inheritdoc />
    public async Task DisableRuleAsync(Guid ruleId, CancellationToken ct = default)
    {
        var rule = await RequireRuleAsync(ruleId, ct);
        rule.Disable();
        await _unitOfWork.SaveEntitiesAsync(ct);
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
