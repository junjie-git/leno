using Leno.Promotion.Domain.Aggregates;
using Leno.Promotion.Domain.Repositories;
using Leno.Promotion.Domain.Rules;
using Leno.Promotion.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using CouponAggregate = Leno.Promotion.Domain.Aggregates.Coupon;

namespace Leno.Promotion.Infrastructure.Rules;

/// <summary>
/// 优惠券规则实现，从 <see cref="IUserCouponRepository.GetByUserAsync"/> 加载用户 Unused 且未过期券，
/// 批量加载券模板后按券类型计算可抵扣金额，取所有券中的最大折扣。
/// 算法完整迁移自 <c>PromotionCalculateAppService.CalculateCouponDiscountAsync</c> 与 <c>ComputeCouponDiscount</c>（旧路径，向后兼容）。
/// </summary>
[Leno.Promotion.Domain.Rules.RulePriority(200, "优惠券规则")]
public sealed class CouponRule : IPromotionRule
{
    /// <summary>规则类型标识。</summary>
    public const string TypeKey = "Coupon";

    private readonly IUserCouponRepository _userCouponRepository;
    private readonly ICouponRepository _couponRepository;
    private readonly IJsonRuleLoader _jsonRuleLoader;
    private readonly ILogger<CouponRule> _logger;

    public CouponRule(
        IUserCouponRepository userCouponRepository,
        ICouponRepository couponRepository,
        IJsonRuleLoader jsonRuleLoader,
        ILogger<CouponRule> logger)
    {
        _userCouponRepository = userCouponRepository ?? throw new ArgumentNullException(nameof(userCouponRepository));
        _couponRepository = couponRepository ?? throw new ArgumentNullException(nameof(couponRepository));
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
        _jsonRuleLoader.GetDefinition(TypeKey)?.Priority ?? 200;

    /// <inheritdoc />
    public async Task<bool> IsApplicableAsync(PromotionRuleContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.SubTotal <= 0m)
        {
            return false;
        }

        if (context.UserId <= 0)
        {
            return false;
        }

        // 应用卖家限定过滤
        var definition = _jsonRuleLoader.GetDefinition(TypeKey);
        if (definition is { ApplicableSellerIds.Count: > 0 }
            && !definition.ApplicableSellerIds.Contains(context.SellerId))
        {
            return false;
        }

        // 当配置为仅使用指定 CouponCode 时，若 context 未传 CouponCode 则跳过
        if (definition?.CouponFilter is { UseCouponCodeOnly: true }
            && string.IsNullOrWhiteSpace(context.CouponCode))
        {
            return false;
        }

        // 快速检查用户是否有 Unused 券（避免空跑 EvaluateAsync）
        // 注意：UserId 在 Domain 中是 long，但 UserCoupon 用 Guid，需要从 context.Attributes 中读取
        // 现有 CalculateDiscountDto 用 Guid，故 context.Attributes["UserGuid"] 应由 PromotionCalculateAppService 注入
        var userGuid = TryGetUserGuid(context);
        if (userGuid is null)
        {
            return false;
        }

        var userCoupons = await _userCouponRepository.GetByUserAsync(userGuid.Value, CouponStatus.Unused, context.EvaluatedAt, ct);
        return userCoupons.Count > 0;
    }

    /// <inheritdoc />
    public async Task<PromotionRuleResult> EvaluateAsync(PromotionRuleContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.SubTotal <= 0m)
        {
            return PromotionRuleResult.NotApplied(TypeKey, "订单金额 ≤ 0", EmptyMetadata);
        }

        var userGuid = TryGetUserGuid(context);
        if (userGuid is null)
        {
            return PromotionRuleResult.NotApplied(TypeKey, "无法解析用户 Guid（context 缺少 UserGuid 属性）", EmptyMetadata);
        }

        var now = context.EvaluatedAt;

        // 1. 查询用户 Unused 且未过期的券（ExpiredAt > now 下推 SQL，P1-10 优化保留）
        var userCoupons = await _userCouponRepository.GetByUserAsync(userGuid.Value, CouponStatus.Unused, now, ct);
        if (userCoupons.Count == 0)
        {
            return PromotionRuleResult.NotApplied(TypeKey, "用户无可用优惠券", EmptyMetadata);
        }

