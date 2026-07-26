using Leno.PointsMembership.Domain.ValueObjects;

namespace Leno.PointsMembership.Application.DTOs;

/// <summary>
/// 积分规则 DTO，表达运营配置的积分发放规则。
/// 响应 GET /api/admin/points/rules 与 GET /api/admin/points/rules/{ruleId}。
/// </summary>
public sealed class PointsRuleDto
{
    public Guid Id { get; init; }

    /// <summary>规则编码，全局唯一。</summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>规则名称。</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>行为类型。</summary>
    public PointsActionType ActionType { get; init; }

    /// <summary>积分值，正数发放、负数扣减。</summary>
    public int Points { get; init; }

    /// <summary>每日上限。</summary>
    public int DailyLimit { get; init; }

    /// <summary>规则状态。</summary>
    public PointsRuleStatus Status { get; init; }

    /// <summary>最后更新时间（UTC）。</summary>
    public DateTime UpdatedAt { get; init; }
}

/// <summary>
/// 创建积分规则 DTO。
/// 请求 POST /api/admin/points/rules。
/// 编码全局唯一，重复时返回 409。
/// </summary>
public sealed class CreatePointsRuleDto
{
    /// <summary>规则编码，全局唯一，如 DAILY_CHECK。创建后不可修改。</summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>规则名称。</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>行为类型。</summary>
    public PointsActionType ActionType { get; init; }

    /// <summary>积分值，范围 [-1000, 1000]，正数发放、负数扣减。</summary>
    public int Points { get; init; }

    /// <summary>每日上限，范围 [1, 100]。</summary>
    public int DailyLimit { get; init; }

    /// <summary>初始状态，默认 Enabled。</summary>
    public PointsRuleStatus Status { get; init; } = PointsRuleStatus.Enabled;
}

/// <summary>
/// 更新积分规则 DTO（编码不可改，状态经启用/停用端点切换）。
/// 请求 PUT /api/admin/points/rules/{ruleId}。
/// </summary>
public sealed class UpdatePointsRuleDto
{
    /// <summary>规则名称。</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>行为类型。</summary>
    public PointsActionType ActionType { get; init; }

    /// <summary>积分值，范围 [-1000, 1000]，正数发放、负数扣减。</summary>
    public int Points { get; init; }

    /// <summary>每日上限，范围 [1, 100]。</summary>
    public int DailyLimit { get; init; }
}
