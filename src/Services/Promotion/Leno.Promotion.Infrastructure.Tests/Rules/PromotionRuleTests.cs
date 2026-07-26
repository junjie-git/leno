using Leno.Promotion.Domain.Aggregates;
using Leno.Promotion.Domain.Repositories;
using Leno.Promotion.Domain.Rules;
using Leno.Promotion.Domain.ValueObjects;
using Leno.Promotion.Infrastructure.Rules;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Leno.Promotion.Infrastructure.Tests.Rules;

/// <summary>
/// FullReductionRule / CouponRule / SeckillDiscountRule 三个规则实现的单元测试。
/// 覆盖：
/// - IsApplicableAsync 过滤逻辑（金额、卖家、类目）
/// - EvaluateAsync 折扣计算与命中档位
/// - 边界条件（空购物车、无活动、不满足门槛）
/// </summary>
public class FullReductionRuleTests
{
    private static readonly ILogger<FullReductionRule> Logger = NullLogger<FullReductionRule>.Instance;

    private static PromotionRuleContext CreateContext(decimal subTotal, long sellerId = 0)
    {
        return new PromotionRuleContext
        {
            UserId = 1,
            SellerId = sellerId,
            Items = new List<CartItemContext>
            {
                new() { SkuId = Guid.NewGuid(), Quantity = 1, UnitPrice = subTotal }
            },
            SubTotal = subTotal,
            Attributes = new Dictionary<string, string>()
        };
    }

    private static PromotionActivity CreateActiveActivity(decimal threshold, decimal discount)
    {
        var activity = PromotionActivity.Create(
            Guid.NewGuid(), "测试满减", PromotionType.FullReduction,
            DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddHours(1));
        activity.Activate();
        activity.AddRule(threshold, discount);
        return activity;
    }

