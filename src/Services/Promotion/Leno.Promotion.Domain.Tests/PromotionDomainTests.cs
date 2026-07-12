﻿using Leno.Promotion.Domain.Aggregates;
using Leno.Promotion.Domain.Events;
using Leno.Promotion.Domain.Exceptions;
using Leno.Promotion.Domain.Services;
using Leno.Promotion.Domain.ValueObjects;

namespace Leno.Promotion.Domain.Tests;

public class SeckillActivityTests
{
    private static readonly Guid SpuId = Guid.NewGuid();
    private static readonly Guid SkuId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public void Create_ValidInput_ShouldCreatePendingActivity()
    {
        var activity = CreateActivity();

        activity.Status.Should().Be(SeckillStatus.Pending);
        activity.SpuId.Should().Be(SpuId);
        activity.SkuId.Should().Be(SkuId);
        activity.AvailableStock.Should().Be(100);
    }

    [Fact]
    public void Create_SeckillPriceNotLessThanOriginal_ShouldThrowException()
    {
        var act = () => SeckillActivity.Create(
            Guid.NewGuid(), SpuId, SkuId, 199m, 100m, 100, 1,
            DateTime.UtcNow.AddHours(1), DateTime.UtcNow.AddHours(2));

        act.Should().Throw<PromotionDomainException>().WithMessage("*小于*");
    }

    [Fact]
    public void Create_EmptySpuId_ShouldThrowException()
    {
        var act = () => SeckillActivity.Create(
            Guid.NewGuid(), Guid.Empty, SkuId, 99m, 199m, 100, 1,
            DateTime.UtcNow.AddHours(1), DateTime.UtcNow.AddHours(2));

        act.Should().Throw<PromotionDomainException>().WithMessage("*SpuId*");
    }

    [Fact]
    public void Create_EmptySkuId_ShouldThrowException()
    {
        var act = () => SeckillActivity.Create(
            Guid.NewGuid(), SpuId, Guid.Empty, 99m, 199m, 100, 1,
            DateTime.UtcNow.AddHours(1), DateTime.UtcNow.AddHours(2));

        act.Should().Throw<PromotionDomainException>().WithMessage("*SkuId*");
    }

    [Fact]
    public void Create_ZeroSeckillPrice_ShouldThrowException()
    {
        var act = () => SeckillActivity.Create(
            Guid.NewGuid(), SpuId, SkuId, 0m, 199m, 100, 1,
            DateTime.UtcNow.AddHours(1), DateTime.UtcNow.AddHours(2));

        act.Should().Throw<PromotionDomainException>().WithMessage("*秒杀价*");
    }

    [Fact]
    public void Create_ZeroOriginalPrice_ShouldThrowException()
    {
        var act = () => SeckillActivity.Create(
            Guid.NewGuid(), SpuId, SkuId, 99m, 0m, 100, 1,
            DateTime.UtcNow.AddHours(1), DateTime.UtcNow.AddHours(2));

        act.Should().Throw<PromotionDomainException>().WithMessage("*原价*");
    }

    [Fact]
    public void Create_ZeroTotalStock_ShouldThrowException()
    {
        var act = () => SeckillActivity.Create(
            Guid.NewGuid(), SpuId, SkuId, 99m, 199m, 0, 1,
            DateTime.UtcNow.AddHours(1), DateTime.UtcNow.AddHours(2));

        act.Should().Throw<PromotionDomainException>().WithMessage("*库存*");
    }

    [Fact]
    public void Create_ZeroLimitPerUser_ShouldThrowException()
    {
        var act = () => SeckillActivity.Create(
            Guid.NewGuid(), SpuId, SkuId, 99m, 199m, 100, 0,
            DateTime.UtcNow.AddHours(1), DateTime.UtcNow.AddHours(2));

        act.Should().Throw<PromotionDomainException>().WithMessage("*限购*");
    }

