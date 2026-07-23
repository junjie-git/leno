using Leno.Promotion.Domain.Aggregates;
using Leno.Promotion.Domain.Repositories;
using Leno.Promotion.Domain.Rules;
using Leno.Promotion.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using SeckillActivityAggregate = Leno.Promotion.Domain.Aggregates.SeckillActivity;

namespace Leno.Promotion.Infrastructure.Rules;

/// <summary>
/// 秒杀折扣规则实现。
/// 当 <see cref="PromotionRuleContext.SeckillActivityId"/> 指向当前进行中的秒杀活动时，
/// 计算 (OriginalPrice - SeckillPrice) * Quantity 作为秒杀折扣金额。
/// 默认 <see cref="Leno.Promotion.Domain.Rules.StackingPolicy.Exclusive"/>：秒杀订单不与满减/优惠券叠加。
/// 若 Promotion BC 无秒杀相关折扣计算逻辑（旧 <c>PromotionCalculateAppService</c> 未含秒杀），本规则基于计划 §4.2 骨架完整实现。
/// </summary>
[Leno.Promotion.Domain.Rules.RulePriority(50, "秒杀折扣规则")]
public sealed class SeckillDiscountRule : IPromotionRule
{
    /// <summary>规则类型标识。</summary>
    public const string TypeKey = "SeckillDiscount";

    private readonly ISeckillActivityRepository _seckillActivityRepository;
    private readonly IJsonRuleLoader _jsonRuleLoader;
    private readonly ILogger<SeckillDiscountRule> _logger;

    public SeckillDiscountRule(
        ISeckillActivityRepository seckillActivityRepository,
        IJsonRuleLoader jsonRuleLoader,
        ILogger<SeckillDiscountRule> logger)
    {
        _seckillActivityRepository = seckillActivityRepository ?? throw new ArgumentNullException(nameof(seckillActivityRepository));
        _jsonRuleLoader = jsonRuleLoader ?? throw new ArgumentNullException(nameof(jsonRuleLoader));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public string RuleType => TypeKey;

    /// <inheritdoc />
    public Leno.Promotion.Domain.Rules.StackingPolicy Stacking =>
        _jsonRuleLoader.GetDefinition(TypeKey)?.Stacking
        ?? Leno.Promotion.Domain.Rules.StackingPolicy.Exclusive;

    /// <inheritdoc />
    public int Priority =>
        _jsonRuleLoader.GetDefinition(TypeKey)?.Priority ?? 50;

    /// <inheritdoc />
    public Task<bool> IsApplicableAsync(PromotionRuleContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.SubTotal <= 0m || context.Items.Count == 0)
        {
            return Task.FromResult(false);
        }

        // 应用卖家限定过滤
        var definition = _jsonRuleLoader.GetDefinition(TypeKey);
        if (definition is { ApplicableSellerIds.Count: > 0 }
            && !definition.ApplicableSellerIds.Contains(context.SellerId))
        {
            return Task.FromResult(false);
        }

        // 秒杀订单：context.SeckillActivityId 必须非空，或 JsonRuleDefinition.SeckillFilter.DefaultActivityId 非空
        var hasActivityId = !string.IsNullOrWhiteSpace(context.SeckillActivityId)
                            || definition?.SeckillFilter?.DefaultActivityId is not null;
        return Task.FromResult(hasActivityId);
    }

    /// <inheritdoc />
    public async Task<PromotionRuleResult> EvaluateAsync(PromotionRuleContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Items.Count == 0)
        {
            return PromotionRuleResult.NotApplied(TypeKey, "购物车行为空", EmptyMetadata);
        }

        // 1. 解析秒杀活动 Id：优先 context.SeckillActivityId，回退到 JsonRuleDefinition.SeckillFilter.DefaultActivityId
        var activityId = ResolveActivityId(context);
        if (activityId is null)
        {
            return PromotionRuleResult.NotApplied(TypeKey, "未指定秒杀活动 Id", EmptyMetadata);
        }

