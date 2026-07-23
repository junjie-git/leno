using System.Diagnostics;
using Leno.Promotion.Domain.Rules;
using Microsoft.Extensions.Logging;

namespace Leno.Promotion.Infrastructure.Rules;

/// <summary>
/// 规则引擎编排器实现。
/// 编排算法：
/// 1. 按 <see cref="IPromotionRule.Priority"/> 升序排序所有注入的规则；
/// 2. 顺序评估每个规则，按 <see cref="IPromotionRule.Stacking"/> 控制叠加语义：
///    - <see cref="StackingPolicy.Exclusive"/>：规则应用后立即中断后续评估；
///    - <see cref="StackingPolicy.Stackable"/>：扣减剩余 SubTotal 后继续评估后续规则；
///    - <see cref="StackingPolicy.BestOf"/>：相邻的 BestOf 规则归为同组，同组内所有规则基于相同上下文评估后仅保留折扣最大者，其余丢弃；
///      遇到非 BestOf 规则或评估结束时解析当前 BestOf 组；
/// 3. 汇总所有应用规则结果，返回 <see cref="PromotionEvaluationResult"/>。
/// 线程安全：编排器本身无状态，依赖注入的规则实现自行保证线程安全。
/// </summary>
public sealed class RuleEngine : IRuleEngine
{
    private readonly IEnumerable<IPromotionRule> _rules;
    private readonly ILogger<RuleEngine> _logger;

