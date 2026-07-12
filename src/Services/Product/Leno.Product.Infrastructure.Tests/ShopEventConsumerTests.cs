using Leno.Product.Domain.Aggregates;
using Leno.Product.Domain.Repositories;
using Leno.Product.Domain.ValueObjects;
using Leno.Product.Infrastructure.Consumers;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using Leno.SharedKernel.ValueObjects;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;

namespace Leno.Product.Infrastructure.Tests;

public class ShopEventConsumerTests
{
    private readonly Mock<ISPURepository> _spuRepoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<ILogger<ShopSuspendedEventConsumer>> _suspendLoggerMock = new();
    private readonly Mock<ILogger<ShopResumedEventConsumer>> _resumeLoggerMock = new();
    private readonly Mock<ILogger<ShopClosedEventConsumer>> _closeLoggerMock = new();
    private readonly Mock<IConnectionMultiplexer> _redisMock = new();
    private readonly Mock<IDatabase> _dbMock = new();

    private static readonly Guid ShopId = Guid.NewGuid();
    private static readonly Guid SellerId = Guid.NewGuid();
    private static readonly Guid CategoryId = Guid.NewGuid();

    public ShopEventConsumerTests()
    {
        _redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(_dbMock.Object);
        _dbMock.Setup(d => d.KeyExistsAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(false);
        _dbMock.Setup(d => d.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(),
            It.IsAny<TimeSpan>(), It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);
    }

    [Fact]
    public async Task ShopSuspendedEventConsumer_ShouldSuspendOnSaleProducts()
    {
        // Arrange
        var spu = CreateOnSaleSpu();
        SetupQueryReturns(new[] { spu }, 1);

        var consumer = new ShopSuspendedEventConsumer(
            _spuRepoMock.Object, _unitOfWorkMock.Object, _suspendLoggerMock.Object, _redisMock.Object);
        var evt = new ShopSuspendedEvent(ShopId, SellerId);

        // Act
        await consumer.Consume(CreateConsumeContext(evt));

        // Assert
        spu.Status.Should().Be(ProductStatus.ShopSuspended);
        spu.SuspendedByShop.Should().BeTrue();
        _unitOfWorkMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ShopSuspendedEventConsumer_EmptyProducts_ShouldNotThrow()
    {
        // Arrange
        SetupQueryReturns(Array.Empty<SPU>(), 0);

        var consumer = new ShopSuspendedEventConsumer(
            _spuRepoMock.Object, _unitOfWorkMock.Object, _suspendLoggerMock.Object, _redisMock.Object);
        var evt = new ShopSuspendedEvent(ShopId, SellerId);

        // Act
        var act = () => consumer.Consume(CreateConsumeContext(evt));

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ShopResumedEventConsumer_ShouldResumeSuspendedProducts()
    {
        // Arrange
        var spu = CreateOnSaleSpu();
        spu.SuspendByShop();
        spu.Status.Should().Be(ProductStatus.ShopSuspended);
        spu.SuspendedByShop.Should().BeTrue();

        SetupQueryReturns(new[] { spu }, 1);

        var consumer = new ShopResumedEventConsumer(
            _spuRepoMock.Object, _unitOfWorkMock.Object, _resumeLoggerMock.Object, _redisMock.Object);
        var evt = new ShopResumedEvent(ShopId, SellerId);

        // Act
        await consumer.Consume(CreateConsumeContext(evt));

        // Assert
        spu.Status.Should().Be(ProductStatus.OnSale);
        spu.SuspendedByShop.Should().BeFalse();
    }

    [Fact]
    public async Task ShopResumedEventConsumer_ManuallyTakenDownProducts_ShouldNotResume()
    {
        // Arrange
        var spu = CreateOnSaleSpu();
        spu.TakeDown("manual take down");
        spu.Status.Should().Be(ProductStatus.TakenDown);
        spu.SuspendedByShop.Should().BeFalse();

        SetupQueryReturns(new[] { spu }, 1);

        var consumer = new ShopResumedEventConsumer(
            _spuRepoMock.Object, _unitOfWorkMock.Object, _resumeLoggerMock.Object, _redisMock.Object);
        var evt = new ShopResumedEvent(ShopId, SellerId);

        // Act
        await consumer.Consume(CreateConsumeContext(evt));

        // Assert
        spu.Status.Should().Be(ProductStatus.TakenDown); // 不应恢复
        spu.SuspendedByShop.Should().BeFalse();
    }

    [Fact]
    public async Task ShopClosedEventConsumer_ShouldTakeDownOnSaleProducts()
    {
        // Arrange
        var spu = CreateOnSaleSpu();
        SetupQueryReturns(new[] { spu }, 1);

        var consumer = new ShopClosedEventConsumer(
            _spuRepoMock.Object, _unitOfWorkMock.Object, _closeLoggerMock.Object, _redisMock.Object);
        var evt = new ShopClosedEvent(ShopId, SellerId);

        // Act
        await consumer.Consume(CreateConsumeContext(evt));

        // Assert
        spu.Status.Should().Be(ProductStatus.TakenDown);
        spu.SuspendedByShop.Should().BeFalse();
    }

    [Fact]
    public async Task ShopClosedEventConsumer_AlreadyTakenDownProducts_ShouldNotChange()
    {
        // Arrange
        var spu = CreateOnSaleSpu();
        spu.TakeDown("already off shelf");
        SetupQueryReturns(new[] { spu }, 1);

        var consumer = new ShopClosedEventConsumer(
            _spuRepoMock.Object, _unitOfWorkMock.Object, _closeLoggerMock.Object, _redisMock.Object);
        var evt = new ShopClosedEvent(ShopId, SellerId);

        // Act
        await consumer.Consume(CreateConsumeContext(evt));

        // Assert
        spu.Status.Should().Be(ProductStatus.TakenDown);
    }

    [Fact]
    public async Task ShopEventConsumer_Pagination_ShouldProcessAllPages()
    {
        // Arrange
        var spu1 = CreateOnSaleSpu();
        var spu2 = CreateOnSaleSpu();

        // 模拟分页：第一页返回1条，第二页返回1条
        _spuRepoMock
            .Setup(r => r.QueryAsync(ShopId, null, ProductStatus.OnSale, null, null, 1, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<SPU> { spu1 }, 2));
        _spuRepoMock
            .Setup(r => r.QueryAsync(ShopId, null, ProductStatus.OnSale, null, null, 2, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<SPU> { spu2 }, 2));

        var consumer = new ShopSuspendedEventConsumer(
            _spuRepoMock.Object, _unitOfWorkMock.Object, _suspendLoggerMock.Object, _redisMock.Object);
        var evt = new ShopSuspendedEvent(ShopId, SellerId);

        // Act
        await consumer.Consume(CreateConsumeContext(evt));

        // Assert
        spu1.Status.Should().Be(ProductStatus.ShopSuspended);
        spu1.SuspendedByShop.Should().BeTrue();
        spu2.Status.Should().Be(ProductStatus.ShopSuspended);
        spu2.SuspendedByShop.Should().BeTrue();
        _unitOfWorkMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.AtLeast(2));
    }

    [Fact]
    public async Task ShopEventConsumer_Idempotent_ShouldSkipDuplicateEvent()
    {
        // Arrange
        _dbMock.Setup(d => d.KeyExistsAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true); // 已处理

        var consumer = new ShopSuspendedEventConsumer(
            _spuRepoMock.Object, _unitOfWorkMock.Object, _suspendLoggerMock.Object, _redisMock.Object);
        var evt = new ShopSuspendedEvent(ShopId, SellerId);

        // Act
        var act = () => consumer.Consume(CreateConsumeContext(evt));

        // Assert
        await act.Should().NotThrowAsync();
        _spuRepoMock.Verify(r => r.QueryAsync(It.IsAny<Guid?>(), It.IsAny<Guid?>(),
            It.IsAny<ProductStatus?>(), It.IsAny<Guid?>(), It.IsAny<string?>(),
            It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private SPU CreateOnSaleSpu()
    {
        var spu = SPU.Create(Guid.NewGuid(), ShopId, SellerId, "Test Product",
            "https://img.example.com/1.jpg", CategoryId, images: []);
        var sku = SKU.Create(Guid.NewGuid(), spu.Id, $"SKU-{Guid.NewGuid():N}"[..8],
            Money.Create(99.99m, "CNY"), 100,
            SkuSpec.Create([SpecAttribute.Create("Color", "Red")]));
        spu.AddSku(sku);
        spu.SubmitForReview();
        spu.Approve(Guid.NewGuid());
        return spu;
    }

    private void SetupQueryReturns(IReadOnlyList<SPU> items, int total)
    {
        _spuRepoMock
            .Setup(r => r.QueryAsync(ShopId, null, It.IsAny<ProductStatus?>(), null, null, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((items, total));
    }

    private static MassTransit.ConsumeContext<T> CreateConsumeContext<T>(T message)
        where T : class
    {
        var mock = new Mock<MassTransit.ConsumeContext<T>>();
        mock.Setup(c => c.Message).Returns(message);
        mock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);
        return mock.Object;
    }
}