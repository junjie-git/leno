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
/// P0-2.9 补充测试：覆盖 SeckillAppService.PlaceOrderAsync 在 DB 乐观锁冲突与预占记录写入失败场景下的行为。
/// 验证 DB 基线扣减已从热路径剥离，仅创建预占记录 + 发事件，由后台对账同步基线。
/// </summary>
public class SeckillAppServicePlaceOrderAdditionalTests
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

    public SeckillAppServicePlaceOrderAdditionalTests()
    {
        _sut = new SeckillAppService(_repoMock.Object, _stockServiceMock.Object, _preOccupationRecordRepoMock.Object, _uowMock.Object);
    }

    [Fact]
    public async Task PlaceOrderAsync_DbConcurrencyConflict_ShouldNotAffectRedisSuccess()
    {
        // 秒杀高并发场景：N 个请求通过 Redis 扣减成功，但 DB 乐观锁只允许第一个提交，
        // 其余抛 DbUpdateConcurrencyException。修复后应：仅创建预占记录 + 发事件，
        // 不调用 activity.DeductStock，DB 不参与扣减热路径，由后台任务/对账同步基线。
        var activity = CreateActivity();
        activity.Activate();
        _repoMock.Setup(r => r.GetByIdAsync(ActivityId, It.IsAny<CancellationToken>())).ReturnsAsync(activity);
        _stockServiceMock.Setup(s => s.TryDeductAsync(ActivityId, SkuId, UserId, 2, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _preOccupationRecordRepoMock.Setup(r => r.AddAsync(It.IsAny<SeckillPreOccupationRecord>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _uowMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await _sut.PlaceOrderAsync(ActivityId, UserId, new SeckillPlaceOrderDto { SkuId = SkuId, Quantity = 2 });

        result.Should().NotBeNull();
        result.OrderId.Should().NotBe(Guid.Empty);
        // 关键断言：不再调用 activity.DeductStock，DB AvailableStock 保持初始值
        activity.AvailableStock.Should().Be(100);
        // Redis 扣减成功后即使 DB 保存失败也不应回退 Redis（DB 不再参与扣减热路径）
        _stockServiceMock.Verify(s => s.RestoreAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _preOccupationRecordRepoMock.Verify(r => r.AddAsync(It.IsAny<SeckillPreOccupationRecord>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PlaceOrderAsync_PreOccupationRecordSaveFailed_ShouldRollbackRedis()
    {
        // 预占记录写入失败（非乐观锁冲突，如网络故障）时仍应回退 Redis，保持最终一致
        var activity = CreateActivity();
        activity.Activate();
        _repoMock.Setup(r => r.GetByIdAsync(ActivityId, It.IsAny<CancellationToken>())).ReturnsAsync(activity);
        _stockServiceMock.Setup(s => s.TryDeductAsync(ActivityId, SkuId, UserId, 2, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _uowMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Network failure"));

        var act = () => _sut.PlaceOrderAsync(ActivityId, UserId, new SeckillPlaceOrderDto { SkuId = SkuId, Quantity = 2 });

        await act.Should().ThrowAsync<InvalidOperationException>();
        _stockServiceMock.Verify(s => s.RestoreAsync(ActivityId, SkuId, 2, It.IsAny<CancellationToken>()), Times.Once);
    }

    private static SeckillActivity CreateActivity()
    {
        return SeckillActivity.Create(
            ActivityId, "测试秒杀活动", SpuId, SkuId, 99m, 199m, 100, 1,
            DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddHours(2));
    }
}
