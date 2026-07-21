using Leno.Order.Domain.Aggregates;
using Leno.Order.Domain.Events;
using Leno.Order.Domain.Repositories;
using Leno.Order.Infrastructure.EventBus;
using Leno.SharedContracts.Events;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;

namespace Leno.Order.Infrastructure.Tests;

public class RedisInventoryRepositoryTests
{
    private static readonly Guid SkuId = Guid.NewGuid();
    private static readonly Guid OrderId = Guid.NewGuid();

    [Fact]
    public async Task ReserveAsync_Success_Should_Persist_StockReservation_Aggregate_And_Publish_Event()
    {
        // Arrange
        var redisMock = new Mock<IConnectionMultiplexer>();
        var dbMock = new Mock<IDatabase>();
        redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(dbMock.Object);
        dbMock.Setup(d => d.ScriptEvaluateAsync(It.IsAny<string>(), It.IsAny<RedisKey[]>(), It.IsAny<RedisValue[]>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisResult.Create(1, ResultType.Integer));

        var stockRepoMock = new Mock<IStockReservationRepository>();
        var loggerMock = new Mock<ILogger<RedisInventoryRepository>>();
        var stockReservation = StockReservation.Create(Guid.NewGuid(), SkuId, 100);
        stockRepoMock.Setup(r => r.GetOrCreateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(stockReservation);

        var sut = new RedisInventoryRepository(redisMock.Object, stockRepoMock.Object, loggerMock.Object);

        // Act
        var success = await sut.ReserveAsync(SkuId, OrderId, 30, CancellationToken.None);

        // Assert
        Assert.True(success);
        stockReservation.ReservedQty.Should().Be(30);
        stockReservation.DomainEvents.Should().Contain(e => e is StockReservedEvent);
        stockRepoMock.Verify(r => r.UpdateAsync(It.Is<StockReservation>(s => s.ReservedQty == 30), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConfirmAsync_Should_Persist_StockReservation_And_Publish_StockConfirmedEvent()
    {
        // Arrange
        var redisMock = new Mock<IConnectionMultiplexer>();
        var dbMock = new Mock<IDatabase>();
        redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(dbMock.Object);
        dbMock.Setup(d => d.ScriptEvaluateAsync(It.IsAny<string>(), It.IsAny<RedisKey[]>(), It.IsAny<RedisValue[]>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisResult.Create(1, ResultType.Integer));

        var stockRepoMock = new Mock<IStockReservationRepository>();
        var loggerMock = new Mock<ILogger<RedisInventoryRepository>>();
        var stockReservation = StockReservation.Create(Guid.NewGuid(), SkuId, 100);
        stockReservation.ReserveStock(OrderId, 30);
        stockRepoMock.Setup(r => r.GetBySkuIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(stockReservation);

        var sut = new RedisInventoryRepository(redisMock.Object, stockRepoMock.Object, loggerMock.Object);

        // Act
        await sut.ConfirmAsync(SkuId, OrderId, 20, CancellationToken.None);

        // Assert
        stockReservation.DeductedQty.Should().Be(20);
        stockReservation.ReservedQty.Should().Be(10);
        stockReservation.DomainEvents.Should().Contain(e => e is StockConfirmedEvent);
        stockRepoMock.Verify(r => r.UpdateAsync(It.IsAny<StockReservation>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReleaseAsync_Should_Persist_StockReservation_And_Publish_StockReleasedEvent()
    {
        // Arrange
        var redisMock = new Mock<IConnectionMultiplexer>();
        var dbMock = new Mock<IDatabase>();
        redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(dbMock.Object);
        dbMock.Setup(d => d.ScriptEvaluateAsync(It.IsAny<string>(), It.IsAny<RedisKey[]>(), It.IsAny<RedisValue[]>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisResult.Create(1, ResultType.Integer));

        var stockRepoMock = new Mock<IStockReservationRepository>();
        var loggerMock = new Mock<ILogger<RedisInventoryRepository>>();
        var stockReservation = StockReservation.Create(Guid.NewGuid(), SkuId, 100);
        stockReservation.ReserveStock(OrderId, 30);
        stockRepoMock.Setup(r => r.GetBySkuIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(stockReservation);

        var sut = new RedisInventoryRepository(redisMock.Object, stockRepoMock.Object, loggerMock.Object);

        // Act
        await sut.ReleaseAsync(SkuId, OrderId, 20, CancellationToken.None);

        // Assert
        stockReservation.ReservedQty.Should().Be(10);
        stockReservation.DomainEvents.Should().Contain(e => e is StockReleasedEvent);
        stockRepoMock.Verify(r => r.UpdateAsync(It.IsAny<StockReservation>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReturnDeductedAsync_Should_Persist_StockReservation_And_Replenish_Deducted()
    {
        // Arrange
        var redisMock = new Mock<IConnectionMultiplexer>();
        var dbMock = new Mock<IDatabase>();
        redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(dbMock.Object);
        dbMock.Setup(d => d.ScriptEvaluateAsync(It.IsAny<string>(), It.IsAny<RedisKey[]>(), It.IsAny<RedisValue[]>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisResult.Create(1, ResultType.Integer));

        var stockRepoMock = new Mock<IStockReservationRepository>();
        var loggerMock = new Mock<ILogger<RedisInventoryRepository>>();
        var stockReservation = StockReservation.Create(Guid.NewGuid(), SkuId, 100);
        stockReservation.ReserveStock(OrderId, 30);
        stockReservation.ConfirmStockDeduction(OrderId, 30);
        // After confirm: ReservedQty=0, DeductedQty=30, BaseLineQty=100, AvailableQty=70
        stockRepoMock.Setup(r => r.GetBySkuIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(stockReservation);

        var sut = new RedisInventoryRepository(redisMock.Object, stockRepoMock.Object, loggerMock.Object);

        // Act
        await sut.ReturnDeductedAsync(SkuId, OrderId, 30, CancellationToken.None);

        // Assert: Replenish(30) 增加 BaseLineQty，DeductedQty 不变
        stockReservation.BaseLineQty.Should().Be(130);
        stockReservation.DeductedQty.Should().Be(30);
        stockReservation.AvailableQty.Should().Be(100);
        stockRepoMock.Verify(r => r.UpdateAsync(It.IsAny<StockReservation>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
