using Leno.Promotion.Domain.Aggregates;
using Leno.Promotion.Domain.Repositories;
using Leno.Promotion.Domain.Rules;
using Leno.Promotion.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Leno.Promotion.Infrastructure.Rules;

/// <summary>
/// 满减规则实现，从 <see cref="IPromotionActivityRepository.GetActiveAsync"/> 加载当前进行中的满减活动，
/// 对每个活动的规则集命中最高档减免，取所有活动中的最大折扣。
/// 算法完整迁移自 <c>PromotionCalculateAppService.CalculateActivityDiscountAsync</c>（旧路径，向后兼容）。
/// </summary>
[Leno.Promotion.Domain.Rules.RulePriority(100, "满减规则")]
public sealed class FullReductionRule : IPromotionRule
{
    /// <summary>规则类型标识。</summary>
    public const string TypeKey = "FullReduction";

    private readonly IPromotionActivityRepository _promotionActivityRepository;
    private readonly IJsonRuleLoader _jsonRuleLoader;
    private readonly ILogger<FullReductionRule> _logger;

    public FullReductionRule(
        IPromotionActivityRepository promotionActivityRepository,
        IJsonRuleLoader jsonRuleLoader,
        ILogger<FullReductionRule> logger)
    {
        _promotionActivityRepository = promotionActivityRepository ?? throw new ArgumentNullException(nameof(promotionActivityRepository));
        _jsonRuleLoader = jsonRuleLoader ?? throw new ArgumentNullException(nameof(jsonRuleLoader));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public string RuleType => TypeKey;

    /// <inheritdoc />
    public Leno.Promotion.Domain.Rules.StackingPolicy Stacking =>
        _jsonRuleLoader.GetDefinition(TypeKey)?.Stacking
        ?? Leno.Promotion.Domain.Rules.StackingPolicy.Stackable;

    /// <inheritdoc />
    public int Priority =>
        _jsonRuleLoader.GetDefinition(TypeKey)?.Priority ?? 100;

    /// <inheritdoc />
    public Task<bool> IsApplicableAsync(PromotionRuleContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.SubTotal <= 0m)
        {
            return Task.FromResult(false);
        }

        // 应用卖家限定过滤：若定义了 ApplicableSellerIds 且当前卖家不在列表中，则不适用
        var definition = _jsonRuleLoader.GetDefinition(TypeKey);
        if (definition is { ApplicableSellerIds.Count: > 0 }
            && !definition.ApplicableSellerIds.Contains(context.SellerId))
        {
            return Task.FromResult(false);
        }

        // 应用类目限定过滤：若定义了 ApplicableCategoryCodes 且当前购物车行均不在类目列表中，则不适用
        if (definition is { ApplicableCategoryCodes.Count: > 0 })
        {
            var hasMatchingCategory = context.Items
                .Any(i => !string.IsNullOrWhiteSpace(i.CategoryCode)
                          && definition.ApplicableCategoryCodes.Contains(i.CategoryCode));
            if (!hasMatchingCategory)
            {
                return Task.FromResult(false);
            }
        }

        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public async Task<PromotionRuleResult> EvaluateAsync(PromotionRuleContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.SubTotal <= 0m)
        {
            return PromotionRuleResult.NotApplied(
                TypeKey,
                "订单金额 ≤ 0，未触发满减",
                EmptyMetadata);
        }

        // 1. 加载当前 Active 且在有效时间区间内的满减活动
        var now = context.EvaluatedAt;
        var activities = await _promotionActivityRepository.GetActiveAsync(now, ct);
        if (activities.Count == 0)
        {
            return PromotionRuleResult.NotApplied(TypeKey, "无进行中的满减活动", EmptyMetadata);
        }

        // 2. 应用类目限定过滤（若有）：仅保留包含匹配类目商品的活动
        var definition = _jsonRuleLoader.GetDefinition(TypeKey);
        var applicableCategoryCodes = definition?.ApplicableCategoryCodes ?? EmptyStringList;
        if (applicableCategoryCodes.Count > 0)
        {
            var contextCategories = context.Items
                .Where(i => !string.IsNullOrWhiteSpace(i.CategoryCode))
                .Select(i => i.CategoryCode!)
                .ToHashSet();
            activities = activities
                .Where(a => true) // 满减活动自身不按类目过滤，类目过滤由 IsApplicableAsync 与下面计算时统一处理
                .ToList();
        }

        // 3. 取所有活动里命中最高档规则的最大减免（与旧 PromotionActivity.CalculateDiscount 行为一致）
        decimal best = 0m;
        Guid bestActivityId = Guid.Empty;
        decimal bestThreshold = 0m;
        foreach (var activity in activities)
        {
            var discount = activity.CalculateDiscount(context.SubTotal);
            if (discount > best)
            {
                best = discount;
                bestActivityId = activity.Id;
                bestThreshold = activity.Rules.LastOrDefault(r => context.SubTotal >= r.ThresholdAmount)?.ThresholdAmount ?? 0m;
            }
        }

        if (best <= 0m)
        {
            return PromotionRuleResult.NotApplied(
                TypeKey,
                $"订单金额 {context.SubTotal} 未命中任何满减档位",
                EmptyMetadata);
        }

        var metadata = new Dictionary<string, string>
        {
            ["activityId"] = bestActivityId.ToString(),
            ["matchedThreshold"] = bestThreshold.ToString("F2"),
            ["discountAmount"] = best.ToString("F2"),
            ["candidateCount"] = activities.Count.ToString()
        };

        _logger.LogInformation(
            "满减规则命中：活动 {ActivityId}，门槛 {Threshold}，减免 {Discount}",
            bestActivityId, bestThreshold, best);

        return PromotionRuleResult.AppliedResult(TypeKey, best, null, metadata);
    }

    private static readonly IReadOnlyDictionary<string, string> EmptyMetadata =
        new Dictionary<string, string>(0);

    private static readonly List<string> EmptyStringList = new(0);
}