    [Fact]
    public void Create_EndTimeNotAfterStartTime_ShouldThrowException()
    {
        var act = () => SeckillActivity.Create(
            Guid.NewGuid(), SpuId, SkuId, 99m, 199m, 100, 1,
            DateTime.UtcNow.AddHours(2), DateTime.UtcNow.AddHours(1));

        act.Should().Throw<PromotionDomainException>().WithMessage("*时间*");
    }

    [Fact]
    public void Create_EmptyActivityId_ShouldGenerateNewId()
    {
        var activity = SeckillActivity.Create(
            Guid.Empty, SpuId, SkuId, 99m, 199m, 100, 1,
            DateTime.UtcNow.AddHours(1), DateTime.UtcNow.AddHours(2));

        activity.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void Activate_Valid_ShouldTransitionToActive()
    {
        var activity = CreateActivity();

        activity.Activate();

        activity.Status.Should().Be(SeckillStatus.Active);
    }

    [Fact]
    public void Activate_NotPending_ShouldThrowException()
    {
        var activity = CreateActivity();
        activity.Activate();

        var act = () => activity.Activate();

        act.Should().Throw<PromotionDomainException>().WithMessage("*状态*");
    }

    [Fact]
    public void Close_Valid_ShouldTransitionToClosed()
    {
        var activity = CreateActivity();

        activity.Close();

        activity.Status.Should().Be(SeckillStatus.Closed);
    }

    [Fact]
    public void Close_AlreadyClosed_ShouldThrowException()
    {
        var activity = CreateActivity();
        activity.Close();

        var act = () => activity.Close();

        act.Should().Throw<PromotionDomainException>().WithMessage("*已关闭*");
    }

    [Fact]
    public void Close_FromActive_ShouldTransitionToClosed()
    {
        var activity = CreateActivity();
        activity.Activate();

        activity.Close();

        activity.Status.Should().Be(SeckillStatus.Closed);
    }

    [Fact]
    public void DeductStock_Valid_ShouldReduceAvailable()
    {
        var activity = CreateActivity();
        activity.Activate();

        activity.DeductStock(UserId, 30);

        activity.AvailableStock.Should().Be(70);
    }

    [Fact]
    public void DeductStock_WhenSoldOut_ShouldSetEnded()
    {
        var activity = CreateActivity();
        activity.Activate();

        activity.DeductStock(UserId, 100);

        activity.AvailableStock.Should().Be(0);
        activity.Status.Should().Be(SeckillStatus.Ended);
    }

    [Fact]
    public void DeductStock_NotActive_ShouldThrowException()
    {
        var activity = CreateActivity();

        var act = () => activity.DeductStock(UserId, 10);

        act.Should().Throw<PromotionDomainException>().WithMessage("*状态*");
    }

    [Fact]
    public void DeductStock_Insufficient_ShouldThrowException()
    {
        var activity = CreateActivity();
        activity.Activate();

        var act = () => activity.DeductStock(UserId, 200);

        act.Should().Throw<PromotionDomainException>().WithMessage("*不足*");
    }

    [Fact]
    public void DeductStock_EmptyUserId_ShouldThrowException()
    {
        var activity = CreateActivity();
        activity.Activate();

        var act = () => activity.DeductStock(Guid.Empty, 10);

        act.Should().Throw<PromotionDomainException>().WithMessage("*UserId*");
    }

    [Fact]
    public void DeductStock_ZeroQuantity_ShouldThrowException()
    {
        var activity = CreateActivity();
        activity.Activate();

        var act = () => activity.DeductStock(UserId, 0);

        act.Should().Throw<PromotionDomainException>().WithMessage("*数量*");
    }

    [Fact]
    public void DeductStock_NegativeQuantity_ShouldThrowException()
    {
        var activity = CreateActivity();
        activity.Activate();

        var act = () => activity.DeductStock(UserId, -1);

        act.Should().Throw<PromotionDomainException>().WithMessage("*数量*");
    }

    [Fact]
    public void DeductStock_OutsideActiveWindow_ShouldThrowException()
    {
        var activity = SeckillActivity.Create(
            Guid.NewGuid(), SpuId, SkuId, 99m, 199m, 100, 1,
            DateTime.UtcNow.AddHours(1), DateTime.UtcNow.AddHours(2));
        activity.Activate();

        var act = () => activity.DeductStock(UserId, 10);

        act.Should().Throw<PromotionDomainException>().WithMessage("*时间*");
    }

    [Fact]
    public void RecordOrderCreated_Valid_ShouldAddDomainEvent()
    {
        var activity = CreateActivity();
        var orderId = Guid.NewGuid();

        activity.RecordOrderCreated(UserId, orderId, 2);

        activity.DomainEvents.Should().HaveCount(1);
    }

    [Fact]
    public void RecordOrderCreated_EmptyUserId_ShouldThrowException()
    {
        var activity = CreateActivity();

        var act = () => activity.RecordOrderCreated(Guid.Empty, Guid.NewGuid(), 1);

        act.Should().Throw<PromotionDomainException>().WithMessage("*UserId*");
    }

    [Fact]
    public void RecordOrderCreated_EmptyOrderId_ShouldThrowException()
    {
        var activity = CreateActivity();

        var act = () => activity.RecordOrderCreated(UserId, Guid.Empty, 1);

        act.Should().Throw<PromotionDomainException>().WithMessage("*OrderId*");
    }

    [Fact]
    public void RestoreStock_Valid_ShouldIncreaseAvailable()
    {
        var activity = CreateActivity();
        activity.Activate();
        activity.DeductStock(UserId, 50);

        activity.RestoreStock(20);

        activity.AvailableStock.Should().Be(70);
    }

    [Fact]
    public void RestoreStock_ExceedTotal_ShouldThrowException()
    {
        var activity = CreateActivity();
        activity.Activate();

        var act = () => activity.RestoreStock(10);

        act.Should().Throw<PromotionDomainException>().WithMessage("*超过*");
    }

    [Fact]
    public void RestoreStock_AfterSoldOut_ShouldRestoreActive()
    {
        var activity = CreateActivity();
        activity.Activate();
        activity.DeductStock(UserId, 100);
        activity.Status.Should().Be(SeckillStatus.Ended);

        activity.RestoreStock(10);

        activity.Status.Should().Be(SeckillStatus.Active);
        activity.AvailableStock.Should().Be(10);
    }

    [Fact]
    public void RestoreStock_Closed_ShouldThrowException()
    {
        var activity = CreateActivity();
        activity.Activate();
        activity.DeductStock(UserId, 50);
        activity.Close();

        var act = () => activity.RestoreStock(10);

        act.Should().Throw<PromotionDomainException>().WithMessage("*已关闭*");
    }

    [Fact]
    public void RestoreStock_ZeroQuantity_ShouldThrowException()
    {
        var activity = CreateActivity();
        activity.Activate();
        activity.DeductStock(UserId, 50);

        var act = () => activity.RestoreStock(0);

        act.Should().Throw<PromotionDomainException>().WithMessage("*数量*");
    }

    [Fact]
    public void IsWithinActiveWindow_BeforeStart_ShouldReturnFalse()
    {
        var activity = SeckillActivity.Create(
            Guid.NewGuid(), SpuId, SkuId, 99m, 199m, 100, 1,
            DateTime.UtcNow.AddHours(1), DateTime.UtcNow.AddHours(2));

        activity.IsWithinActiveWindow(DateTime.UtcNow).Should().BeFalse();
    }

    [Fact]
    public void IsWithinActiveWindow_AfterEnd_ShouldReturnFalse()
    {
        var activity = SeckillActivity.Create(
            Guid.NewGuid(), SpuId, SkuId, 99m, 199m, 100, 1,
            DateTime.UtcNow.AddHours(-2), DateTime.UtcNow.AddHours(-1));

        activity.IsWithinActiveWindow(DateTime.UtcNow).Should().BeFalse();
    }

    [Fact]
    public void IsWithinActiveWindow_DuringWindow_ShouldReturnTrue()
    {
        var activity = CreateActivity();

        activity.IsWithinActiveWindow(DateTime.UtcNow).Should().BeTrue();
    }

    [Fact]
    public void DeductStock_SoldOutEvent_ShouldContainSkuId()
    {
        var activity = CreateActivity();
        activity.Activate();

        activity.DeductStock(UserId, 100);

        activity.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<SeckillStockSoldOutEvent>();
    }

    private static SeckillActivity CreateActivity()
    {
        return SeckillActivity.Create(
            Guid.NewGuid(), SpuId, SkuId, 99m, 199m, 100, 1,
            DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddHours(2));
    }
}

public class CouponTests
{
    [Fact]
    public void Create_ValidFixedAmount_ShouldCreate()
    {
        var coupon = Coupon.Create(
            Guid.NewGuid(), "满100减20", CouponType.FixedAmount, 20m, 100m,
            CouponValidityType.FixedPeriod,
            DateTime.UtcNow, DateTime.UtcNow.AddDays(30), null, 1000);

        coupon.Name.Should().Be("满100减20");
        coupon.Type.Should().Be(CouponType.FixedAmount);
        coupon.FaceValue.Should().Be(20m);
        coupon.Status.Should().Be(CouponTemplateStatus.Enabled);
        coupon.IssuedQty.Should().Be(0);
    }

