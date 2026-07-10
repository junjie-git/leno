using Leno.Promotion.Domain.Aggregates;
using Leno.Promotion.Domain.Repositories;
using Leno.Promotion.Domain.ValueObjects;
using CouponAggregate = Leno.Promotion.Domain.Aggregates.Coupon;

namespace Leno.Promotion.Application.Services;

/// <summary>
/// 促销折扣试算服务实现。
/// 试算逻辑（合理近似）：
/// 1. 汇总订单行小计得到订单总金额；
/// 2. 查询当前 Active 且在有效时间区间内的满减活动，命中最高档规则的活动取最大减免；
/// 3. 查询用户 Unused 且未过期的优惠券，按券类型计算可抵扣金额，取最大值；
/// 4. 返回活动减免与最优券抵扣之和。
/// </summary>
public sealed class PromotionCalculateAppService : IPromotionCalculateAppService
{
    private readonly IPromotionActivityRepository _promotionActivityRepository;
    private readonly IUserCouponRepository _userCouponRepository;
    private readonly ICouponRepository _couponRepository;

    public PromotionCalculateAppService(
        IPromotionActivityRepository promotionActivityRepository,
        IUserCouponRepository userCouponRepository,
        ICouponRepository couponRepository)
    {
        _promotionActivityRepository = promotionActivityRepository;
        _userCouponRepository = userCouponRepository;
        _couponRepository = couponRepository;
    }

    /// <inheritdoc />
    public async Task<DiscountResultDto> CalculateDiscountAsync(CalculateDiscountDto input, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (input.UserId == Guid.Empty)
        {
            throw new ArgumentException("UserId 不可为空", nameof(input));
        }

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

        var userCoupons = await _userCouponRepository.GetByUserAsync(userId, CouponStatus.Unused, ct);
        if (userCoupons.Count == 0)
        {
            return 0m;
        }

        decimal best = 0m;
        foreach (var userCoupon in userCoupons)
        {
            // 过滤已过期（防御性：聚合状态可能未及时同步为 Expired）
            if (userCoupon.IsExpiredAt(now))
            {
                continue;
            }

            var coupon = await _couponRepository.GetByIdAsync(userCoupon.CouponId, ct);
            if (coupon is null || coupon.Status != CouponTemplateStatus.Enabled)
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
