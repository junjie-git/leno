using Leno.Order.Domain.Aggregates;
using Leno.Order.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;

namespace Leno.Order.Infrastructure.Tests;

/// <summary>
/// 库存 Redis-DB 对账后台服务单元测试。
/// 验证不一致时以 DB 为准刷新 Redis，一致时不刷新。
/// </summary>
public sealed class InventoryReconciliationBackgroundServiceTests : IDisposable
{
    private readonly Mock<IConnectionMultiplexer> _redisMock = new();
    private readonly Mock<IDatabase> _dbMock = new();
    private readonly InventoryReconciliationBackgroundService _sut;

    public InventoryReconciliationBackgroundServiceTests()
    {
        _redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_dbMock.Object);
        var options = Options.Create(new InventoryReconciliationOptions
        {
            Interval = TimeSpan.FromHours(1),
            BatchSize = 100
        });
        _sut = new InventoryReconciliationBackgroundService(
            Mock.Of<IServiceProvider>(),
            _redisMock.Object,
            Mock.Of<ILogger<InventoryReconciliationBackgroundService>>(),
            options);
    }

    public void Dispose() => _sut.Dispose();

    private static StockReservation CreateReservation(Guid skuId, int baseLine, int reserved)
    {
        var reservation = StockReservation.Create(Guid.NewGuid(), skuId, baseLine);
        if (reserved > 0)
        {
            reservation.ReserveStock(Guid.NewGuid(), reserved);
        }
        return reservation;
    }

    #region 不一致时以 DB 刷新 Redis

    [Fact]
    public async Task ReconcileAsync_RedisMismatch_ShouldRefreshRedisFromDb()
    {
        // Arrange：DB 可用库存 = 100 - 30 = 70，Redis 为 50 → 不一致
        var skuId = Guid.NewGuid();
        var reservation = CreateReservation(skuId, baseLine: 100, reserved: 30);

        _dbMock.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisValue)50);

        // Act
        await _sut.ReconcileAsync(new[] { reservation }, CancellationToken.None);

        // Assert：以 DB 值 70 刷新 Redis
        _dbMock.Verify(
            d => d.StringSetAsync(
                It.Is<RedisKey>(k => k.ToString() == $"inventory:stock:{skuId}"),
                It.Is<RedisValue>(v => (int)v == 70),
                It.IsAny<TimeSpan?>(),
                It.IsAny<bool>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()),
            Times.Once);
    }

    #endregion

    #region Redis 键缺失时以 DB 刷新

    [Fact]
    public async Task ReconcileAsync_RedisMissing_ShouldRefreshFromDb()
    {
        // Arrange：Redis 无值（视为 0），DB 可用 70 → 不一致
        var skuId = Guid.NewGuid();
        var reservation = CreateReservation(skuId, baseLine: 100, reserved: 30);

        _dbMock.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        // Act
        await _sut.ReconcileAsync(new[] { reservation }, CancellationToken.None);

        // Assert
        _dbMock.Verify(
            d => d.StringSetAsync(
                It.Is<RedisKey>(k => k.ToString() == $"inventory:stock:{skuId}"),
                It.Is<RedisValue>(v => (int)v == 70),
                It.IsAny<TimeSpan?>(),
                It.IsAny<bool>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()),
            Times.Once);
    }

    #endregion

    #region 一致时不刷新 Redis

    [Fact]
    public async Task ReconcileAsync_RedisConsistent_ShouldNotRefreshRedis()
    {
        // Arrange：DB 可用 70，Redis 也是 70 → 一致
        var skuId = Guid.NewGuid();
        var reservation = CreateReservation(skuId, baseLine: 100, reserved: 30);

        _dbMock.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisValue)70);

        // Act
        await _sut.ReconcileAsync(new[] { reservation }, CancellationToken.None);

        // Assert
        _dbMock.Verify(
            d => d.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<bool>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()),
            Times.Never);
    }

    #endregion

    #region 返回不一致数量

    [Fact]
    public async Task ReconcileAsync_MixedBatch_ShouldReturnMismatchCount()
    {
        // Arrange：两个 SKU，一个一致、一个不一致
        var okSku = Guid.NewGuid();
        var badSku = Guid.NewGuid();
        var ok = CreateReservation(okSku, 100, 30);       // DB 70
        var bad = CreateReservation(badSku, 100, 30);     // DB 70

        _dbMock.Setup(d => d.StringGetAsync(It.Is<RedisKey>(k => k.ToString() == $"inventory:stock:{okSku}"), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisValue)70);
        _dbMock.Setup(d => d.StringGetAsync(It.Is<RedisKey>(k => k.ToString() == $"inventory:stock:{badSku}"), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisValue)50);

        // Act
        var mismatchCount = await _sut.ReconcileAsync(new[] { ok, bad }, CancellationToken.None);

        // Assert
        mismatchCount.Should().Be(1);
    }

    #endregion
}