    [Fact]
    public void Create_ValidRelativeDays_ShouldCreate()
    {
        var coupon = Coupon.Create(
            Guid.NewGuid(), "9折券", CouponType.Percentage, 10m, 0m,
            CouponValidityType.RelativeDays, null, null, 7, 500);

        coupon.ValidityType.Should().Be(CouponValidityType.RelativeDays);
        coupon.ValidDays.Should().Be(7);
    }

    [Fact]
    public void Create_PercentageOver100_ShouldThrowException()
    {
        var act = () => Coupon.Create(
            Guid.NewGuid(), "Invalid", CouponType.Percentage, 150m, 0m,
            CouponValidityType.RelativeDays, null, null, 7, 500);

        act.Should().Throw<PromotionDomainException>().WithMessage("*100*");
    }

    [Fact]
    public void Create_EmptyName_ShouldThrowException()
    {
        var act = () => Coupon.Create(
            Guid.NewGuid(), "", CouponType.FixedAmount, 20m, 0m,
            CouponValidityType.RelativeDays, null, null, 7, 500);

        act.Should().Throw<PromotionDomainException>().WithMessage("*名称*");
    }

    [Fact]
    public void Create_NegativeMinSpend_ShouldThrowException()
    {
        var act = () => Coupon.Create(
            Guid.NewGuid(), "Test", CouponType.FixedAmount, 20m, -1m,
            CouponValidityType.RelativeDays, null, null, 7, 500);

        act.Should().Throw<PromotionDomainException>().WithMessage("*门槛*");
    }

