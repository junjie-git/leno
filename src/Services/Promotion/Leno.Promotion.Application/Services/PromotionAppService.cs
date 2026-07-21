using Leno.Promotion.Application.DTOs;
using Leno.Promotion.Domain.Aggregates;
using Leno.Promotion.Domain.Exceptions;
using Leno.Promotion.Domain.Repositories;
using Leno.Promotion.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using PromotionActivityAggregate = Leno.Promotion.Domain.Aggregates.PromotionActivity;

namespace Leno.Promotion.Application.Services;

/// <summary>
/// 满减活动管理应用服务实现。
/// </summary>
public sealed class PromotionAppService : IPromotionAppService
{
    private readonly IPromotionActivityRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public PromotionAppService(IPromotionActivityRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public async Task<PromotionActivityDto> CreateAsync(CreatePromotionActivityDto dto, CancellationToken ct = default)
    {
        var activity = PromotionActivityAggregate.Create(
            Guid.NewGuid(), dto.Name, dto.Type, dto.StartTime, dto.EndTime);

        foreach (var rule in dto.Rules)
        {
            activity.AddRule(rule.ThresholdAmount, rule.DiscountAmount);
        }

        await _repository.AddAsync(activity, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);
        return ToDto(activity);
    }

    /// <inheritdoc />
    public async Task<PromotionActivityDto> UpdateAsync(Guid activityId, UpdatePromotionActivityDto dto, CancellationToken ct = default)
    {
        var activity = await RequireActivityAsync(activityId, ct);
        activity.Rename(dto.Name);
        // 移除所有规则后重新添加
        var existingRules = activity.Rules.ToList();
        foreach (var rule in existingRules)
        {
            activity.RemoveRule(rule.ThresholdAmount);
        }
        foreach (var rule in dto.Rules)
        {
            activity.AddRule(rule.ThresholdAmount, rule.DiscountAmount);
        }

        await _unitOfWork.SaveEntitiesAsync(ct);
        return ToDto(activity);
    }

    /// <inheritdoc />
    public async Task ActivateAsync(Guid activityId, CancellationToken ct = default)
    {
        var activity = await RequireActivityAsync(activityId, ct);
        activity.Activate();
        await _unitOfWork.SaveEntitiesAsync(ct);
    }

    /// <inheritdoc />
    public async Task PauseAsync(Guid activityId, CancellationToken ct = default)
    {
        var activity = await RequireActivityAsync(activityId, ct);
        activity.Pause();
        await _unitOfWork.SaveEntitiesAsync(ct);
    }

    /// <inheritdoc />
    public async Task CloseAsync(Guid activityId, CancellationToken ct = default)
    {
        var activity = await RequireActivityAsync(activityId, ct);
        activity.Close();
        await _unitOfWork.SaveEntitiesAsync(ct);
    }

    /// <inheritdoc />
    public async Task<PromotionActivityDto> GetByIdAsync(Guid activityId, CancellationToken ct = default)
    {
        var activity = await RequireActivityAsync(activityId, ct);
        return ToDto(activity);
    }

    /// <inheritdoc />
    public async Task<List<PromotionActivityDto>> QueryAsync(PromotionStatus? status, int page, int pageSize, CancellationToken ct = default)
    {
        var activities = await _repository.GetByStatusAsync(status, page, pageSize, ct);
        return activities.Select(ToDto).ToList();
    }

    private async Task<PromotionActivityAggregate> RequireActivityAsync(Guid activityId, CancellationToken ct)
        => await _repository.GetByIdAsync(activityId, ct)
           ?? throw new PromotionDomainException($"活动 {activityId} 不存在", "PROMOTION_NOT_FOUND");

    private static PromotionActivityDto ToDto(PromotionActivityAggregate activity)
        => new()
        {
            Id = activity.Id,
            Name = activity.Name,
            Type = activity.Type,
            Status = activity.Status,
            StartTime = activity.StartTime,
            EndTime = activity.EndTime,
            Rules = activity.Rules.Select(r => new PromotionRuleDto
            {
                ThresholdAmount = r.ThresholdAmount,
                DiscountAmount = r.DiscountAmount
            }).ToList(),
            CreatedAt = activity.CreatedAt
        };
}
