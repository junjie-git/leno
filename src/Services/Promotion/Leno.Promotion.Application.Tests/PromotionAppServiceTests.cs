using Leno.Promotion.Application;
using Leno.Promotion.Application.DTOs;
using Leno.Promotion.Application.Services;
using Leno.Promotion.Domain.Aggregates;
using Leno.Promotion.Domain.Exceptions;
using Leno.Promotion.Domain.Repositories;
using Leno.Promotion.Domain.Services;
using Leno.Promotion.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;
using Moq;
using SeckillActivity = Leno.Promotion.Domain.Aggregates.SeckillActivity;

namespace Leno.Promotion.Application.Tests;

public class PromotionAppServiceTests
{
    private readonly Mock<IPromotionActivityRepository> _repoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly PromotionAppService _sut;

    private static readonly Guid ActivityId = Guid.NewGuid();

    public PromotionAppServiceTests()
    {
        _sut = new PromotionAppService(_repoMock.Object, _uowMock.Object);
    }

    [Fact]
    public async Task GetByIdAsync_Existing_ShouldReturnDto()
    {
        var activity = CreateActivity();
        _repoMock.Setup(r => r.GetByIdAsync(ActivityId, It.IsAny<CancellationToken>())).ReturnsAsync(activity);

        var result = await _sut.GetByIdAsync(ActivityId);

        result.Should().NotBeNull();
        result.Name.Should().Be("双11满减");
    }

    [Fact]
    public async Task GetByIdAsync_NotFound_ShouldThrowException()
    {
        _repoMock.Setup(r => r.GetByIdAsync(ActivityId, It.IsAny<CancellationToken>())).ReturnsAsync((PromotionActivity?)null);

        var act = () => _sut.GetByIdAsync(ActivityId);

        await act.Should().ThrowAsync<PromotionDomainException>().WithMessage("*不存在*");
    }

    [Fact]
    public async Task ActivateAsync_Valid_ShouldActivate()
    {
        var activity = CreateActivity();
        _repoMock.Setup(r => r.GetByIdAsync(ActivityId, It.IsAny<CancellationToken>())).ReturnsAsync(activity);

        await _sut.ActivateAsync(ActivityId);

        activity.Status.Should().Be(PromotionStatus.Active);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CloseAsync_Valid_ShouldClose()
    {
        var activity = CreateActivity();
        _repoMock.Setup(r => r.GetByIdAsync(ActivityId, It.IsAny<CancellationToken>())).ReturnsAsync(activity);

        await _sut.CloseAsync(ActivityId);

        activity.Status.Should().Be(PromotionStatus.Closed);
    }

    private static PromotionActivity CreateActivity()
    {
        return PromotionActivity.Create(
            ActivityId, "双11满减", PromotionType.FullReduction,
            DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddHours(2));
    }
}

public partial class CouponAppServiceTests
{
    private readonly Mock<ICouponRepository> _couponRepoMock = new();
    private readonly Mock<IUserCouponRepository> _userCouponRepoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly CouponAppService _sut;

    private static readonly Guid CouponId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid OrderId = Guid.NewGuid();

    public CouponAppServiceTests()
    {
        _sut = new CouponAppService(_couponRepoMock.Object, _userCouponRepoMock.Object, _uowMock.Object);
    }

    [Fact]
    public async Task EnableAsync_Valid_ShouldEnable()
    {
        var coupon = CreateCoupon();
        coupon.Disable();
        _couponRepoMock.Setup(r => r.GetByIdAsync(CouponId, It.IsAny<CancellationToken>())).ReturnsAsync(coupon);

        await _sut.EnableAsync(CouponId);

        coupon.Status.Should().Be(CouponTemplateStatus.Enabled);
    }

    [Fact]
    public async Task DisableAsync_Valid_ShouldDisable()
    {
        var coupon = CreateCoupon();
        _couponRepoMock.Setup(r => r.GetByIdAsync(CouponId, It.IsAny<CancellationToken>())).ReturnsAsync(coupon);

        await _sut.DisableAsync(CouponId);

        coupon.Status.Should().Be(CouponTemplateStatus.Disabled);
    }