    [Fact]
    public void Issue_Valid_ShouldIncrementIssuedQty()
    {
        var coupon = CreateCoupon();

        coupon.Issue(10);

        coupon.IssuedQty.Should().Be(10);
    }

    [Fact]
    public void Issue_ExceedTotal_ShouldThrowException()
    {
        var coupon = CreateCoupon();
        coupon.Issue(100);

        var act = () => coupon.Issue(1);

        act.Should().Throw<PromotionDomainException>().WithMessage("*超出*");
    }

    [Fact]
    public void Issue_Disabled_ShouldThrowException()
    {
        var coupon = CreateCoupon();
        coupon.Disable();

        var act = () => coupon.Issue(1);

        act.Should().Throw<PromotionDomainException>().WithMessage("*停用*");
    }

    [Fact]
    public void Enable_Disabled_ShouldEnable()
    {
        var coupon = CreateCoupon();
        coupon.Disable();

        coupon.Enable();

        coupon.Status.Should().Be(CouponTemplateStatus.Enabled);
    }

    [Fact]
    public void Enable_AlreadyEnabled_ShouldThrowException()
    {
        var coupon = CreateCoupon();

        var act = () => coupon.Enable();

        act.Should().Throw<PromotionDomainException>().WithMessage("*已启用*");
    }

