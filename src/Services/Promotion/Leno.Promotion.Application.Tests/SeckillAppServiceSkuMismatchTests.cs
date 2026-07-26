using Leno.Promotion.Application.DTOs;
using Leno.Promotion.Application.Services;
using Leno.Promotion.Domain.Aggregates;
using Leno.Promotion.Domain.Exceptions;
using Leno.Promotion.Domain.Repositories;
using Leno.Promotion.Domain.Services;
using Leno.Promotion.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using Moq;
using SeckillActivity = Leno.Promotion.Domain.Aggregates.SeckillActivity;

namespace Leno.Promotion.Application.Tests;

/// <summary>
/// P0-2.8 修复测试：覆盖 SeckillAppService.PlaceOrderAsync 的单 SKU 契约校验。
/// 验证：
/// 1. 传入与活动 SkuId 一致的 SkuId 时正常下单；
/// 2. 传入与活动 SkuId 不一致的 SkuId 时抛 SECKILL_SKU_MISMATCH；
/// 3. 传入 Guid.Empty 时使用 activity.SkuId（向后兼容）。
/// </summary>
public class SeckillAppServiceSkuMismatchTests
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

    public SeckillAppServiceSkuMismatchTests()
    {
        _sut = new SeckillAppService(_repoMock.Object, _stockServiceMock.Object, _preOccupationRecordRepoMock.Object, _uowMock.Object);
    }

    /// <summary>
    /// 传入与活动 SkuId 一致的 SkuId 时应正常下单。
    /// </summary>
    [Fact]
    public async Task PlaceOrderAsync_MatchingSkuId_ShouldSucceed()
    {
        var activity = CreateActivity();
        activity.Activate();
        _repoMock.Setup(r => r.GetByIdAsync(ActivityId, It.IsAny<CancellationToken>())).ReturnsAsync(activity);
        _stockServiceMock.Setup(s => s.TryDeductAsync(ActivityId, SkuId, UserId, 1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _preOccupationRecordRepoMock.Setup(r => r.AddAsync(It.IsAny<SeckillPreOccupationRecord>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _uowMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await _sut.PlaceOrderAsync(ActivityId, UserId, new SeckillPlaceOrderDto { SkuId = SkuId, Quantity = 1 });

        result.Should().NotBeNull();
        result.OrderId.Should().NotBe(Guid.Empty);
        // 关键断言：Redis 扣减使用 activity.SkuId（与传入一致）
        _stockServiceMock.Verify(s => s.TryDeductAsync(ActivityId, SkuId, UserId, 1, 1, It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// 传入与活动 SkuId 不一致的 SkuId 时应抛 SECKILL_SKU_MISMATCH，且不应触发 Redis 扣减。
    /// </summary>
    [Fact]
    public async Task PlaceOrderAsync_MismatchedSkuId_ShouldThrowSkuMismatch()
    {
        var activity = CreateActivity();
        activity.Activate();
        var otherSkuId = Guid.NewGuid(); // 与 activity.SkuId 不同
        _repoMock.Setup(r => r.GetByIdAsync(ActivityId, It.IsAny<CancellationToken>())).ReturnsAsync(activity);

        var act = () => _sut.PlaceOrderAsync(ActivityId, UserId, new SeckillPlaceOrderDto { SkuId = otherSkuId, Quantity = 1 });

        var exception = await act.Should().ThrowAsync<PromotionDomainException>();
        exception.Which.ErrorCode.Should().Be("SECKILL_SKU_MISMATCH");
        // 关键断言：抛异常前不应触发 Redis 扣减，避免错误 SKU 库存被扣减
        _stockServiceMock.Verify(
            s => s.TryDeductAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// 传入 Guid.Empty 时应使用 activity.SkuId（向后兼容），下单成功。
    /// </summary>
    [Fact]
    public async Task PlaceOrderAsync_EmptySkuId_ShouldFallbackToActivitySkuId()
    {
        var activity = CreateActivity();
        activity.Activate();
        _repoMock.Setup(r => r.GetByIdAsync(ActivityId, It.IsAny<CancellationToken>())).ReturnsAsync(activity);
        _stockServiceMock.Setup(s => s.TryDeductAsync(ActivityId, SkuId, UserId, 1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _preOccupationRecordRepoMock.Setup(r => r.AddAsync(It.IsAny<SeckillPreOccupationRecord>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _uowMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

        // 不传 SkuId（默认 Guid.Empty）
        var result = await _sut.PlaceOrderAsync(ActivityId, UserId, new SeckillPlaceOrderDto { Quantity = 1 });

        result.Should().NotBeNull();
        result.OrderId.Should().NotBe(Guid.Empty);
        // 关键断言：Redis 扣减使用 activity.SkuId（向后兼容）
        _stockServiceMock.Verify(s => s.TryDeductAsync(ActivityId, SkuId, UserId, 1, 1, It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private static SeckillActivity CreateActivity()
    {
        return SeckillActivity.Create(
            ActivityId, "测试秒杀活动", SpuId, SkuId, 99m, 199m, 100, 1,
            DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddHours(2));
    }
}