        // 2. 一次性批量加载所有券模板，消除 N+1 查询（与旧实现一致）
        var couponIds = userCoupons.Select(uc => uc.CouponId).Distinct().ToList();
        var coupons = await _couponRepository.GetByIdsAsync(couponIds, ct);
        var couponMap = coupons
            .Where(c => c.Status == CouponTemplateStatus.Enabled)
            .ToDictionary(c => c.Id);

        // 3. 应用 JsonRuleDefinition 中 CouponFilter.ApplicableCouponIds 过滤（如配置仅这些券模板可参与）
        var definition = _jsonRuleLoader.GetDefinition(TypeKey);
        var applicableCouponIds = definition?.CouponFilter?.ApplicableCouponIds;
        if (applicableCouponIds is { Count: > 0 })
        {
            var allowedSet = applicableCouponIds.ToHashSet();
            couponMap = couponMap
                .Where(kv => allowedSet.Contains(kv.Key))
                .ToDictionary(kv => kv.Key, kv => kv.Value);
        }

        // 4. 遍历每张券按类型计算抵扣金额，取最大者
        decimal best = 0m;
        Guid bestUserCouponId = Guid.Empty;
        Guid bestCouponId = Guid.Empty;
        string bestCouponType = string.Empty;
        foreach (var userCoupon in userCoupons)
        {
            if (!couponMap.TryGetValue(userCoupon.CouponId, out var coupon))
            {
                continue;
            }

            var discount = ComputeCouponDiscount(coupon, context.SubTotal);
            if (discount > best)
            {
                best = discount;
                bestUserCouponId = userCoupon.Id;
                bestCouponId = coupon.Id;
                bestCouponType = coupon.Type.ToString();
            }
        }

        if (best <= 0m)
        {
            return PromotionRuleResult.NotApplied(
                TypeKey,
                "用户所有优惠券均不满足门槛或已停用",
                EmptyMetadata);
        }

        var metadata = new Dictionary<string, string>
        {
            ["userCouponId"] = bestUserCouponId.ToString(),
            ["couponId"] = bestCouponId.ToString(),
            ["couponType"] = bestCouponType,
            ["discountAmount"] = best.ToString("F2")
        };

        _logger.LogInformation(
            "优惠券规则命中：用户券 {UserCouponId}（模板 {CouponId}，类型 {CouponType}），抵扣 {Discount}",
            bestUserCouponId, bestCouponId, bestCouponType, best);

        return PromotionRuleResult.AppliedResult(TypeKey, best, bestUserCouponId, metadata);
    }

    /// <summary>
    /// 按券类型与门槛计算可抵扣金额。不满足门槛或抵扣后为负返回 0。
    /// 算法与旧 <c>PromotionCalculateAppService.ComputeCouponDiscount</c> 完全一致，向后兼容。
    /// </summary>
    private static decimal ComputeCouponDiscount(CouponAggregate coupon, decimal orderTotal)
    {
        if (orderTotal < coupon.MinSpend)
        {
            return 0m;
        }

        return coupon.Type switch
        {
            CouponType.FixedAmount => Math.Min(coupon.FaceValue, orderTotal),
            CouponType.FullReduction => Math.Min(coupon.FaceValue, orderTotal),
            CouponType.Percentage => Math.Min(orderTotal * coupon.FaceValue / 100m, orderTotal),
            _ => 0m
        };
    }

    /// <summary>
    /// 从 <see cref="PromotionRuleContext.Attributes"/> 解析用户 Guid。
    /// 现有 <c>CalculateDiscountDto.UserId</c> 为 <c>Guid</c>，规则上下文用 <c>long</c>，需要通过 Attributes["UserGuid"] 桥接。
    /// </summary>
    private static Guid? TryGetUserGuid(PromotionRuleContext context)
    {
        if (context.Attributes.TryGetValue("UserGuid", out var userGuidStr)
            && Guid.TryParse(userGuidStr, out var userGuid)
            && userGuid != Guid.Empty)
        {
            return userGuid;
        }
        return null;
    }

    private static readonly IReadOnlyDictionary<string, string> EmptyMetadata =
        new Dictionary<string, string>(0);
}