    [Fact]
    public void Disable_Enabled_ShouldDisable()
    {
        var coupon = CreateCoupon();

        coupon.Disable();

        coupon.Status.Should().Be(CouponTemplateStatus.Disabled);
    }

    [Fact]
    public void IsReceivable_Enabled_ShouldReturnTrue()
    {
        var coupon = CreateCoupon();

        var result = coupon.IsReceivable(DateTime.UtcNow);

        result.Should().BeTrue();
    }

    [Fact]
    public void IsReceivable_Disabled_ShouldReturnFalse()
    {
        var coupon = CreateCoupon();
        coupon.Disable();

        var result = coupon.IsReceivable(DateTime.UtcNow);

        result.Should().BeFalse();
    }

    [Fact]
    public void IsReceivable_SoldOut_ShouldReturnFalse()
    {
        var coupon = CreateCoupon();
        coupon.Issue(100);

        var result = coupon.IsReceivable(DateTime.UtcNow);

        result.Should().BeFalse();
    }

    private static Coupon CreateCoupon()
    {
        return Coupon.Create(
            Guid.NewGuid(), "满100减20", CouponType.FixedAmount, 20m, 100m,
            CouponValidityType.FixedPeriod,
            DateTime.UtcNow, DateTime.UtcNow.AddDays(30), null, 100);
    }
}

public class UserCouponTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid CouponId = Guid.NewGuid();

    [Fact]
    public void Receive_Valid_ShouldCreateUnused()
    {
        var uc = UserCoupon.Receive(Guid.NewGuid(), UserId, CouponId, "Manual", DateTime.UtcNow.AddDays(30));

        uc.Status.Should().Be(CouponStatus.Unused);
        uc.UserId.Should().Be(UserId);
        uc.CouponId.Should().Be(CouponId);
    }

    [Fact]
    public void Receive_EmptyUserId_ShouldThrowException()
    {
        var act = () => UserCoupon.Receive(Guid.NewGuid(), Guid.Empty, CouponId, "Manual", DateTime.UtcNow.AddDays(30));

        act.Should().Throw<PromotionDomainException>().WithMessage("*UserId*");
    }

    [Fact]
    public void Receive_Expired_ShouldThrowException()
    {
        var act = () => UserCoupon.Receive(Guid.NewGuid(), UserId, CouponId, "Manual", DateTime.UtcNow.AddHours(-1));

        act.Should().Throw<PromotionDomainException>().WithMessage("*过期*");
    }

    [Fact]
    public void Lock_Valid_ShouldTransitionToLocked()
    {
        var uc = CreateUserCoupon();
        var orderId = Guid.NewGuid();

        uc.Lock(orderId);

        uc.Status.Should().Be(CouponStatus.Locked);
        uc.LockedOrderId.Should().Be(orderId);
    }

    [Fact]
    public void Lock_NotUnused_ShouldThrowException()
    {
        var uc = CreateUserCoupon();
        uc.Lock(Guid.NewGuid());

        var act = () => uc.Lock(Guid.NewGuid());

        act.Should().Throw<PromotionDomainException>().WithMessage("*状态*");
    }

    [Fact]
    public void Consume_Valid_ShouldTransitionToUsed()
    {
        var uc = CreateUserCoupon();
        var orderId = Guid.NewGuid();
        uc.Lock(orderId);

        uc.Consume(orderId);

        uc.Status.Should().Be(CouponStatus.Used);
        uc.UsedOrderId.Should().Be(orderId);
    }

