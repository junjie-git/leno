using Leno.Promotion.Application.DTOs;
using Leno.Promotion.Application.Services;
using Leno.Promotion.Domain.Aggregates;
using Leno.Promotion.Domain.Repositories;
using Leno.Promotion.Domain.Services;
using Leno.Promotion.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;
using Moq;
using SeckillActivity = Leno.Promotion.Domain.Aggregates.SeckillActivity;

namespace Leno.Promotion.Application.Tests;

/// <summary>
/// BC5 第三梯队 P1 能力补齐：查询筛选透传测试。
/// 验证 PromotionAppService / CouponAppService / SeckillAppService 的 QueryAsync 方法
/// 将 name / status / startTime / endTime / type / page / pageSize 筛选参数
/// 原样透传给对应仓储的 QueryAsync 与 CountAsync，并将仓储返回的列表与总数正确组装为分页结果 DTO。
/// </summary>
public class PromotionQueryFilterPassThroughTests
{
    // ========== PromotionAppService.QueryAsync 筛选透传 ==========

    /// <summary>
    /// 满减活动查询：所有筛选参数（name/status/startTime/endTime/page/pageSize）
    /// 应原样透传给 IPromotionActivityRepository.QueryAsync 与 CountAsync。
    /// </summary>
    [Fact]
    public async Task PromotionQueryAsync_AllFilters_ShouldPassThroughToRepository()
    {
        var repoMock = new Mock<IPromotionActivityRepository>();
        var uowMock = new Mock<IUnitOfWork>();
        var sut = new PromotionAppService(repoMock.Object, uowMock.Object);

        var startTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var endTime = new DateTime(2026, 12, 31, 23, 59, 59, DateTimeKind.Utc);
        var activities = new List<PromotionActivity>
        {
            PromotionActivity.Create(
                Guid.NewGuid(), "双11满减", PromotionType.FullReduction,
                startTime, endTime)
        };
        repoMock.Setup(r => r.QueryAsync(
                "双11", PromotionStatus.Active, startTime, endTime, 2, 10,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(activities);
        repoMock.Setup(r => r.CountAsync(
                "双11", PromotionStatus.Active, startTime, endTime,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        var result = await sut.QueryAsync("双11", PromotionStatus.Active, startTime, endTime, 2, 10);

        result.Should().NotBeNull();
        result.Items.Should().HaveCount(1);
        result.Items[0].Name.Should().Be("双11满减");
        result.Total.Should().Be(5);
        repoMock.Verify(r => r.QueryAsync(
            "双11", PromotionStatus.Active, startTime, endTime, 2, 10,
            It.IsAny<CancellationToken>()), Times.Once);
        repoMock.Verify(r => r.CountAsync(
            "双11", PromotionStatus.Active, startTime, endTime,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// 满减活动查询：null 筛选参数应原样透传（不过滤），page/pageSize 仍透传。
    /// </summary>
    [Fact]
    public async Task PromotionQueryAsync_NullFilters_ShouldPassThroughNulls()
    {
        var repoMock = new Mock<IPromotionActivityRepository>();
        var uowMock = new Mock<IUnitOfWork>();
        var sut = new PromotionAppService(repoMock.Object, uowMock.Object);

        var activities = new List<PromotionActivity>
        {
            PromotionActivity.Create(
                Guid.NewGuid(), "活动A", PromotionType.FullReduction,
                DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2)),
            PromotionActivity.Create(
                Guid.NewGuid(), "活动B", PromotionType.FullReduction,
                DateTime.UtcNow.AddDays(3), DateTime.UtcNow.AddDays(4))
        };
        repoMock.Setup(r => r.QueryAsync(
                null, null, null, null, 1, 20,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(activities);
        repoMock.Setup(r => r.CountAsync(
                null, null, null, null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        var result = await sut.QueryAsync(null, null, null, null, 1, 20);

        result.Items.Should().HaveCount(2);
        result.Total.Should().Be(2);
        repoMock.Verify(r => r.QueryAsync(
            null, null, null, null, 1, 20,
            It.IsAny<CancellationToken>()), Times.Once);
        repoMock.Verify(r => r.CountAsync(
            null, null, null, null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// 满减活动查询：仓储返回空列表时，应返回空 Items 与 CountAsync 的总数。
    /// </summary>
    [Fact]
    public async Task PromotionQueryAsync_EmptyResult_ShouldReturnEmptyItemsWithTotal()
    {
        var repoMock = new Mock<IPromotionActivityRepository>();
        var uowMock = new Mock<IUnitOfWork>();
        var sut = new PromotionAppService(repoMock.Object, uowMock.Object);

        repoMock.Setup(r => r.QueryAsync(
                It.IsAny<string?>(), It.IsAny<PromotionStatus?>(),
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>(),
                It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PromotionActivity>());
        repoMock.Setup(r => r.CountAsync(
                It.IsAny<string?>(), It.IsAny<PromotionStatus?>(),
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var result = await sut.QueryAsync("不存在的名称", null, null, null, 1, 10);

        result.Items.Should().BeEmpty();
        result.Total.Should().Be(0);
    }

    /// <summary>
    /// 满减活动查询：仅 status 筛选时，name/startTime/endTime 透传 null，CountAsync 不传 page/pageSize。
    /// </summary>
    [Fact]
    public async Task PromotionQueryAsync_StatusOnly_ShouldPassThroughStatusAndNullOthers()
    {
        var repoMock = new Mock<IPromotionActivityRepository>();
        var uowMock = new Mock<IUnitOfWork>();
        var sut = new PromotionAppService(repoMock.Object, uowMock.Object);

        repoMock.Setup(r => r.QueryAsync(
                null, PromotionStatus.Closed, null, null, 1, 50,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PromotionActivity>());
        repoMock.Setup(r => r.CountAsync(
                null, PromotionStatus.Closed, null, null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(10);

        var result = await sut.QueryAsync(null, PromotionStatus.Closed, null, null, 1, 50);

        result.Total.Should().Be(10);
        repoMock.Verify(r => r.QueryAsync(
            null, PromotionStatus.Closed, null, null, 1, 50,
            It.IsAny<CancellationToken>()), Times.Once);
        repoMock.Verify(r => r.CountAsync(
            null, PromotionStatus.Closed, null, null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ========== CouponAppService.QueryAsync 筛选透传 ==========

    /// <summary>
    /// 优惠券查询：所有筛选参数（name/type/status/page/pageSize）
    /// 应原样透传给 ICouponRepository.QueryAsync 与 CountAsync。
    /// </summary>
    [Fact]
    public async Task CouponQueryAsync_AllFilters_ShouldPassThroughToRepository()
    {
        var couponRepoMock = new Mock<ICouponRepository>();
        var userCouponRepoMock = new Mock<IUserCouponRepository>();
        var uowMock = new Mock<IUnitOfWork>();
        var sut = new CouponAppService(couponRepoMock.Object, userCouponRepoMock.Object, uowMock.Object);

        var coupons = new List<Coupon>
        {
            Coupon.Create(
                Guid.NewGuid(), "满100减20", CouponType.FixedAmount, 20m, 100m,
                CouponValidityType.FixedPeriod,
                DateTime.UtcNow, DateTime.UtcNow.AddDays(30), null, 1000)
        };
        couponRepoMock.Setup(r => r.QueryAsync(
                "满100", CouponType.FixedAmount, CouponTemplateStatus.Enabled, 3, 15,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(coupons);
        couponRepoMock.Setup(r => r.CountAsync(
                "满100", CouponType.FixedAmount, CouponTemplateStatus.Enabled,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(8);

        var result = await sut.QueryAsync("满100", CouponType.FixedAmount, CouponTemplateStatus.Enabled, 3, 15);

        result.Should().NotBeNull();
        result.Items.Should().HaveCount(1);
        result.Items[0].Name.Should().Be("满100减20");
        result.Total.Should().Be(8);
        couponRepoMock.Verify(r => r.QueryAsync(
            "满100", CouponType.FixedAmount, CouponTemplateStatus.Enabled, 3, 15,
            It.IsAny<CancellationToken>()), Times.Once);
        couponRepoMock.Verify(r => r.CountAsync(
            "满100", CouponType.FixedAmount, CouponTemplateStatus.Enabled,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// 优惠券查询：null 筛选参数应原样透传（不过滤），page/pageSize 仍透传。
    /// </summary>
    [Fact]
    public async Task CouponQueryAsync_NullFilters_ShouldPassThroughNulls()
    {
        var couponRepoMock = new Mock<ICouponRepository>();
        var userCouponRepoMock = new Mock<IUserCouponRepository>();
        var uowMock = new Mock<IUnitOfWork>();
        var sut = new CouponAppService(couponRepoMock.Object, userCouponRepoMock.Object, uowMock.Object);

        var coupons = new List<Coupon>
        {
            Coupon.Create(
                Guid.NewGuid(), "券A", CouponType.FixedAmount, 10m, 50m,
                CouponValidityType.FixedPeriod,
                DateTime.UtcNow, DateTime.UtcNow.AddDays(30), null, 500),
            Coupon.Create(
                Guid.NewGuid(), "券B", CouponType.Percentage, 5m, 0m,
                CouponValidityType.RelativeDays, null, null, 7, 200)
        };
        couponRepoMock.Setup(r => r.QueryAsync(
                null, null, null, 1, 20,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(coupons);
        couponRepoMock.Setup(r => r.CountAsync(
                null, null, null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        var result = await sut.QueryAsync(null, null, null, 1, 20);

        result.Items.Should().HaveCount(2);
        result.Total.Should().Be(2);
        couponRepoMock.Verify(r => r.QueryAsync(
            null, null, null, 1, 20,
            It.IsAny<CancellationToken>()), Times.Once);
        couponRepoMock.Verify(r => r.CountAsync(
            null, null, null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// 优惠券查询：仓储返回空列表时，应返回空 Items 与 CountAsync 的总数。
    /// </summary>
    [Fact]
    public async Task CouponQueryAsync_EmptyResult_ShouldReturnEmptyItemsWithTotal()
    {
        var couponRepoMock = new Mock<ICouponRepository>();
        var userCouponRepoMock = new Mock<IUserCouponRepository>();
        var uowMock = new Mock<IUnitOfWork>();
        var sut = new CouponAppService(couponRepoMock.Object, userCouponRepoMock.Object, uowMock.Object);

        couponRepoMock.Setup(r => r.QueryAsync(
                It.IsAny<string?>(), It.IsAny<CouponType?>(),
                It.IsAny<CouponTemplateStatus?>(),
                It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Coupon>());
        couponRepoMock.Setup(r => r.CountAsync(
                It.IsAny<string?>(), It.IsAny<CouponType?>(),
                It.IsAny<CouponTemplateStatus?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var result = await sut.QueryAsync("不存在的券", null, null, 1, 10);

        result.Items.Should().BeEmpty();
        result.Total.Should().Be(0);
    }

    /// <summary>
    /// 优惠券查询：仅 type 筛选时，name/status 透传 null，CountAsync 不传 page/pageSize。
    /// </summary>
    [Fact]
    public async Task CouponQueryAsync_TypeOnly_ShouldPassThroughTypeAndNullOthers()
    {
        var couponRepoMock = new Mock<ICouponRepository>();
        var userCouponRepoMock = new Mock<IUserCouponRepository>();
        var uowMock = new Mock<IUnitOfWork>();
        var sut = new CouponAppService(couponRepoMock.Object, userCouponRepoMock.Object, uowMock.Object);

        couponRepoMock.Setup(r => r.QueryAsync(
                null, CouponType.Percentage, null, 1, 30,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Coupon>());
        couponRepoMock.Setup(r => r.CountAsync(
                null, CouponType.Percentage, null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(15);

        var result = await sut.QueryAsync(null, CouponType.Percentage, null, 1, 30);

        result.Total.Should().Be(15);
        couponRepoMock.Verify(r => r.QueryAsync(
            null, CouponType.Percentage, null, 1, 30,
            It.IsAny<CancellationToken>()), Times.Once);
        couponRepoMock.Verify(r => r.CountAsync(
            null, CouponType.Percentage, null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ========== SeckillAppService.QueryAsync 筛选透传 ==========

    /// <summary>
    /// 秒杀活动查询：所有筛选参数（name/status/page/pageSize）
    /// 应原样透传给 ISeckillActivityRepository.QueryAsync 与 CountAsync。
    /// Pending 态活动不触发 Redis 读取，避免测试依赖 Redis Mock。
    /// </summary>
    [Fact]
    public async Task SeckillQueryAsync_AllFilters_ShouldPassThroughToRepository()
    {
        var repoMock = new Mock<ISeckillActivityRepository>();
        var stockServiceMock = new Mock<ISeckillStockService>();
        var preOccupationRepoMock = new Mock<ISeckillPreOccupationRecordRepository>();
        var uowMock = new Mock<IUnitOfWork>();
        var sut = new SeckillAppService(
            repoMock.Object, stockServiceMock.Object, preOccupationRepoMock.Object, uowMock.Object);

        var activities = new List<SeckillActivity>
        {
            SeckillActivity.Create(
                Guid.NewGuid(), "双11秒杀专场", Guid.NewGuid(), Guid.NewGuid(),
                99m, 199m, 100, 1,
                DateTime.UtcNow.AddHours(1), DateTime.UtcNow.AddHours(2))
        };
        repoMock.Setup(r => r.QueryAsync(
                "双11", SeckillStatus.Pending, 2, 10,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(activities);
        repoMock.Setup(r => r.CountAsync(
                "双11", SeckillStatus.Pending,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        var result = await sut.QueryAsync("双11", SeckillStatus.Pending, 2, 10);

        result.Should().NotBeNull();
        result.Items.Should().HaveCount(1);
        result.Items[0].Name.Should().Be("双11秒杀专场");
        result.Total.Should().Be(3);
        // Pending 态活动不调用 Redis 库存读取
        stockServiceMock.Verify(
            s => s.GetAvailableAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
        repoMock.Verify(r => r.QueryAsync(
            "双11", SeckillStatus.Pending, 2, 10,
            It.IsAny<CancellationToken>()), Times.Once);
        repoMock.Verify(r => r.CountAsync(
            "双11", SeckillStatus.Pending,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// 秒杀活动查询：null 筛选参数应原样透传（不过滤），page/pageSize 仍透传。
    /// </summary>
    [Fact]
    public async Task SeckillQueryAsync_NullFilters_ShouldPassThroughNulls()
    {
        var repoMock = new Mock<ISeckillActivityRepository>();
        var stockServiceMock = new Mock<ISeckillStockService>();
        var preOccupationRepoMock = new Mock<ISeckillPreOccupationRecordRepository>();
        var uowMock = new Mock<IUnitOfWork>();
        var sut = new SeckillAppService(
            repoMock.Object, stockServiceMock.Object, preOccupationRepoMock.Object, uowMock.Object);

        var activities = new List<SeckillActivity>
        {
            SeckillActivity.Create(
                Guid.NewGuid(), "秒杀A", Guid.NewGuid(), Guid.NewGuid(),
                99m, 199m, 100, 1,
                DateTime.UtcNow.AddHours(1), DateTime.UtcNow.AddHours(2)),
            SeckillActivity.Create(
                Guid.NewGuid(), "秒杀B", Guid.NewGuid(), Guid.NewGuid(),
                49m, 99m, 50, 1,
                DateTime.UtcNow.AddHours(3), DateTime.UtcNow.AddHours(4))
        };
        repoMock.Setup(r => r.QueryAsync(
                null, null, 1, 20,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(activities);
        repoMock.Setup(r => r.CountAsync(
                null, null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        var result = await sut.QueryAsync(null, null, 1, 20);

        result.Items.Should().HaveCount(2);
        result.Total.Should().Be(2);
        repoMock.Verify(r => r.QueryAsync(
            null, null, 1, 20,
            It.IsAny<CancellationToken>()), Times.Once);
        repoMock.Verify(r => r.CountAsync(
            null, null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// 秒杀活动查询：仓储返回空列表时，应返回空 Items 与 CountAsync 的总数，
    /// 且不调用 ToDtoAsync（不触发 Redis 库存读取）。
    /// </summary>
    [Fact]
    public async Task SeckillQueryAsync_EmptyResult_ShouldReturnEmptyItemsAndNotCallRedis()
    {
        var repoMock = new Mock<ISeckillActivityRepository>();
        var stockServiceMock = new Mock<ISeckillStockService>();
        var preOccupationRepoMock = new Mock<ISeckillPreOccupationRecordRepository>();
        var uowMock = new Mock<IUnitOfWork>();
        var sut = new SeckillAppService(
            repoMock.Object, stockServiceMock.Object, preOccupationRepoMock.Object, uowMock.Object);

        repoMock.Setup(r => r.QueryAsync(
                It.IsAny<string?>(), It.IsAny<SeckillStatus?>(),
                It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SeckillActivity>());
        repoMock.Setup(r => r.CountAsync(
                It.IsAny<string?>(), It.IsAny<SeckillStatus?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var result = await sut.QueryAsync("不存在的秒杀", null, 1, 10);

        result.Items.Should().BeEmpty();
        result.Total.Should().Be(0);
        // 空列表时不应调用 Redis 库存读取
        stockServiceMock.Verify(
            s => s.GetAvailableAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// 秒杀活动查询：仅 status 筛选时，name 透传 null，CountAsync 不传 page/pageSize。
    /// </summary>
    [Fact]
    public async Task SeckillQueryAsync_StatusOnly_ShouldPassThroughStatusAndNullName()
    {
        var repoMock = new Mock<ISeckillActivityRepository>();
        var stockServiceMock = new Mock<ISeckillStockService>();
        var preOccupationRepoMock = new Mock<ISeckillPreOccupationRecordRepository>();
        var uowMock = new Mock<IUnitOfWork>();
        var sut = new SeckillAppService(
            repoMock.Object, stockServiceMock.Object, preOccupationRepoMock.Object, uowMock.Object);

        repoMock.Setup(r => r.QueryAsync(
                null, SeckillStatus.Closed, 1, 50,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SeckillActivity>());
        repoMock.Setup(r => r.CountAsync(
                null, SeckillStatus.Closed,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(20);

        var result = await sut.QueryAsync(null, SeckillStatus.Closed, 1, 50);

        result.Total.Should().Be(20);
        repoMock.Verify(r => r.QueryAsync(
            null, SeckillStatus.Closed, 1, 50,
            It.IsAny<CancellationToken>()), Times.Once);
        repoMock.Verify(r => r.CountAsync(
            null, SeckillStatus.Closed,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// 秒杀活动查询：Active 态活动应触发 Redis 实时库存读取（验证 ToDtoAsync 行为）。
    /// </summary>
    [Fact]
    public async Task SeckillQueryAsync_ActiveActivities_ShouldReadRedisRealtimeStock()
    {
        var repoMock = new Mock<ISeckillActivityRepository>();
        var stockServiceMock = new Mock<ISeckillStockService>();
        var preOccupationRepoMock = new Mock<ISeckillPreOccupationRecordRepository>();
        var uowMock = new Mock<IUnitOfWork>();
        var sut = new SeckillAppService(
            repoMock.Object, stockServiceMock.Object, preOccupationRepoMock.Object, uowMock.Object);

        var activity = SeckillActivity.Create(
            Guid.NewGuid(), "进行中秒杀", Guid.NewGuid(), Guid.NewGuid(),
            99m, 199m, 100, 1,
            DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddHours(2));
        activity.Activate();
        var activities = new List<SeckillActivity> { activity };

        repoMock.Setup(r => r.QueryAsync(
                "进行中", SeckillStatus.Active, 1, 10,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(activities);
        repoMock.Setup(r => r.CountAsync(
                "进行中", SeckillStatus.Active,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        stockServiceMock.Setup(s => s.GetAvailableAsync(
                activity.Id, activity.SkuId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(75);

        var result = await sut.QueryAsync("进行中", SeckillStatus.Active, 1, 10);

        result.Items.Should().HaveCount(1);
        result.Items[0].AvailableStockRealtime.Should().Be(75);
        result.Total.Should().Be(1);
        stockServiceMock.Verify(
            s => s.GetAvailableAsync(activity.Id, activity.SkuId, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
