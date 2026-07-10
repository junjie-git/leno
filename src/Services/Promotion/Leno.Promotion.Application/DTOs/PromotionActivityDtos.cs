using Leno.Promotion.Domain.ValueObjects;

namespace Leno.Promotion.Application.DTOs;

/// <summary>
/// 满减活动 DTO。
/// </summary>
public sealed class PromotionActivityDto
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public PromotionType Type { get; init; }

    public PromotionStatus Status { get; init; }

    public DateTime StartTime { get; init; }

    public DateTime EndTime { get; init; }

    public IReadOnlyList<PromotionRuleDto> Rules { get; init; } = Array.Empty<PromotionRuleDto>();

    public DateTime CreatedAt { get; init; }
}

/// <summary>
/// 满减规则 DTO。
/// </summary>
public sealed class PromotionRuleDto
{
    public decimal ThresholdAmount { get; init; }

    public decimal DiscountAmount { get; init; }
}

/// <summary>
/// 创建满减活动 DTO。
/// </summary>
public sealed class CreatePromotionActivityDto
{
    public string Name { get; init; } = string.Empty;

    public PromotionType Type { get; init; }

    public DateTime StartTime { get; init; }

    public DateTime EndTime { get; init; }

    public List<PromotionRuleDto> Rules { get; init; } = new();
}

/// <summary>
/// 更新满减活动 DTO（仅可更新名称与规则，时间区间不可改）。
/// </summary>
public sealed class UpdatePromotionActivityDto
{
    public string Name { get; init; } = string.Empty;

    public List<PromotionRuleDto> Rules { get; init; } = new();
}
