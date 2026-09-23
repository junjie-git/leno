using Leno.Promotion.Application;
using Leno.Promotion.Application.Services;
using Leno.Promotion.Domain.Aggregates;
using Leno.Promotion.Domain.Repositories;
using Leno.Promotion.Domain.Rules;
using Leno.Promotion.Domain.ValueObjects;
using Leno.Promotion.Infrastructure.Rules;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Leno.Promotion.Infrastructure.Tests.Differential;

/// <summary>
/// 促销折扣试算 —— 规则引擎唯一路径的回归测试（双轨下线 D3 收口，2026-09-23）。
/// <para>
/// 本测试的前身是「旧路径 vs 规则引擎」差分对照测试：旧硬编码路径删除前，
/// 六组语料已通过差分门禁（金币案例断言两路径完全相等；门槛跨界案例确认差异为有意语义）。
/// 旧路径与 <c>Promotion:UseRuleEngine</c> 删除后，本测试以显式期望值钉住引擎行为。
/// </para>
/// <para>语义基准：券门槛按<b>前序规则扣减后的剩余金额</b>判定（Stackable 逐级扣减）。</para>
/// </summary>
public class RuleEngineDiscountRegressionTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    /// <summary>测试用规则定义加载器：始终返回 null（规则回退默认优先级与 Stackable 叠加）。</summary>
    private sealed class NullJsonRuleLoader : IJsonRuleLoader
    {
        public Task LoadAsync(CancellationToken ct = default) => Task.CompletedTask;

        public Task ReloadAsync(CancellationToken ct = default) => Task.CompletedTask;

        public JsonRuleDefinition? GetDefinition(string ruleType) => null;

        public long CachedVersion => 0;

        public DateTime? LoadedAt => null;
    }

    private static IPromotionCalculateAppService CreateSut(
        IEnumerable<PromotionActivity> activities,
        IEnumerable<UserCoupon> userCoupons,
        IEnumerable<Coupon> coupons)
    {
        var activityRepo = new Mock<IPromotionActivityRepository>();
        activityRepo.Setup(r => r.GetActiveAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(activities.ToList());

        var userCouponRepo = new Mock<IUserCouponRepository>();
        userCouponRepo.Setup(r => r.GetByUserAsync(
                It.IsAny<Guid>(), It.IsAny<CouponStatus?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(userCoupons.ToList());

        var couponRepo = new Mock<ICouponRepository>();
        couponRepo.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(coupons.ToList());

        var loader = new NullJsonRuleLoader();
        var engine = new RuleEngine(
            new IPromotionRule[]
            {
                new FullReductionRule(activityRepo.Object, loader, NullLogger<FullReductionRule>.Instance),
                new CouponRule(userCouponRepo.Object, couponRepo.Object, loader, NullLogger<CouponRule>.Instance)
            },
            NullLogger<RuleEngine>.Instance);

        return new PromotionCalculateAppService(engine, NullLogger<PromotionCalculateAppService>.Instance);
    }

    private static CalculateDiscountDto Order(decimal subtotal) => new()
    {
        UserId = UserId,
        Items = [new DiscountItemInput { SkuId = Guid.NewGuid(), Subtotal = subtotal }]
    };

    private static PromotionActivity ActiveActivity(params (decimal Threshold, decimal Discount)[] tiers)
    {
        var activity = PromotionActivity.Create(
            Guid.NewGuid(), "满减活动", PromotionType.FullReduction,
            DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddHours(1));
        activity.Activate();
        foreach (var (threshold, discount) in tiers)
        {
            activity.AddRule(threshold, discount);
        }

        return activity;
    }

    private static (Coupon Coupon, UserCoupon UserCoupon) CouponPair(
        CouponType type, decimal faceValue, decimal minSpend)
    {
        var coupon = Coupon.Create(
            Guid.NewGuid(), "测试券", type, faceValue, minSpend,
            CouponValidityType.FixedPeriod,
            DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(30), null, -1);
        var userCoupon = UserCoupon.Receive(
            Guid.NewGuid(), UserId, coupon.Id, "test", DateTime.UtcNow.AddDays(30));
        return (coupon, userCoupon);
    }

    [Fact]
    public async Task 仅满减_命中最高档()
    {
        var sut = CreateSut([ActiveActivity((100m, 10m), (200m, 30m))], [], []);

        var result = await sut.CalculateDiscountAsync(Order(250m));

        result.TotalDiscountAmount.Should().Be(30m);
        result.Currency.Should().Be("CNY");
    }

    [Fact]
    public async Task 仅优惠券_固定额取券面值()
    {
        var (coupon, userCoupon) = CouponPair(CouponType.FixedAmount, 5m, 50m);
        var sut = CreateSut([], [userCoupon], [coupon]);

        var result = await sut.CalculateDiscountAsync(Order(150m));

        result.TotalDiscountAmount.Should().Be(5m);
    }

    [Fact]
    public async Task 百分比券_按剩余金额计算()
    {
        var (coupon, userCoupon) = CouponPair(CouponType.Percentage, 10m, 0m);
        var sut = CreateSut([], [userCoupon], [coupon]);

        var result = await sut.CalculateDiscountAsync(Order(200m));

        result.TotalDiscountAmount.Should().Be(20m);
    }

    [Fact]
    public async Task 满减叠加固定额券_门槛在折后金额下满足()
    {
        // 满 100 减 10 后剩余 140，券门槛 50 满足 → 10 + 5
        var (coupon, userCoupon) = CouponPair(CouponType.FixedAmount, 5m, 50m);
        var sut = CreateSut([ActiveActivity((100m, 10m))], [userCoupon], [coupon]);

        var result = await sut.CalculateDiscountAsync(Order(150m));

        result.TotalDiscountAmount.Should().Be(15m);
    }

    [Fact]
    public async Task 券门槛按折后金额判定_跨界时不叠加()
    {
        // 满 100 减 50 后剩余 100 < 券门槛 120 → 券不适用，仅 50
        // （D3 差分门禁固化的语义：旧路径曾按原价判定并叠加为 70，属有意变更）
        var (coupon, userCoupon) = CouponPair(CouponType.FixedAmount, 20m, 120m);
        var sut = CreateSut([ActiveActivity((100m, 50m))], [userCoupon], [coupon]);

        var result = await sut.CalculateDiscountAsync(Order(150m));

        result.TotalDiscountAmount.Should().Be(50m);
    }

    [Fact]
    public async Task 券不满足门槛_仅满减生效()
    {
        var (coupon, userCoupon) = CouponPair(CouponType.FixedAmount, 20m, 200m);
        var sut = CreateSut([ActiveActivity((100m, 10m))], [userCoupon], [coupon]);

        var result = await sut.CalculateDiscountAsync(Order(150m));

        result.TotalDiscountAmount.Should().Be(10m);
    }

    [Fact]
    public async Task 零金额订单_折扣为零()
    {
        var (coupon, userCoupon) = CouponPair(CouponType.FixedAmount, 5m, 0m);
        var sut = CreateSut([ActiveActivity((100m, 10m))], [userCoupon], [coupon]);

        var result = await sut.CalculateDiscountAsync(Order(0m));

        result.TotalDiscountAmount.Should().Be(0m);
    }

    [Fact]
    public async Task 无任何活动与券_折扣为零()
    {
        var sut = CreateSut([], [], []);

        var result = await sut.CalculateDiscountAsync(Order(150m));

        result.TotalDiscountAmount.Should().Be(0m);
    }

    [Fact]
    public async Task 用户标识为空_抛出参数异常()
    {
        var sut = CreateSut([], [], []);
        var input = new CalculateDiscountDto { UserId = Guid.Empty, Items = [] };

        var act = async () => await sut.CalculateDiscountAsync(input);

        await act.Should().ThrowAsync<ArgumentException>();
    }
}
