using Leno.Promotion.Domain.Aggregates;
using Leno.Promotion.Domain.Repositories;
using Leno.Promotion.Domain.Rules;
using Leno.Promotion.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using CouponAggregate = Leno.Promotion.Domain.Aggregates.Coupon;

namespace Leno.Promotion.Application.Services;

/// <summary>
/// 促销折扣试算服务实现。
/// 支持双路径：
/// <list type="bullet">
/// <item>规则引擎路径（<see cref="PromotionOptions.UseRuleEngine"/>=true）：构造 <see cref="PromotionRuleContext"/> 调用 <see cref="IRuleEngine"/> 编排所有规则，返回聚合折扣；</item>
/// <item>旧硬编码路径（<see cref="PromotionOptions.UseRuleEngine"/>=false，默认）：满减 + 优惠券直接相加，向后兼容。</item>
/// </list>
/// 旧路径方法保留 <c>[Obsolete]</c> 标记，灰度验证通过后将在后续版本移除。
/// </summary>
public sealed class PromotionCalculateAppService : IPromotionCalculateAppService
{
    private readonly IPromotionActivityRepository _promotionActivityRepository;
    private readonly IUserCouponRepository _userCouponRepository;
    private readonly ICouponRepository _couponRepository;
    private readonly IRuleEngine _ruleEngine;
    private readonly PromotionOptions _options;
    private readonly ILogger<PromotionCalculateAppService> _logger;