    [Fact]
    public async Task IssueAsync_Valid_ShouldIssue()
    {
        var coupon = CreateCoupon();
        _couponRepoMock.Setup(r => r.GetByIdAsync(CouponId, It.IsAny<CancellationToken>())).ReturnsAsync(coupon);

        await _sut.IssueAsync(CouponId, 10);

        coupon.IssuedQty.Should().Be(10);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReceiveAsync_Valid_ShouldCreateUserCoupon()
    {
        var coupon = CreateCoupon();
        _couponRepoMock.Setup(r => r.GetByIdAsync(CouponId, It.IsAny<CancellationToken>())).ReturnsAsync(coupon);
        _userCouponRepoMock.Setup(r => r.ExistsAsync(UserId, CouponId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _sut.ReceiveAsync(UserId, CouponId, "Manual");

        result.Should().NotBeNull();
        result.UserId.Should().Be(UserId);
        _userCouponRepoMock.Verify(r => r.AddAsync(It.IsAny<UserCoupon>(), It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReceiveAsync_ConcurrentDuplicate_ShouldThrowAlreadyReceived()
    {
        // 并发领取：ExistsAsync 检查通过（并发窗口），但 SaveEntitiesAsync 因 (UserId, CouponId) 唯一索引冲突抛 DbUpdateException
        var coupon = CreateCoupon();
        _couponRepoMock.Setup(r => r.GetByIdAsync(CouponId, It.IsAny<CancellationToken>())).ReturnsAsync(coupon);
        _userCouponRepoMock.Setup(r => r.ExistsAsync(UserId, CouponId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _uowMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("unique constraint violation"));

        var act = () => _sut.ReceiveAsync(UserId, CouponId, "Manual");

        await act.Should().ThrowAsync<PromotionDomainException>()
            .WithMessage("*已领取*");
    }

    [Fact]
    public async Task LockCouponAsync_Valid_ShouldLockAndSave()
    {
        var userCoupon = CreateUserCoupon();
        _userCouponRepoMock.Setup(r => r.GetByUserIdAndCouponIdAsync(UserId, CouponId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userCoupon);

        await _sut.LockCouponAsync(UserId, CouponId, OrderId);

        userCoupon.Status.Should().Be(CouponStatus.Locked);
        userCoupon.LockedOrderId.Should().Be(OrderId);
        _userCouponRepoMock.Verify(r => r.UpdateAsync(userCoupon, It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LockCouponAsync_NotFound_ShouldThrowException()
    {
        _userCouponRepoMock.Setup(r => r.GetByUserIdAndCouponIdAsync(UserId, CouponId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserCoupon?)null);

        var act = () => _sut.LockCouponAsync(UserId, CouponId, OrderId);

        await act.Should().ThrowAsync<PromotionDomainException>().WithMessage("*未持有*");
    }

    [Fact]
    public async Task LockCouponAsync_AlreadyLocked_ShouldThrowExceptionAndNotSave()
    {
        // 并发互斥：券已被另一订单锁定，第二个 LockCouponAsync 被聚合根拒绝
        var userCoupon = CreateUserCoupon();
        userCoupon.Lock(Guid.NewGuid());
        _userCouponRepoMock.Setup(r => r.GetByUserIdAndCouponIdAsync(UserId, CouponId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userCoupon);

        var act = () => _sut.LockCouponAsync(UserId, CouponId, OrderId);

        await act.Should().ThrowAsync<PromotionDomainException>().WithMessage("*锁定*");
        _userCouponRepoMock.Verify(r => r.UpdateAsync(It.IsAny<UserCoupon>(), It.IsAny<CancellationToken>()), Times.Never);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private static UserCoupon CreateUserCoupon()
        => UserCoupon.Receive(Guid.NewGuid(), UserId, CouponId, "Manual", DateTime.UtcNow.AddDays(10));

    private static Coupon CreateCoupon()
    {
        return Coupon.Create(
            CouponId, "满100减20", CouponType.FixedAmount, 20m, 100m,
            CouponValidityType.FixedPeriod,
            DateTime.UtcNow, DateTime.UtcNow.AddDays(30), null, 1000);
    }
}

public class SeckillAppServiceTests
{
    private readonly Mock<ISeckillActivityRepository> _repoMock = new();
    private readonly Mock<ISeckillStockService> _stockServiceMock = new();
    private readonly Mock<ISeckillPreOccupationRecordRepository> _preOccupationRecordRepoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly SeckillAppService _sut;

    private static readonly Guid ActivityId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid SpuId = Guid.NewGuid();
    private static readonly Guid SkuId = Guid.NewGuid();

    public SeckillAppServiceTests()
    {
        _sut = new SeckillAppService(_repoMock.Object, _stockServiceMock.Object, _preOccupationRecordRepoMock.Object, _uowMock.Object);
    }

    [Fact]
    public async Task CreateAsync_Valid_ShouldReturnDto()
    {
        _repoMock.Setup(r => r.AddAsync(It.IsAny<SeckillActivity>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var dto = new CreateSeckillActivityDto
        {
            Name = "测试秒杀活动",
            SpuId = SpuId, SkuId = SkuId, SeckillPrice = 99m, OriginalPrice = 199m,
            TotalStock = 100, LimitPerUser = 1,
            StartTime = DateTime.UtcNow.AddHours(1), EndTime = DateTime.UtcNow.AddHours(2)
        };

        var result = await _sut.CreateAsync(dto);

        result.Should().NotBeNull();
        result.Name.Should().Be("测试秒杀活动");
        result.SpuId.Should().Be(SpuId);
        result.Status.Should().Be(SeckillStatus.Pending);
        _repoMock.Verify(r => r.AddAsync(It.IsAny<SeckillActivity>(), It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ActivateAsync_Valid_ShouldActivateAndInitializeStock()
    {
        var activity = CreateActivity();
        _repoMock.Setup(r => r.GetByIdAsync(ActivityId, It.IsAny<CancellationToken>())).ReturnsAsync(activity);

        await _sut.ActivateAsync(ActivityId);

        activity.Status.Should().Be(SeckillStatus.Active);
        _stockServiceMock.Verify(s => s.InitializeAsync(ActivityId, It.IsAny<Dictionary<Guid, int>>(), It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ActivateAsync_NotFound_ShouldThrowException()
    {
        _repoMock.Setup(r => r.GetByIdAsync(ActivityId, It.IsAny<CancellationToken>())).ReturnsAsync((SeckillActivity?)null);

        var act = () => _sut.ActivateAsync(ActivityId);

        await act.Should().ThrowAsync<PromotionDomainException>().WithMessage("*不存在*");
    }

    [Fact]
    public async Task CloseAsync_Valid_ShouldClose()
    {
        var activity = CreateActivity();
        _repoMock.Setup(r => r.GetByIdAsync(ActivityId, It.IsAny<CancellationToken>())).ReturnsAsync(activity);

        await _sut.CloseAsync(ActivityId);

        activity.Status.Should().Be(SeckillStatus.Closed);
    }

    [Fact]
    public async Task CloseActivityWithStockWriteBackAsync_Valid_ShouldCloseAndWriteBack()
    {
        var activity = CreateActivity();
        _repoMock.Setup(r => r.GetByIdAsync(ActivityId, It.IsAny<CancellationToken>())).ReturnsAsync(activity);

        await _sut.CloseActivityWithStockWriteBackAsync(ActivityId);

        activity.Status.Should().Be(SeckillStatus.Closed);
        _stockServiceMock.Verify(s => s.WriteBackToDbAsync(ActivityId, It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PlaceOrderAsync_Valid_ShouldReturnResult()
    {
        var activity = CreateActivity();
        activity.Activate();
        _repoMock.Setup(r => r.GetByIdAsync(ActivityId, It.IsAny<CancellationToken>())).ReturnsAsync(activity);
        _stockServiceMock.Setup(s => s.TryDeductAsync(ActivityId, SkuId, UserId, 2, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var result = await _sut.PlaceOrderAsync(ActivityId, UserId, new SeckillPlaceOrderDto { SkuId = SkuId, Quantity = 2 });

        result.Should().NotBeNull();
        result.ActivityId.Should().Be(ActivityId);
        result.UserId.Should().Be(UserId);
        result.Quantity.Should().Be(2);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PlaceOrderAsync_WithExplicitSkuId_ShouldUseActivitySkuId()
    {
        // 单 SKU 契约：调用方传入与活动一致的 SkuId 时正常下单，使用 activity.SkuId
        var activity = CreateActivity();
        activity.Activate();
        _repoMock.Setup(r => r.GetByIdAsync(ActivityId, It.IsAny<CancellationToken>())).ReturnsAsync(activity);
        _stockServiceMock.Setup(s => s.TryDeductAsync(ActivityId, SkuId, UserId, 1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var result = await _sut.PlaceOrderAsync(ActivityId, UserId, new SeckillPlaceOrderDto { SkuId = SkuId, Quantity = 1 });

        result.Should().NotBeNull();
        _stockServiceMock.Verify(s => s.TryDeductAsync(ActivityId, SkuId, UserId, 1, 1, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PlaceOrderAsync_WithoutSkuId_ShouldFallbackToActivitySkuId()
    {
        var activity = CreateActivity();
        activity.Activate();
        _repoMock.Setup(r => r.GetByIdAsync(ActivityId, It.IsAny<CancellationToken>())).ReturnsAsync(activity);
        _stockServiceMock.Setup(s => s.TryDeductAsync(ActivityId, SkuId, UserId, 1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var result = await _sut.PlaceOrderAsync(ActivityId, UserId, new SeckillPlaceOrderDto { Quantity = 1 });

        result.Should().NotBeNull();
        _stockServiceMock.Verify(s => s.TryDeductAsync(ActivityId, SkuId, UserId, 1, 1, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PlaceOrderAsync_InsufficientStock_ShouldThrowException()
    {
        var activity = CreateActivity();
        activity.Activate();
        _repoMock.Setup(r => r.GetByIdAsync(ActivityId, It.IsAny<CancellationToken>())).ReturnsAsync(activity);
        _stockServiceMock.Setup(s => s.TryDeductAsync(ActivityId, SkuId, UserId, 2, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var act = () => _sut.PlaceOrderAsync(ActivityId, UserId, new SeckillPlaceOrderDto { SkuId = SkuId, Quantity = 2 });

        await act.Should().ThrowAsync<PromotionDomainException>().WithMessage("*库存不足*");
    }

    [Fact]
    public async Task PlaceOrderAsync_ExceedsLimit_ShouldThrowException()
    {
        var activity = CreateActivity();
        activity.Activate();
        _repoMock.Setup(r => r.GetByIdAsync(ActivityId, It.IsAny<CancellationToken>())).ReturnsAsync(activity);
        _stockServiceMock.Setup(s => s.TryDeductAsync(ActivityId, SkuId, UserId, 2, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        var act = () => _sut.PlaceOrderAsync(ActivityId, UserId, new SeckillPlaceOrderDto { SkuId = SkuId, Quantity = 2 });

        await act.Should().ThrowAsync<PromotionDomainException>().WithMessage("*超出限购*");
    }

    [Fact]
    public async Task PlaceOrderAsync_DbFailure_ShouldRestoreRedisStock()
    {
        var activity = CreateActivity();
        activity.Activate();
        _repoMock.Setup(r => r.GetByIdAsync(ActivityId, It.IsAny<CancellationToken>())).ReturnsAsync(activity);
        _stockServiceMock.Setup(s => s.TryDeductAsync(ActivityId, SkuId, UserId, 2, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _uowMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB error"));

        var act = () => _sut.PlaceOrderAsync(ActivityId, UserId, new SeckillPlaceOrderDto { SkuId = SkuId, Quantity = 2 });

        await act.Should().ThrowAsync<InvalidOperationException>();
        _stockServiceMock.Verify(s => s.RestoreAsync(ActivityId, SkuId, 2, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PlaceOrderAsync_EmptyUserId_ShouldThrowException()
    {
        var act = () => _sut.PlaceOrderAsync(ActivityId, Guid.Empty, new SeckillPlaceOrderDto { SkuId = SkuId, Quantity = 2 });

        await act.Should().ThrowAsync<PromotionDomainException>().WithMessage("*UserId*");
    }

    [Fact]
    public async Task PlaceOrderAsync_NullDto_ShouldThrowException()
    {
        var act = () => _sut.PlaceOrderAsync(ActivityId, UserId, null!, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task PlaceOrderAsync_ZeroQuantity_ShouldThrowException()
    {
        var act = () => _sut.PlaceOrderAsync(ActivityId, UserId, new SeckillPlaceOrderDto { SkuId = SkuId, Quantity = 0 });

        await act.Should().ThrowAsync<PromotionDomainException>().WithMessage("*数量*");
    }

    [Fact]
    public async Task GetByIdAsync_Existing_ShouldReturnDto()
    {
        var activity = CreateActivity();
        _repoMock.Setup(r => r.GetByIdAsync(ActivityId, It.IsAny<CancellationToken>())).ReturnsAsync(activity);

        var result = await _sut.GetByIdAsync(ActivityId);

        result.Should().NotBeNull();
        result.Id.Should().Be(ActivityId);
    }

    [Fact]
    public async Task GetByIdAsync_NotFound_ShouldThrowException()
    {
        _repoMock.Setup(r => r.GetByIdAsync(ActivityId, It.IsAny<CancellationToken>())).ReturnsAsync((SeckillActivity?)null);

        var act = () => _sut.GetByIdAsync(ActivityId);

        await act.Should().ThrowAsync<PromotionDomainException>().WithMessage("*不存在*");
    }

    [Fact]
    public async Task GetActiveAsync_ShouldReturnList()
    {
        var activities = new List<SeckillActivity> { CreateActivity() };
        _repoMock.Setup(r => r.GetActiveAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(activities);

        var result = await _sut.GetActiveAsync();

        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task QueryAsync_ShouldReturnList()
    {
        var activities = new List<SeckillActivity> { CreateActivity() };
        _repoMock.Setup(r => r.QueryAsync(null, null, 1, 20, It.IsAny<CancellationToken>())).ReturnsAsync(activities);
        _repoMock.Setup(r => r.CountAsync(null, null, It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await _sut.QueryAsync(null, null, 1, 20);

        result.Items.Should().HaveCount(1);
        result.Total.Should().Be(1);
    }

    private static SeckillActivity CreateActivity()
    {
        return SeckillActivity.Create(
            ActivityId, "测试秒杀活动", SpuId, SkuId, 99m, 199m, 100, 1,
            DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddHours(2));
    }
}