    public RuleEngine(
        IEnumerable<IPromotionRule> rules,
        ILogger<RuleEngine> logger)
    {
        _rules = rules ?? throw new ArgumentNullException(nameof(rules));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<PromotionEvaluationResult> EvaluateAsync(PromotionRuleContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);

        var stopwatch = Stopwatch.StartNew();
        var originalSubTotal = context.SubTotal;

        // 快照排序后的规则列表，避免每次评估重新枚举
        var orderedRules = _rules
            .OrderBy(r => r.Priority)
            .ThenBy(r => r.RuleType, StringComparer.Ordinal)
            .ToList();

        var appliedRules = new List<PromotionRuleResult>();
        var remainingSubTotal = context.SubTotal;
        Guid? appliedCouponId = null;

        // BestOf 组缓冲：存放当前未解析的 BestOf 规则结果，组内所有规则基于相同 remainingSubTotal 评估
        var bestOfBuffer = new List<PromotionRuleResult>();
        decimal bestOfGroupBaseSubTotal = 0m;

        foreach (var rule in orderedRules)
        {
            ct.ThrowIfCancellationRequested();

            // 遇到非 BestOf 规则时，先解析已累积的 BestOf 组（保证组内规则基于相同上下文竞争）
            if (rule.Stacking != StackingPolicy.BestOf && bestOfBuffer.Count > 0)
            {
                var resolved = ResolveBestOfGroup(bestOfBuffer, bestOfGroupBaseSubTotal);
                if (resolved is not null)
                {
                    appliedRules.Add(resolved);
                    remainingSubTotal -= resolved.DiscountAmount;
                    if (resolved.AppliedCouponId is not null && appliedCouponId is null)
                    {
                        appliedCouponId = resolved.AppliedCouponId;
                    }
                }
                bestOfBuffer.Clear();
            }

            // 构造当前上下文：使用剩余 SubTotal 派生新上下文
            // BestOf 组内规则共享组开始时的 remainingSubTotal（已在首次入组时记录）
            var evalContext = rule.Stacking == StackingPolicy.BestOf && bestOfBuffer.Count > 0
                ? context with { SubTotal = bestOfGroupBaseSubTotal }
                : context with { SubTotal = remainingSubTotal };

            // 快速过滤：不适用则跳过
            var applicable = await rule.IsApplicableAsync(evalContext, ct);
            if (!applicable)
            {
                _logger.LogDebug("规则 {RuleType}（Priority={Priority}）不适用，跳过", rule.RuleType, rule.Priority);
                continue;
            }

            var result = await rule.EvaluateAsync(evalContext, ct);

            switch (rule.Stacking)
            {
                case StackingPolicy.Exclusive:
                    if (result.Applied)
                    {
                        appliedRules.Add(result);
                        remainingSubTotal -= result.DiscountAmount;
                        if (result.AppliedCouponId is not null && appliedCouponId is null)
                        {
                            appliedCouponId = result.AppliedCouponId;
                        }
                        _logger.LogInformation(
                            "规则 {RuleType}（Exclusive）应用，折扣 {Discount}，中断后续规则评估",
                            rule.RuleType, result.DiscountAmount);
                        // 解析可能残留的 BestOf 组（理论上已被前面的判断清空，防御性处理）
                        if (bestOfBuffer.Count > 0)
                        {
                            bestOfBuffer.Clear();
                        }
                        goto EvaluationComplete;
                    }
                    break;

                case StackingPolicy.Stackable:
                    if (result.Applied)
                    {
                        appliedRules.Add(result);
                        remainingSubTotal -= result.DiscountAmount;
                        if (result.AppliedCouponId is not null && appliedCouponId is null)
                        {
                            appliedCouponId = result.AppliedCouponId;
                        }
                        _logger.LogDebug(
                            "规则 {RuleType}（Stackable）应用，折扣 {Discount}，剩余 SubTotal {Remaining}",
                            rule.RuleType, result.DiscountAmount, remainingSubTotal);
                    }
                    break;

                case StackingPolicy.BestOf:
                    if (bestOfBuffer.Count == 0)
                    {
                        // 首个 BestOf 规则入组，记录组基准 SubTotal（后续组内规则均基于此值评估）
                        bestOfGroupBaseSubTotal = remainingSubTotal;
                    }
                    if (result.Applied)
                    {
                        bestOfBuffer.Add(result);
                    }
                    _logger.LogDebug(
                        "规则 {RuleType}（BestOf）评估完成，Applied={Applied}，折扣 {Discount}，加入 BestOf 组（当前组大小 {GroupSize}）",
                        rule.RuleType, result.Applied, result.DiscountAmount, bestOfBuffer.Count);
                    break;
            }
        }

    EvaluationComplete:
        // 评估结束：解析可能残留的 BestOf 组（规则列表末尾为 BestOf 的情况）
        if (bestOfBuffer.Count > 0)
        {
            var resolved = ResolveBestOfGroup(bestOfBuffer, bestOfGroupBaseSubTotal);
            if (resolved is not null)
            {
                appliedRules.Add(resolved);
                remainingSubTotal -= resolved.DiscountAmount;
                if (resolved.AppliedCouponId is not null && appliedCouponId is null)
                {
                    appliedCouponId = resolved.AppliedCouponId;
                }
            }
            bestOfBuffer.Clear();
        }

        stopwatch.Stop();

        var totalDiscount = appliedRules.Sum(r => r.DiscountAmount);

        _logger.LogInformation(
            "规则引擎评估完成：应用 {AppliedCount} 条规则，总折扣 {TotalDiscount}，实付 {Payable}，耗时 {ElapsedMs}ms",
            appliedRules.Count, totalDiscount, Math.Max(0m, originalSubTotal - totalDiscount),
            stopwatch.ElapsedMilliseconds);

        return new PromotionEvaluationResult
        {
            AppliedRules = appliedRules,
            TotalDiscountAmount = totalDiscount,
            AppliedCouponId = appliedCouponId,
            Currency = "CNY",
            Elapsed = stopwatch.Elapsed,
            OriginalSubTotal = originalSubTotal
        };
    }

    /// <summary>
    /// 解析 BestOf 组：取折扣金额最大者作为组内唯一结果，其余丢弃。
    /// 若组内无已应用规则，返回 <c>null</c>。
    /// 同折扣时取首个（优先级更高者，因列表已按 Priority 升序）。
    /// </summary>
    private static PromotionRuleResult? ResolveBestOfGroup(
        List<PromotionRuleResult> buffer,
        decimal groupBaseSubTotal)
    {
        if (buffer.Count == 0)
        {
            return null;
        }

        // 组内仅保留已应用结果，未应用的已在入组时过滤，防御性再过滤一次
        var applied = buffer.Where(r => r.Applied).ToList();
        if (applied.Count == 0)
        {
            return null;
        }

        // 取折扣最大者；同折扣取首个（优先级更高）
        var best = applied[0];
        for (var i = 1; i < applied.Count; i++)
        {
            if (applied[i].DiscountAmount > best.DiscountAmount)
            {
                best = applied[i];
            }
        }

        return best;
    }
}