    [Fact]
    public async Task IsApplicableAsync_PositiveSubTotal_ReturnsTrue()
    {
        var repoMock = new Mock<IPromotionActivityRepository>();
        var loaderMock = new Mock<IJsonRuleLoader>();
        var rule = new FullReductionRule(repoMock.Object, loaderMock.Object, Logger);

        var result = await rule.IsApplicableAsync(CreateContext(100m), CancellationToken.None);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsApplicableAsync_ZeroSubTotal_ReturnsFalse()
    {
        var repoMock = new Mock<IPromotionActivityRepository>();
        var loaderMock = new Mock<IJsonRuleLoader>();
        var rule = new FullReductionRule(repoMock.Object, loaderMock.Object, Logger);

        var result = await rule.IsApplicableAsync(CreateContext(0m), CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task EvaluateAsync_HitsHighestThreshold_ReturnsDiscount()
    {
        var activity1 = CreateActiveActivity(100m, 10m);
        var activity2 = CreateActiveActivity(200m, 50m);

        var repoMock = new Mock<IPromotionActivityRepository>();
        repoMock.Setup(r => r.GetActiveAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PromotionActivity> { activity1, activity2 });

        var loaderMock = new Mock<IJsonRuleLoader>();
        var rule = new FullReductionRule(repoMock.Object, loaderMock.Object, Logger);

        var result = await rule.EvaluateAsync(CreateContext(250m), CancellationToken.None);

        result.Applied.Should().BeTrue();
        result.DiscountAmount.Should().Be(50m);
        result.Metadata.Should().ContainKey("activityId");
    }

    [Fact]
    public async Task EvaluateAsync_NoActivities_ReturnsNotApplied()
    {
        var repoMock = new Mock<IPromotionActivityRepository>();
        repoMock.Setup(r => r.GetActiveAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PromotionActivity>());

        var loaderMock = new Mock<IJsonRuleLoader>();
        var rule = new FullReductionRule(repoMock.Object, loaderMock.Object, Logger);

        var result = await rule.EvaluateAsync(CreateContext(100m), CancellationToken.None);

        result.Applied.Should().BeFalse();
        result.DiscountAmount.Should().Be(0m);
    }

    [Fact]
    public async Task EvaluateAsync_BelowAllThresholds_ReturnsNotApplied()
    {
        var activity = CreateActiveActivity(500m, 100m);

        var repoMock = new Mock<IPromotionActivityRepository>();
        repoMock.Setup(r => r.GetActiveAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PromotionActivity> { activity });

        var loaderMock = new Mock<IJsonRuleLoader>();
        var rule = new FullReductionRule(repoMock.Object, loaderMock.Object, Logger);

        var result = await rule.EvaluateAsync(CreateContext(100m), CancellationToken.None);

        result.Applied.Should().BeFalse();
        result.DiscountAmount.Should().Be(0m);
    }

    [Fact]
    public async Task EvaluateAsync_MultipleActivities_TakesMaxDiscount()
    {
        // 两个活动都命中，取折扣最大者
        var activity1 = CreateActiveActivity(100m, 30m);
        var activity2 = CreateActiveActivity(100m, 50m);

        var repoMock = new Mock<IPromotionActivityRepository>();
        repoMock.Setup(r => r.GetActiveAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PromotionActivity> { activity1, activity2 });

        var loaderMock = new Mock<IJsonRuleLoader>();
        var rule = new FullReductionRule(repoMock.Object, loaderMock.Object, Logger);

        var result = await rule.EvaluateAsync(CreateContext(150m), CancellationToken.None);

        result.Applied.Should().BeTrue();
        result.DiscountAmount.Should().Be(50m);
    }

    [Fact]
    public async Task EvaluateAsync_ZeroSubTotal_ReturnsNotApplied()
    {
        var repoMock = new Mock<IPromotionActivityRepository>();
        var loaderMock = new Mock<IJsonRuleLoader>();
        var rule = new FullReductionRule(repoMock.Object, loaderMock.Object, Logger);

        var result = await rule.EvaluateAsync(CreateContext(0m), CancellationToken.None);

        result.Applied.Should().BeFalse();
    }
}

public class CouponRuleTests
{
    private static readonly ILogger<CouponRule> Logger = NullLogger<CouponRule>.Instance;
    private static readonly Guid UserGuid = Guid.NewGuid();

    private static PromotionRuleContext CreateContext(decimal subTotal)
    {
        return new PromotionRuleContext
        {
            UserId = 1,
            SellerId = 0,
            Items = new List<CartItemContext>
            {
                new() { SkuId = Guid.NewGuid(), Quantity = 1, UnitPrice = subTotal }
            },
            SubTotal = subTotal,
            Attributes = new Dictionary<string, string> { ["UserGuid"] = UserGuid.ToString() }
        };
    }

    private static Coupon CreateCoupon(CouponType type, decimal faceValue, decimal minSpend)
    {
        return Coupon.Create(
            Guid.NewGuid(), "测试券", type, faceValue, minSpend,
            CouponValidityType.FixedPeriod,
            DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1),
            null, 100);
    }

    private static UserCoupon CreateUserCoupon(Guid couponId)
    {
        return UserCoupon.Receive(
            Guid.NewGuid(), UserGuid, couponId, "Manual",
            DateTime.UtcNow.AddDays(1));
    }

    [Fact]
    public async Task IsApplicableAsync_NoUserGuid_ReturnsFalse()
    {
        var userCouponRepoMock = new Mock<IUserCouponRepository>();
        var couponRepoMock = new Mock<ICouponRepository>();
        var loaderMock = new Mock<IJsonRuleLoader>();
        var rule = new CouponRule(userCouponRepoMock.Object, couponRepoMock.Object, loaderMock.Object, Logger);

        var context = new PromotionRuleContext
        {
            UserId = 1,
            SellerId = 0,
            Items = new List<CartItemContext>
            {
                new() { SkuId = Guid.NewGuid(), Quantity = 1, UnitPrice = 100m }
            },
            SubTotal = 100m,
            Attributes = new Dictionary<string, string>()
        };

        var result = await rule.IsApplicableAsync(context, CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task EvaluateAsync_FixedAmountCoupon_ReturnsDiscount()
    {
        var coupon = CreateCoupon(CouponType.FixedAmount, 20m, 50m);
        var userCoupon = CreateUserCoupon(coupon.Id);

        var userCouponRepoMock = new Mock<IUserCouponRepository>();
        userCouponRepoMock.Setup(r => r.GetByUserAsync(UserGuid, CouponStatus.Unused, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserCoupon> { userCoupon });

        var couponRepoMock = new Mock<ICouponRepository>();
        couponRepoMock.Setup(r => r.GetByIdsAsync(It.IsAny<List<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Coupon> { coupon });

        var loaderMock = new Mock<IJsonRuleLoader>();
        var rule = new CouponRule(userCouponRepoMock.Object, couponRepoMock.Object, loaderMock.Object, Logger);

        var result = await rule.EvaluateAsync(CreateContext(100m), CancellationToken.None);

        result.Applied.Should().BeTrue();
        result.DiscountAmount.Should().Be(20m);
        result.AppliedCouponId.Should().Be(userCoupon.Id);
    }

    [Fact]
    public async Task EvaluateAsync_PercentageCoupon_CalculatesProportionalDiscount()
    {
        var coupon = CreateCoupon(CouponType.Percentage, 10m, 0m);
        var userCoupon = CreateUserCoupon(coupon.Id);

        var userCouponRepoMock = new Mock<IUserCouponRepository>();
        userCouponRepoMock.Setup(r => r.GetByUserAsync(UserGuid, CouponStatus.Unused, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserCoupon> { userCoupon });

        var couponRepoMock = new Mock<ICouponRepository>();
        couponRepoMock.Setup(r => r.GetByIdsAsync(It.IsAny<List<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Coupon> { coupon });

        var loaderMock = new Mock<IJsonRuleLoader>();
        var rule = new CouponRule(userCouponRepoMock.Object, couponRepoMock.Object, loaderMock.Object, Logger);

        var result = await rule.EvaluateAsync(CreateContext(200m), CancellationToken.None);

        result.Applied.Should().BeTrue();
        // 10% of 200 = 20
        result.DiscountAmount.Should().Be(20m);
    }

    [Fact]
    public async Task EvaluateAsync_BelowMinSpend_ReturnsNotApplied()
    {
        var coupon = CreateCoupon(CouponType.FixedAmount, 50m, 200m);
        var userCoupon = CreateUserCoupon(coupon.Id);

        var userCouponRepoMock = new Mock<IUserCouponRepository>();
        userCouponRepoMock.Setup(r => r.GetByUserAsync(UserGuid, CouponStatus.Unused, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserCoupon> { userCoupon });

        var couponRepoMock = new Mock<ICouponRepository>();
        couponRepoMock.Setup(r => r.GetByIdsAsync(It.IsAny<List<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Coupon> { coupon });

        var loaderMock = new Mock<IJsonRuleLoader>();
        var rule = new CouponRule(userCouponRepoMock.Object, couponRepoMock.Object, loaderMock.Object, Logger);

        var result = await rule.EvaluateAsync(CreateContext(100m), CancellationToken.None);

        result.Applied.Should().BeFalse();
        result.DiscountAmount.Should().Be(0m);
    }

    [Fact]
    public async Task EvaluateAsync_NoUserCoupons_ReturnsNotApplied()
    {
        var userCouponRepoMock = new Mock<IUserCouponRepository>();
        userCouponRepoMock.Setup(r => r.GetByUserAsync(UserGuid, CouponStatus.Unused, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserCoupon>());

        var couponRepoMock = new Mock<ICouponRepository>();
        var loaderMock = new Mock<IJsonRuleLoader>();
        var rule = new CouponRule(userCouponRepoMock.Object, couponRepoMock.Object, loaderMock.Object, Logger);

        var result = await rule.EvaluateAsync(CreateContext(100m), CancellationToken.None);

        result.Applied.Should().BeFalse();
    }

    [Fact]
    public async Task EvaluateAsync_MultipleCoupons_TakesMaxDiscount()
    {
        var coupon1 = CreateCoupon(CouponType.FixedAmount, 10m, 0m);
        var coupon2 = CreateCoupon(CouponType.FixedAmount, 30m, 0m);
        var userCoupon1 = CreateUserCoupon(coupon1.Id);
        var userCoupon2 = CreateUserCoupon(coupon2.Id);

        var userCouponRepoMock = new Mock<IUserCouponRepository>();
        userCouponRepoMock.Setup(r => r.GetByUserAsync(UserGuid, CouponStatus.Unused, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserCoupon> { userCoupon1, userCoupon2 });

        var couponRepoMock = new Mock<ICouponRepository>();
        couponRepoMock.Setup(r => r.GetByIdsAsync(It.IsAny<List<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Coupon> { coupon1, coupon2 });

        var loaderMock = new Mock<IJsonRuleLoader>();
        var rule = new CouponRule(userCouponRepoMock.Object, couponRepoMock.Object, loaderMock.Object, Logger);

        var result = await rule.EvaluateAsync(CreateContext(100m), CancellationToken.None);

        result.Applied.Should().BeTrue();
        result.DiscountAmount.Should().Be(30m);
        result.AppliedCouponId.Should().Be(userCoupon2.Id);
    }
}

public class SeckillDiscountRuleTests
{
    private static readonly ILogger<SeckillDiscountRule> Logger = NullLogger<SeckillDiscountRule>.Instance;
    private static readonly Guid SkuId = Guid.NewGuid();

    private static SeckillActivity CreateActiveSeckill()
    {
        var activity = SeckillActivity.Create(
            Guid.NewGuid(), "测试秒杀活动", Guid.NewGuid(), SkuId,
            99m, 199m, 100, 1,
            DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddHours(1));
        activity.Activate();
        return activity;
    }

    private static PromotionRuleContext CreateContext(decimal subTotal, string? seckillActivityId = null)
    {
        return new PromotionRuleContext
        {
            UserId = 1,
            SellerId = 0,
            Items = new List<CartItemContext>
            {
                new() { SkuId = SkuId, Quantity = 2, UnitPrice = subTotal / 2 }
            },
            SubTotal = subTotal,
            SeckillActivityId = seckillActivityId,
            Attributes = new Dictionary<string, string>()
        };
    }

    [Fact]
    public async Task IsApplicableAsync_NoSeckillActivityId_ReturnsFalse()
    {
        var repoMock = new Mock<ISeckillActivityRepository>();
        var loaderMock = new Mock<IJsonRuleLoader>();
        var rule = new SeckillDiscountRule(repoMock.Object, loaderMock.Object, Logger);

        var result = await rule.IsApplicableAsync(CreateContext(100m, null), CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsApplicableAsync_WithSeckillActivityId_ReturnsTrue()
    {
        var repoMock = new Mock<ISeckillActivityRepository>();
        var loaderMock = new Mock<IJsonRuleLoader>();
        var rule = new SeckillDiscountRule(repoMock.Object, loaderMock.Object, Logger);

        var result = await rule.IsApplicableAsync(CreateContext(100m, Guid.NewGuid().ToString()), CancellationToken.None);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateAsync_MatchingSku_ReturnsDiscount()
    {
        var activity = CreateActiveSeckill();

        var repoMock = new Mock<ISeckillActivityRepository>();
        repoMock.Setup(r => r.GetByIdAsync(activity.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(activity);

        var loaderMock = new Mock<IJsonRuleLoader>();
        var rule = new SeckillDiscountRule(repoMock.Object, loaderMock.Object, Logger);

        // 购物车 2 件，原价 199 * 2 = 398，秒杀价 99 * 2 = 198，折扣 = 398 - 198 = 200
        var context = CreateContext(198m, activity.Id.ToString());

        var result = await rule.EvaluateAsync(context, CancellationToken.None);

        result.Applied.Should().BeTrue();
        result.DiscountAmount.Should().Be(200m);
        result.Metadata.Should().ContainKey("activityId");
        result.Metadata["activityId"].Should().Be(activity.Id.ToString());
    }

    [Fact]
    public async Task EvaluateAsync_ActivityNotFound_ReturnsNotApplied()
    {
        var activityId = Guid.NewGuid();
        var repoMock = new Mock<ISeckillActivityRepository>();
        repoMock.Setup(r => r.GetByIdAsync(activityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SeckillActivity?)null);

        var loaderMock = new Mock<IJsonRuleLoader>();
        var rule = new SeckillDiscountRule(repoMock.Object, loaderMock.Object, Logger);

        var result = await rule.EvaluateAsync(CreateContext(100m, activityId.ToString()), CancellationToken.None);

        result.Applied.Should().BeFalse();
    }

    [Fact]
    public async Task EvaluateAsync_SkuMismatch_ReturnsNotApplied()
    {
        var activity = CreateActiveSeckill();
        var differentSku = Guid.NewGuid();

        var repoMock = new Mock<ISeckillActivityRepository>();
        repoMock.Setup(r => r.GetByIdAsync(activity.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(activity);

        var loaderMock = new Mock<IJsonRuleLoader>();
        var rule = new SeckillDiscountRule(repoMock.Object, loaderMock.Object, Logger);

        var context = new PromotionRuleContext
        {
            UserId = 1,
            SellerId = 0,
            Items = new List<CartItemContext>
            {
                new() { SkuId = differentSku, Quantity = 1, UnitPrice = 100m }
            },
            SubTotal = 100m,
            SeckillActivityId = activity.Id.ToString(),
            Attributes = new Dictionary<string, string>()
        };

        var result = await rule.EvaluateAsync(context, CancellationToken.None);

        result.Applied.Should().BeFalse();
    }

    [Fact]
    public async Task EvaluateAsync_ZeroItems_ReturnsNotApplied()
    {
        var repoMock = new Mock<ISeckillActivityRepository>();
        var loaderMock = new Mock<IJsonRuleLoader>();
        var rule = new SeckillDiscountRule(repoMock.Object, loaderMock.Object, Logger);

        var context = new PromotionRuleContext
        {
            UserId = 1,
            SellerId = 0,
            Items = new List<CartItemContext>(),
            SubTotal = 0m,
            SeckillActivityId = Guid.NewGuid().ToString(),
            Attributes = new Dictionary<string, string>()
        };

        var result = await rule.EvaluateAsync(context, CancellationToken.None);

        result.Applied.Should().BeFalse();
    }
}