    [Fact]
    public void Consume_OrderMismatch_ShouldThrowException()
    {
        var uc = CreateUserCoupon();
        uc.Lock(Guid.NewGuid());

        var act = () => uc.Consume(Guid.NewGuid());

        act.Should().Throw<PromotionDomainException>().WithMessage("*不一致*");
    }

    [Fact]
    public void Release_Valid_ShouldReturnToUnused()
    {
        var uc = CreateUserCoupon();
        uc.Lock(Guid.NewGuid());

        uc.Release();

        uc.Status.Should().Be(CouponStatus.Unused);
        uc.LockedOrderId.Should().BeNull();
    }

    [Fact]
    public void Expire_Valid_ShouldSetExpired()
    {
        var uc = CreateUserCoupon();

        uc.Expire();

        uc.Status.Should().Be(CouponStatus.Expired);
    }

    [Fact]
    public void Expire_AlreadyUsed_ShouldThrowException()
    {
        var uc = CreateUserCoupon();
        var orderId = Guid.NewGuid();
        uc.Lock(orderId);
        uc.Consume(orderId);

        var act = () => uc.Expire();

        act.Should().Throw<PromotionDomainException>().WithMessage("*状态*");
    }

    [Fact]
    public void Expire_AlreadyExpired_ShouldThrowException()
    {
        var uc = CreateUserCoupon();
        uc.Expire();

        var act = () => uc.Expire();

        act.Should().Throw<PromotionDomainException>().WithMessage("*状态*");
    }

    [Fact]
    public void IsExpiredAt_BeforeExpiry_ShouldReturnFalse()
    {
        var uc = CreateUserCoupon();

        uc.IsExpiredAt(DateTime.UtcNow).Should().BeFalse();
    }

    [Fact]
    public void IsExpiredAt_AfterExpiry_ShouldReturnTrue()
    {
        var uc = UserCoupon.Receive(Guid.NewGuid(), UserId, CouponId, "Manual", DateTime.UtcNow.AddDays(30));

        uc.IsExpiredAt(DateTime.UtcNow.AddDays(31)).Should().BeTrue();
    }

    [Fact]
    public void Lock_ExpiredCoupon_ShouldThrowException()
    {
        // Receive rejects already-expired coupons, so we create one that expires very soon,
        // then wait for expiry before locking.
        var uc = UserCoupon.Receive(Guid.NewGuid(), UserId, CouponId, "Manual", DateTime.UtcNow.AddMilliseconds(100));
        System.Threading.Thread.Sleep(200);

        var act = () => uc.Lock(Guid.NewGuid());

        act.Should().Throw<PromotionDomainException>().WithMessage("*过期*");
    }

    [Fact]
    public void Release_WhenExpired_ShouldSetExpired()
    {
        var uc2 = UserCoupon.Receive(Guid.NewGuid(), UserId, CouponId, "Manual", DateTime.UtcNow.AddMilliseconds(100));
        uc2.Lock(Guid.NewGuid());
        // Wait for expiry
        System.Threading.Thread.Sleep(200);

        uc2.Release();

        uc2.Status.Should().Be(CouponStatus.Expired);
    }

    [Fact]
    public void Expire_FromLocked_ShouldSetExpired()
    {
        var uc = CreateUserCoupon();
        uc.Lock(Guid.NewGuid());

        uc.Expire();

        uc.Status.Should().Be(CouponStatus.Expired);
        uc.LockedOrderId.Should().BeNull();
    }

    private static UserCoupon CreateUserCoupon()
    {
        return UserCoupon.Receive(Guid.NewGuid(), UserId, CouponId, "Manual", DateTime.UtcNow.AddDays(30));
    }
}

public class PromotionActivityTests
{
    [Fact]
    public void Create_Valid_ShouldCreatePending()
    {
        var activity = PromotionActivity.Create(
            Guid.NewGuid(), "双11满减", PromotionType.FullReduction,
            DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2));

