using Leno.Promotion.Domain.Aggregates;
using Leno.Promotion.Domain.Exceptions;
using Leno.Promotion.Domain.Repositories;
using Leno.Promotion.Infrastructure.Services;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;

namespace Leno.Promotion.Infrastructure.Tests;

/// <summary>
/// RedisSeckillStockService.WriteBackToDbAsync 单元测试。
/// 验证 Redis 剩余库存真实回写 SeckillActivity 聚合并通过 UnitOfWork 持久化。
/// </summary>
public class RedisSeckillStockServiceTests
{
    private readonly Mock<IConnectionMultiplexer> _redisMock = new();
    private readonly Mock<IDatabase> _dbMock = new();
    private readonly Mock<ISeckillActivityRepository> _repoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<ILogger<RedisSeckillStockService>> _loggerMock = new();
    private readonly RedisSeckillStockService _sut;

    public RedisSeckillStockServiceTests()
    {
        _redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_dbMock.Object);
        _uowMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _sut = new RedisSeckillStockService(_redisMock.Object, _repoMock.Object, _uowMock.Object, _loggerMock.Object);
    }

    private static SeckillActivity CreateActiveActivity(int totalStock)
    {
        var activity = SeckillActivity.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            99m, 199m, totalStock, 5,
            DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddHours(2));
        activity.Activate();
        return activity;
    }

    private void SetupRedisStock(Guid activityId, Guid skuId, int remainingStock)
    {
        _dbMock.Setup(d => d.HashGetAllAsync(It.Is<RedisKey>(k => k.ToString() == $"seckill:{activityId}:stock"), It.IsAny<CommandFlags>()))
            .ReturnsAsync(new HashEntry[] { new(skuId.ToString(), remainingStock) });
    }

    #region Redis 剩余 < DB 基线 → 同步并保存

    [Fact]
    public async Task WriteBackToDbAsync_RedisLowerThanDb_ShouldSyncAvailableStockAndSave()
    {
        // Arrange：DB 基线 100，Redis 剩余 80 → 应同步为 80
        var activity = CreateActiveActivity(100);
        SetupRedisStock(activity.Id, activity.SkuId, remainingStock: 80);
        _repoMock.Setup(r => r.GetByIdAsync(activity.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(activity);

        // Act
        await _sut.WriteBackToDbAsync(activity.Id, CancellationToken.None);

        // Assert
        activity.AvailableStock.Should().Be(80);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Redis 剩余 ≥ DB 基线 → 不覆盖

    [Fact]
    public async Task WriteBackToDbAsync_RedisNotLowerThanDb_ShouldNotOverwriteStock()
    {
        // Arrange：DB 已扣减到 80（模拟并发已回写），Redis 也是 80 → 不应变化
        var activity = CreateActiveActivity(100);
        activity.DeductStock(Guid.NewGuid(), 20); // AvailableStock = 80
        SetupRedisStock(activity.Id, activity.SkuId, remainingStock: 80);
        _repoMock.Setup(r => r.GetByIdAsync(activity.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(activity);

        // Act
        await _sut.WriteBackToDbAsync(activity.Id, CancellationToken.None);

        // Assert：未被 Redis 值覆盖（相等不更新）
        activity.AvailableStock.Should().Be(80);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region 活动 Close 后仍能按 ID 加载并回写库存（P1-3.8 核心修复）

    [Fact]
    public async Task WriteBackToDbAsync_ClosedActivity_ShouldStillSyncStock()
    {
        // 原 bug：按 SkuId + Active 过滤，活动 Close 后返回 null 跳过同步
        // 修复后：按 activityId 加载，Closed 态仍能回写 DB 库存基线
        var activity = CreateActiveActivity(100);
        activity.Close(); // Status = Closed
        SetupRedisStock(activity.Id, activity.SkuId, remainingStock: 80);
        _repoMock.Setup(r => r.GetByIdAsync(activity.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(activity);

        // Act
        await _sut.WriteBackToDbAsync(activity.Id, CancellationToken.None);

        // Assert：Closed 态下仍同步库存
        activity.AvailableStock.Should().Be(80);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region 活动不存在 → 跳过并不抛异常

    [Fact]
    public async Task WriteBackToDbAsync_ActivityNotFound_ShouldSkipAndNotSave()
    {
        // Arrange
        var activityId = Guid.NewGuid();
        _repoMock.Setup(r => r.GetByIdAsync(activityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SeckillActivity?)null);

        // Act
        await _sut.WriteBackToDbAsync(activityId, CancellationToken.None);

        // Assert：活动不存在，不保存
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region Redis 无库存数据

    [Fact]
    public async Task WriteBackToDbAsync_EmptyRedisStock_ShouldNotThrow()
    {
        // Arrange：活动存在但 Redis 无库存数据
        var activity = CreateActiveActivity(100);
        _repoMock.Setup(r => r.GetByIdAsync(activity.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(activity);
        _dbMock.Setup(d => d.HashGetAllAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(Array.Empty<HashEntry>());

        // Act
        await _sut.WriteBackToDbAsync(activity.Id, CancellationToken.None);

        // Assert：无 SKU 可回写，仍完成调用并保存
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion
}

/// <summary>
/// SeckillActivity.SyncFromRedis 聚合方法单元测试。
/// </summary>
public class SeckillActivitySyncFromRedisTests
{
    private static SeckillActivity CreateActiveActivity(int totalStock)
    {
        var activity = SeckillActivity.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            99m, 199m, totalStock, 5,
            DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddHours(2));
        activity.Activate();
        return activity;
    }

    #region Redis 剩余小于当前 → 更新

    [Fact]
    public void SyncFromRedis_LowerThanCurrent_ShouldUpdateAvailableStock()
    {
        var activity = CreateActiveActivity(100);

        activity.SyncFromRedis(80);

        activity.AvailableStock.Should().Be(80);
    }

    #endregion

    #region Redis 剩余不小于当前 → 保持

    [Fact]
    public void SyncFromRedis_NotLowerThanCurrent_ShouldKeepCurrent()
    {
        var activity = CreateActiveActivity(100);

        activity.SyncFromRedis(100); // 相等不更新
        activity.AvailableStock.Should().Be(100);

        activity.SyncFromRedis(150); // 大于不更新，避免覆盖回退
        activity.AvailableStock.Should().Be(100);
    }

    #endregion

    #region 负值 → 抛领域异常

    [Fact]
    public void SyncFromRedis_Negative_ShouldThrow()
    {
        var activity = CreateActiveActivity(100);

        var act = () => activity.SyncFromRedis(-1);

        act.Should().Throw<PromotionDomainException>();
    }

    #endregion

    #region Closed 态同步 → 允许（CloseActivityWithStockWriteBackAsync 调用链所需）

    [Fact]
    public void SyncFromRedis_ClosedActivity_ShouldStillSync()
    {
        // CloseActivityWithStockWriteBackAsync 先 Close() 再回写库存，
        // SyncFromRedis 须在 Closed 态下允许同步（DeductStock 已禁 Closed 态扣减，同步单调递减安全）
        var activity = CreateActiveActivity(100);
        activity.Close();

        activity.SyncFromRedis(80);

        activity.AvailableStock.Should().Be(80);
    }

    #endregion
}
