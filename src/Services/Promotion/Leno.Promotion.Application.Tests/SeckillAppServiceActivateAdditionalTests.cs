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
/// P0-2.7 补充测试：覆盖 SeckillAppService.ActivateAsync 在 Redis 初始化失败/成功场景下的状态一致性。
/// </summary>
public class SeckillAppServiceActivateAdditionalTests
{
    private readonly Mock<ISeckillActivityRepository> _repoMock = new();
    private readonly Mock<ISeckillStockService> _stockServiceMock = new();
    private readonly Mock<ISeckillPreOccupationRecordRepository> _preOccupationRecordRepoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly SeckillAppService _sut;

    private static readonly Guid ActivityId = Guid.NewGuid();
    private static readonly Guid SpuId = Guid.NewGuid();
    private static readonly Guid SkuId = Guid.NewGuid();

    public SeckillAppServiceActivateAdditionalTests()
    {
        _sut = new SeckillAppService(_repoMock.Object, _stockServiceMock.Object, _preOccupationRecordRepoMock.Object, _uowMock.Object);
    }

    [Fact]
    public async Task ActivateAsync_RedisInitFailed_ShouldNotMarkActivityActive()
    {
        // Redis 初始化失败时，聚合状态不应被持久化为 Active
        var activity = CreateActivity();
        _repoMock.Setup(r => r.GetByIdAsync(ActivityId, It.IsAny<CancellationToken>())).ReturnsAsync(activity);
        _stockServiceMock.Setup(s => s.InitializeAsync(ActivityId, It.IsAny<Dictionary<Guid, int>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Redis connection refused"));
        _uowMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var act = () => _sut.ActivateAsync(ActivityId);

        await act.Should().ThrowAsync<PromotionDomainException>()
            .WithMessage("*Redis*");
        // 关键断言：聚合内存状态回退为 Pending（未被持久化）
        activity.Status.Should().Be(SeckillStatus.Pending);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ActivateAsync_RedisInitSucceeded_ShouldMarkActiveAndSave()
    {
        var activity = CreateActivity();
        _repoMock.Setup(r => r.GetByIdAsync(ActivityId, It.IsAny<CancellationToken>())).ReturnsAsync(activity);
        _stockServiceMock.Setup(s => s.InitializeAsync(ActivityId, It.IsAny<Dictionary<Guid, int>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _uowMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await _sut.ActivateAsync(ActivityId);

        activity.Status.Should().Be(SeckillStatus.Active);
        _stockServiceMock.Verify(s => s.InitializeAsync(ActivityId, It.IsAny<Dictionary<Guid, int>>(), It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private static SeckillActivity CreateActivity()
    {
        return SeckillActivity.Create(
            ActivityId, "测试秒杀活动", SpuId, SkuId, 99m, 199m, 100, 1,
            DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddHours(2));
    }
}