        activity.Status.Should().Be(PromotionStatus.Pending);
        activity.Name.Should().Be("双11满减");
    }

    [Fact]
    public void Create_EmptyName_ShouldThrowException()
    {
        var act = () => PromotionActivity.Create(
            Guid.NewGuid(), "", PromotionType.FullReduction,
            DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2));

        act.Should().Throw<PromotionDomainException>().WithMessage("*名称*");
    }

    [Fact]
    public void Activate_FromPending_ShouldTransitionToActive()
    {
        var activity = CreateActivity();

        activity.Activate();

        activity.Status.Should().Be(PromotionStatus.Active);
    }

    [Fact]
    public void Pause_Active_ShouldTransitionToPaused()
    {
        var activity = CreateActivity();
        activity.Activate();

        activity.Pause();

        activity.Status.Should().Be(PromotionStatus.Paused);
    }

    [Fact]
    public void Pause_NotActive_ShouldThrowException()
    {
        var activity = CreateActivity();

        var act = () => activity.Pause();

        act.Should().Throw<PromotionDomainException>().WithMessage("*状态*");
    }

    [Fact]
    public void Close_Valid_ShouldTransitionToClosed()
    {
        var activity = CreateActivity();

        activity.Close();

        activity.Status.Should().Be(PromotionStatus.Closed);
    }

    [Fact]
    public void AddRule_Valid_ShouldAddRule()
    {
        var activity = CreateActivity();

        activity.AddRule(100m, 10m);

        activity.Rules.Should().HaveCount(1);
        activity.Rules[0].ThresholdAmount.Should().Be(100m);
        activity.Rules[0].DiscountAmount.Should().Be(10m);
    }

    [Fact]
    public void AddRule_Duplicate_ShouldThrowException()
    {
        var activity = CreateActivity();
        activity.AddRule(100m, 10m);

        var act = () => activity.AddRule(100m, 20m);

        act.Should().Throw<PromotionDomainException>().WithMessage("*已存在*");
    }

    [Fact]
    public void RemoveRule_Existing_ShouldRemove()
    {
        var activity = CreateActivity();
        activity.AddRule(100m, 10m);

        activity.RemoveRule(100m);

        activity.Rules.Should().BeEmpty();
    }

    [Fact]
    public void RemoveRule_NotExisting_ShouldThrowException()
    {
        var activity = CreateActivity();

        var act = () => activity.RemoveRule(100m);

        act.Should().Throw<PromotionDomainException>().WithMessage("*不存在*");
    }

    [Fact]
    public void CalculateDiscount_ActiveInWindow_ShouldReturnDiscount()
    {
        var activity = CreateActivity();
        activity.Activate();
        activity.AddRule(100m, 10m);
        activity.AddRule(200m, 30m);

        var discount = activity.CalculateDiscount(150m);

        discount.Should().Be(10m);
    }

    [Fact]
    public void CalculateDiscount_HighestTier_ShouldReturnMaxDiscount()
    {
        var activity = CreateActivity();
        activity.Activate();
        activity.AddRule(100m, 10m);
        activity.AddRule(200m, 30m);

        var discount = activity.CalculateDiscount(300m);

        discount.Should().Be(30m);
    }

    [Fact]
    public void CalculateDiscount_NotActive_ShouldReturnZero()
    {
        var activity = CreateActivity();
        activity.AddRule(100m, 10m);

        var discount = activity.CalculateDiscount(150m);

        discount.Should().Be(0);
    }

    private static PromotionActivity CreateActivity()
    {
        return PromotionActivity.Create(
            Guid.NewGuid(), "双11满减", PromotionType.FullReduction,
            DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddHours(2));
    }
}
public class SeckillPreOccupationRecordTests
{
    [Fact]
    public void Create_Valid_ShouldCreateUnfulfilled()
    {
        var record = SeckillPreOccupationRecord.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 2);

