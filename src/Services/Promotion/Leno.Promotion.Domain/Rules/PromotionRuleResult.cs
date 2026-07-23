namespace Leno.Promotion.Domain.Rules;

/// <summary>
/// 单条规则评估结果，由 <see cref="IPromotionRule.EvaluateAsync"/> 返回。
/// 不可变记录，规则编排器聚合所有 Applied 结果后生成 <see cref="PromotionEvaluationResult"/>。
/// </summary>
public sealed record PromotionRuleResult
{
    /// <summary>
    /// 规则类型标识（与 <see cref="IPromotionRule.RuleType"/> 一致），
    /// 用于编排器与上层调用方追溯哪个规则生效。
    /// </summary>
    public required string RuleType { get; init; }

    /// <summary>
    /// 本规则应用的折扣金额（≥ 0）。
    /// 当 <see cref="Applied"/> = false 时为 0，表示规则未生效。
    /// </summary>
    public decimal DiscountAmount { get; init; }

    /// <summary>
    /// 规则应用关联的券标识（仅优惠券规则非空，其他规则为 <c>null</c>）。
    /// 用于订单锁定券时回填。
    /// </summary>
    public Guid? AppliedCouponId { get; init; }

    /// <summary>
    /// 规则元数据（如命中的活动 Id、命中的满减档位、秒杀活动 Id 等），便于审计与可观测。
    /// 不可空，无元数据时为空字典。
    /// </summary>
    public required IReadOnlyDictionary<string, string> Metadata { get; init; }

    /// <summary>规则是否被应用。false 时 <see cref="DiscountAmount"/> 必为 0。</summary>
    public bool Applied { get; init; }

    /// <summary>
    /// 未应用原因（<see cref="Applied"/>=false 时填写，便于调试与日志）。
    /// 如 "订单金额未达满减门槛" / "用户无可用优惠券"。
    /// </summary>
    public string? NotAppliedReason { get; init; }

    /// <summary>构造未应用结果，<see cref="DiscountAmount"/>=0、<see cref="Applied"/>=false。</summary>
    public static PromotionRuleResult NotApplied(
        string ruleType,
        string reason,
        IReadOnlyDictionary<string, string>? metadata = null)
        => new()
        {
            RuleType = ruleType,
            DiscountAmount = 0m,
            AppliedCouponId = null,
            Metadata = metadata ?? EmptyMetadata,
            Applied = false,
            NotAppliedReason = reason
        };

    /// <summary>构造已应用结果。</summary>
    public static PromotionRuleResult AppliedResult(
        string ruleType,
        decimal discountAmount,
        Guid? appliedCouponId,
        IReadOnlyDictionary<string, string>? metadata = null)
        => new()
        {
            RuleType = ruleType,
            DiscountAmount = discountAmount,
            AppliedCouponId = appliedCouponId,
            Metadata = metadata ?? EmptyMetadata,
            Applied = true,
            NotAppliedReason = null
        };

    /// <summary>空元数据常量，避免每次评估重复分配空字典。</summary>
    private static readonly IReadOnlyDictionary<string, string> EmptyMetadata =
        new Dictionary<string, string>(0);
}
