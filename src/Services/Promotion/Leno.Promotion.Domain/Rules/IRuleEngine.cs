namespace Leno.Promotion.Domain.Rules;

/// <summary>
/// 规则引擎接口，编排所有 <see cref="IPromotionRule"/> 实现，输出最终折扣汇总。
/// 实现应：
/// 1. 按 <see cref="IPromotionRule.Priority"/> 升序评估所有规则；
/// 2. 按 <see cref="IPromotionRule.Stacking"/> 控制叠加语义：
///    - <see cref="StackingPolicy.Exclusive"/>：应用后立即中断；
///    - <see cref="StackingPolicy.Stackable"/>：扣减 SubTotal 后继续评估；
///    - <see cref="StackingPolicy.BestOf"/>：对同组所有 BestOf 规则评估后取折扣最大者，仅保留最优结果；
/// 3. 返回 <see cref="PromotionEvaluationResult"/> 汇总应用规则与最终折扣。
/// </summary>
public interface IRuleEngine
{
    /// <summary>
    /// 评估规则集合，按优先级与叠加策略编排执行，返回最终折扣汇总。
    /// </summary>
    /// <param name="context">规则评估上下文。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>评估结果，包含所有应用的规则与总折扣金额。</returns>
    Task<PromotionEvaluationResult> EvaluateAsync(PromotionRuleContext context, CancellationToken ct);
}

/// <summary>
/// 规则引擎评估结果，汇总所有应用的规则及其折扣金额。
/// 不可变记录，供 <see cref="IPromotionCalculateAppService"/> 转换为对外 DTO。
/// </summary>
public sealed record PromotionEvaluationResult
{
    /// <summary>
    /// 所有应用的规则结果列表（按 <see cref="IPromotionRule.Priority"/> 升序，BestOf 仅保留最优者）。
    /// 不可空，无规则应用时为空列表。
    /// </summary>
    public required IReadOnlyList<PromotionRuleResult> AppliedRules { get; init; }

    /// <summary>
    /// 总折扣金额（所有应用规则的 DiscountAmount 之和，已扣除 BestOf 中未保留的结果）。
    /// </summary>
    public decimal TotalDiscountAmount { get; init; }

    /// <summary>
    /// 应用券标识（首个应用了券的规则的 AppliedCouponId，无券应用时为 null）。
    /// 供订单域锁定券时引用。
    /// </summary>
    public Guid? AppliedCouponId { get; init; }

    /// <summary>试算时的货币代码（默认 CNY，与现有 <c>DiscountResultDto</c> 一致）。</summary>
    public string Currency { get; init; } = "CNY";

    /// <summary>评估耗时（毫秒），便于性能监控与 SLO 告警。</summary>
    public TimeSpan Elapsed { get; init; }

    /// <summary>原始上下文 SubTotal，便于上层调用方计算实付金额。</summary>
    public decimal OriginalSubTotal { get; init; }

    /// <summary>实付金额 = <see cref="OriginalSubTotal"/> - <see cref="TotalDiscountAmount"/>（不低于 0）。</summary>
    public decimal PayableAmount => Math.Max(0m, OriginalSubTotal - TotalDiscountAmount);
}