        record.IsFulfilled.Should().BeFalse();
        record.IsRolledBack.Should().BeFalse();
        record.Quantity.Should().Be(2);
    }

    [Fact]
    public void MarkFulfilled_ShouldSetFulfilled()
    {
        var record = CreateRecord();

        record.MarkFulfilled();

        record.IsFulfilled.Should().BeTrue();
        record.FulfilledAt.Should().NotBeNull();
    }

    [Fact]
    public void MarkFulfilled_Twice_ShouldNotThrow()
    {
        var record = CreateRecord();
        record.MarkFulfilled();

        record.MarkFulfilled();

        record.IsFulfilled.Should().BeTrue();
    }

    [Fact]
    public void MarkRolledBack_ShouldSetRolledBack()
    {
        var record = CreateRecord();

        record.MarkRolledBack();

        record.IsRolledBack.Should().BeTrue();
        record.RolledBackAt.Should().NotBeNull();
    }

    [Fact]
    public void MarkRolledBack_Twice_ShouldNotThrow()
    {
        var record = CreateRecord();
        record.MarkRolledBack();

        record.MarkRolledBack();

        record.IsRolledBack.Should().BeTrue();
    }

    private static SeckillPreOccupationRecord CreateRecord()
        => SeckillPreOccupationRecord.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1);
}

public class UserCouponReturnTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid CouponId = Guid.NewGuid();

    [Fact]
    public void Return_Valid_ShouldRestoreToUnused()
    {
        var uc = CreateUsedCoupon();

        uc.Return();

        uc.Status.Should().Be(CouponStatus.Unused);
        uc.UsedOrderId.Should().BeNull();
        uc.UsedAt.Should().BeNull();
    }

    [Fact]
    public void Return_NotUsed_ShouldThrowException()
    {
        var uc = UserCoupon.Receive(Guid.NewGuid(), UserId, CouponId, "Manual", DateTime.UtcNow.AddDays(30));

        var act = () => uc.Return();

        act.Should().Throw<PromotionDomainException>().WithMessage("*状态*");
    }

    [Fact]
    public void Return_FromLocked_ShouldThrowException()
    {
        var uc = UserCoupon.Receive(Guid.NewGuid(), UserId, CouponId, "Manual", DateTime.UtcNow.AddDays(30));
        uc.Lock(Guid.NewGuid());

        var act = () => uc.Return();

        act.Should().Throw<PromotionDomainException>().WithMessage("*状态*");
    }

    [Fact]
    public void Return_FromExpired_ShouldThrowException()
    {
        var uc = UserCoupon.Receive(Guid.NewGuid(), UserId, CouponId, "Manual", DateTime.UtcNow.AddDays(30));
        uc.Expire();

        var act = () => uc.Return();

        act.Should().Throw<PromotionDomainException>().WithMessage("*状态*");
    }

    [Fact]
    public void Return_WhenExpired_ShouldSetExpired()
    {
        var uc = UserCoupon.Receive(Guid.NewGuid(), UserId, CouponId, "Manual", DateTime.UtcNow.AddMilliseconds(100));
        var orderId = Guid.NewGuid();
        uc.Lock(orderId);
        uc.Consume(orderId);
        System.Threading.Thread.Sleep(200);

        uc.Return();

        uc.Status.Should().Be(CouponStatus.Expired);
    }

    [Fact]
    public void Return_ShouldKeepOriginalValidityPeriod()
    {
        var originalExpiredAt = DateTime.UtcNow.AddDays(30);
        var uc = CreateUsedCoupon(originalExpiredAt);

        uc.Return();

        uc.ExpiredAt.Should().Be(originalExpiredAt);
    }

    private static UserCoupon CreateUsedCoupon(DateTime? expiredAt = null)
    {
        var uc = UserCoupon.Receive(Guid.NewGuid(), UserId, CouponId, "Manual", expiredAt ?? DateTime.UtcNow.AddDays(30));
        var orderId = Guid.NewGuid();
        uc.Lock(orderId);
        uc.Consume(orderId);
        return uc;
    }
}