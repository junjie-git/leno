namespace Leno.Promotion.Domain.Rules;

/// <summary>
/// 促销规则抽象接口。
/// 规则实现必须可独立评估：给定 <see cref="PromotionRuleContext"/>，
/// 判断 <see cref="IsApplicableAsync"/>（快速过滤）后调用 <see cref="EvaluateAsync"/> 计算折扣。
/// 规则编排由 <see cref="IRuleEngine"/> 按 <see cref="Priority"/> 升序、<see cref="Stacking"/> 策略执行。
/// </summary>
public interface IPromotionRule
{
    /// <summary>
    /// 规则类型标识（如 "FullReduction" / "Coupon" / "SeckillDiscount"）。
    /// 与 <see cref="Leno.Promotion.Domain.Aggregates.PromotionRuleDefinition"/> 持久化的 RuleType 对齐。
    /// </summary>
    string RuleType { get; }

    /// <summary>
    /// 叠加策略，决定本规则应用后是否中断后续规则评估。
    /// </summary>
    StackingPolicy Stacking { get; }

    /// <summary>
    /// 优先级，数字越小越先评估。同 <see cref="StackingPolicy"/> 的规则按此值升序评估。
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// 快速判断规则是否适用于当前上下文（如未传 CouponCode 时 CouponRule 直接返回 false）。
    /// 不进行实际折扣计算，仅做轻量过滤，便于编排器跳过不适用规则。
    /// </summary>
    /// <param name="context">规则评估上下文。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>true 表示适用，编排器将调用 <see cref="EvaluateAsync"/>；false 表示跳过本规则。</returns>
    Task<bool> IsApplicableAsync(PromotionRuleContext context, CancellationToken ct);

    /// <summary>
    /// 评估规则，计算折扣金额并返回结果。
    /// 编排器在调用本方法前会先调用 <see cref="IsApplicableAsync"/>，实现可信任其结果。
    /// 注意：编排器在评估期间会通过 <c>context with { SubTotal = remainingSubTotal }</c> 派生新上下文，
    /// 实现应基于传入 context 的 SubTotal 计算折扣，而非引用外部状态。
    /// </summary>
    /// <param name="context">规则评估上下文（编排器可能已派生扣减后的 SubTotal）。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>规则评估结果（包含是否应用、折扣金额、元数据）。</returns>
    Task<PromotionRuleResult> EvaluateAsync(PromotionRuleContext context, CancellationToken ct);
}