        // 2. 加载秒杀活动聚合（不依赖状态过滤，由聚合 IsWithinActiveWindow 判断时间窗口）
        var activity = await _seckillActivityRepository.GetByIdAsync(activityId.Value, ct);
        if (activity is null)
        {
            return PromotionRuleResult.NotApplied(TypeKey, $"秒杀活动 {activityId} 不存在", EmptyMetadata);
        }

        var now = context.EvaluatedAt;
        if (activity.Status != SeckillStatus.Active)
        {
            return PromotionRuleResult.NotApplied(
                TypeKey,
                $"秒杀活动 {activityId} 状态 {activity.Status} 非 Active",
                EmptyMetadata);
        }

        if (!activity.IsWithinActiveWindow(now))
        {
            return PromotionRuleResult.NotApplied(
                TypeKey,
                $"秒杀活动 {activityId} 不在有效时间区间内",
                EmptyMetadata);
        }

        // 3. 按购物车行匹配秒杀 SkuId，计算每行折扣 (OriginalPrice - SeckillPrice) * Quantity，乘以 DiscountRatio
        var definition = _jsonRuleLoader.GetDefinition(TypeKey);
        var discountRatio = definition?.SeckillFilter?.DiscountRatio ?? 100m;
        if (discountRatio <= 0m || discountRatio > 100m)
        {
            discountRatio = 100m;
        }

        decimal totalDiscount = 0m;
        int matchedQuantity = 0;
        foreach (var item in context.Items)
        {
            if (item.SkuId != activity.SkuId)
            {
                continue;
            }

            // 单行折扣 = (原价 - 秒杀价) * 数量 * (DiscountRatio / 100)
            var lineOriginalTotal = activity.OriginalPrice * item.Quantity;
            var lineSeckillTotal = activity.SeckillPrice * item.Quantity;
            var lineDiscount = (lineOriginalTotal - lineSeckillTotal) * discountRatio / 100m;

            // 折扣不可超过行原价小计
            if (lineDiscount > lineOriginalTotal)
            {
                lineDiscount = lineOriginalTotal;
            }

            if (lineDiscount > 0m)
            {
                totalDiscount += lineDiscount;
                matchedQuantity += item.Quantity;
            }
        }

        if (totalDiscount <= 0m)
        {
            return PromotionRuleResult.NotApplied(
                TypeKey,
                $"购物车未匹配秒杀活动 {activityId} 的 SKU {activity.SkuId}",
                EmptyMetadata);
        }

        var metadata = new Dictionary<string, string>
        {
            ["activityId"] = activity.Id.ToString(),
            ["skuId"] = activity.SkuId.ToString(),
            ["seckillPrice"] = activity.SeckillPrice.ToString("F2"),
            ["originalPrice"] = activity.OriginalPrice.ToString("F2"),
            ["matchedQuantity"] = matchedQuantity.ToString(),
            ["discountRatio"] = discountRatio.ToString("F2"),
            ["discountAmount"] = totalDiscount.ToString("F2")
        };

        _logger.LogInformation(
            "秒杀折扣规则命中：活动 {ActivityId}，SKU {SkuId}，原价 {Original}，秒杀价 {Seckill}，数量 {Qty}，折扣 {Discount}",
            activity.Id, activity.SkuId, activity.OriginalPrice, activity.SeckillPrice,
            matchedQuantity, totalDiscount);

        return PromotionRuleResult.AppliedResult(TypeKey, totalDiscount, null, metadata);
    }

    /// <summary>
    /// 解析秒杀活动 Id：优先 <see cref="PromotionRuleContext.SeckillActivityId"/>（字符串），
    /// 回退到 <see cref="JsonRuleDefinition.SeckillFilter"/>.<see cref="SeckillFilterDefinition.DefaultActivityId"/>。
    /// </summary>
    private Guid? ResolveActivityId(PromotionRuleContext context)
    {
        if (!string.IsNullOrWhiteSpace(context.SeckillActivityId)
            && Guid.TryParse(context.SeckillActivityId, out var parsed)
            && parsed != Guid.Empty)
        {
            return parsed;
        }

        var definition = _jsonRuleLoader.GetDefinition(TypeKey);
        return definition?.SeckillFilter?.DefaultActivityId;
    }

    private static readonly IReadOnlyDictionary<string, string> EmptyMetadata =
        new Dictionary<string, string>(0);
}