    public PromotionCalculateAppService(
        IPromotionActivityRepository promotionActivityRepository,
        IUserCouponRepository userCouponRepository,
        ICouponRepository couponRepository,
        IRuleEngine ruleEngine,
        IOptions<PromotionOptions> options,
        ILogger<PromotionCalculateAppService> logger)
    {
        _promotionActivityRepository = promotionActivityRepository ?? throw new ArgumentNullException(nameof(promotionActivityRepository));
        _userCouponRepository = userCouponRepository ?? throw new ArgumentNullException(nameof(userCouponRepository));
        _couponRepository = couponRepository ?? throw new ArgumentNullException(nameof(couponRepository));
        _ruleEngine = ruleEngine ?? throw new ArgumentNullException(nameof(ruleEngine));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<DiscountResultDto> CalculateDiscountAsync(CalculateDiscountDto input, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (input.UserId == Guid.Empty)
        {
            throw new ArgumentException("UserId 不可为空", nameof(input));
        }

        // feature flag 路由：UseRuleEngine=true 走规则引擎，否则走旧路径
        if (_options.UseRuleEngine)
        {
            _logger.LogDebug("UseRuleEngine=true，走规则引擎路径试算");
            return await CalculateViaRuleEngineAsync(input, ct);
        }

        _logger.LogDebug("UseRuleEngine=false，走旧硬编码试算路径");
#pragma warning disable CS0618 // 特性开关关闭时有意调用旧路径，灰度验证通过后将移除
        return await CalculateLegacyAsync(input, ct);
#pragma warning restore CS0618
    }

    /// <summary>
    /// 规则引擎路径：将 <see cref="CalculateDiscountDto"/> 映射为 <see cref="PromotionRuleContext"/>，
    /// 调用 <see cref="IRuleEngine.EvaluateAsync"/> 编排所有规则，返回折扣汇总。
    /// </summary>
    private async Task<DiscountResultDto> CalculateViaRuleEngineAsync(CalculateDiscountDto input, CancellationToken ct)
    {
        var context = MapToRuleContext(input);
        var result = await _ruleEngine.EvaluateAsync(context, ct);

        return new DiscountResultDto
        {
            TotalDiscountAmount = result.TotalDiscountAmount,
            Currency = result.Currency
        };
    }

    /// <summary>
    /// 将对外 DTO <see cref="CalculateDiscountDto"/> 映射为规则引擎上下文 <see cref="PromotionRuleContext"/>。
    /// 映射规则：
    /// <list type="bullet">
    /// <item>UserId：<see cref="CalculateDiscountDto.UserId"/>（Guid）放入 <see cref="PromotionRuleContext.Attributes"/>["UserGuid"]，供 <c>CouponRule</c> 解析；</item>
    /// <item>Items：每行 <see cref="DiscountItemInput.Subtotal"/> 映射为 Quantity=1、UnitPrice=Subtotal 的 <see cref="CartItemContext"/>，保留行小计；</item>
    /// <item>SubTotal：所有行小计之和；</item>
    /// <item>SellerId：0（多卖家聚合，DTO 未携带卖家维度）；</item>
    /// <item>CouponCode/SeckillActivityId：null（DTO 未携带）。</item>
    /// </list>
    /// </summary>
    private static PromotionRuleContext MapToRuleContext(CalculateDiscountDto input)
    {
        var items = (input.Items ?? Enumerable.Empty<DiscountItemInput>())
            .Select(i => new CartItemContext
            {
                SkuId = i.SkuId,
                Quantity = 1,
                UnitPrice = i.Subtotal,
                CategoryCode = null
            })
            .ToList();

        var subTotal = items.Sum(i => i.Subtotal);

        var attributes = new Dictionary<string, string>
        {
            ["UserGuid"] = input.UserId.ToString()
        };

        return new PromotionRuleContext
        {
            UserId = 0,
            SellerId = 0,
            Items = items,
            SubTotal = subTotal,
            CouponCode = null,
            SeckillActivityId = null,
            Attributes = attributes
        };
    }

    /// <summary>
    /// 旧硬编码试算路径：满减活动 + 优惠券直接相加。
    /// 算法与 v1 实现完全一致，保留用于灰度回退与回归对比。
    /// 灰度验证通过后将在后续版本移除。
    /// </summary>
    [Obsolete("使用规则引擎路径替代，灰度验证通过后将移除。设置 Promotion:UseRuleEngine=true 启用新路径。")]
    private async Task<DiscountResultDto> CalculateLegacyAsync(CalculateDiscountDto input, CancellationToken ct)
    {
        var orderTotal = input.Items?.Sum(i => i.Subtotal) ?? 0m;
        var now = DateTime.UtcNow;

        // 1. 满减活动：取当前进行中活动里命中最高档的最大减免
        var activityDiscount = await CalculateActivityDiscountAsync(orderTotal, now, ct);

        // 2. 用户优惠券：取 Unused 且未过期券中抵扣最大者
        var couponDiscount = await CalculateCouponDiscountAsync(input.UserId, orderTotal, now, ct);

        return new DiscountResultDto
        {
            TotalDiscountAmount = activityDiscount + couponDiscount,
            Currency = "CNY"
        };
    }

    private async Task<decimal> CalculateActivityDiscountAsync(decimal orderTotal, DateTime now, CancellationToken ct)
    {
        if (orderTotal <= 0m)
        {
            return 0m;
        }

        var activities = await _promotionActivityRepository.GetActiveAsync(now, ct);
        if (activities.Count == 0)
        {
            return 0m;
        }

        // PromotionActivity.CalculateDiscount 已校验状态与时间窗口，并命中最高档规则
        decimal best = 0m;
        foreach (var activity in activities)
        {
            var discount = activity.CalculateDiscount(orderTotal);
            if (discount > best)
            {
                best = discount;
            }
        }

        return best;
    }

    private async Task<decimal> CalculateCouponDiscountAsync(Guid userId, decimal orderTotal, DateTime now, CancellationToken ct)
    {
        if (orderTotal <= 0m)
        {
            return 0m;
        }

        // P1-10：ExpiredAt > now 下推到 SQL，消除内存过滤（原 .Where(uc => !uc.IsExpiredAt(now)) 已移除）
        var userCoupons = await _userCouponRepository.GetByUserAsync(userId, CouponStatus.Unused, now, ct);
        if (userCoupons.Count == 0)
        {
            return 0m;
        }

        // 一次性批量加载所有券模板，消除 N+1 DB 查询（原实现循环内逐个 GetByIdAsync）
        var couponIds = userCoupons.Select(uc => uc.CouponId).Distinct().ToList();
        var coupons = await _couponRepository.GetByIdsAsync(couponIds, ct);
        var couponMap = coupons
            .Where(c => c.Status == CouponTemplateStatus.Enabled)
            .ToDictionary(c => c.Id);

        decimal best = 0m;
        foreach (var userCoupon in userCoupons)
        {
            if (!couponMap.TryGetValue(userCoupon.CouponId, out var coupon))
            {
                continue;
            }

            var discount = ComputeCouponDiscount(coupon, orderTotal);
            if (discount > best)
            {
                best = discount;
            }
        }

        return best;
    }

    /// <summary>
    /// 按券类型与门槛计算可抵扣金额。不满足门槛或抵扣后为负返回 0。
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
}
