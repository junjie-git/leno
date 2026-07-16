using Leno.Product.Domain.Aggregates;
using Leno.Product.Domain.Repositories;
using Leno.Product.Domain.ValueObjects;
using Leno.Product.Infrastructure.Consumers;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using Leno.SharedKernel.ValueObjects;
using Microsoft.Extensions.Logging;
using Leno.Infrastructure.Abstractions;
using Moq;

namespace Leno.Product.Infrastructure.Tests;

public class ReviewEventConsumerTests
{
    private readonly Mock<ISPURepository> _spuRepoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<ILogger<ReviewSubmittedEventConsumer>> _submitLoggerMock = new();
    private readonly Mock<ILogger<ReviewHiddenEventConsumer>> _hideLoggerMock = new();
    private readonly Mock<IIdempotencyStore> _idempotencyStoreMock = new();

    private static readonly Guid ShopId = Guid.NewGuid();
    private static readonly Guid SellerId = Guid.NewGuid();
    private static readonly Guid CategoryId = Guid.NewGuid();

    public ReviewEventConsumerTests()
    {
        _idempotencyStoreMock.Setup(s => s.IsProcessedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _idempotencyStoreMock.Setup(s => s.MarkAsProcessedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    #region ReviewSubmittedEventConsumer

    [Fact]
    public async Task ReviewSubmittedEventConsumer_ShouldUpdateProductScore()
    {
        // Arrange
        var spu = CreateOnSaleSpu();
        _spuRepoMock.Setup(r => r.GetByIdAsync(spu.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(spu);

        var consumer = new ReviewSubmittedEventConsumer(
            _spuRepoMock.Object, _unitOfWorkMock.Object, _submitLoggerMock.Object, _idempotencyStoreMock.Object);
        var evt = new ReviewSubmittedEvent(Guid.NewGuid(), Guid.NewGuid(), spu.Id, 5);

        // Act
        await consumer.Consume(CreateConsumeContext(evt));

        // Assert
        spu.Score.Should().Be(5);
        spu.ReviewCount.Should().Be(1);
        _spuRepoMock.Verify(r => r.UpdateAsync(spu, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReviewSubmittedEventConsumer_ProductNotFound_ShouldNotThrow()
    {
        // Arrange
        _spuRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SPU?)null);

        var consumer = new ReviewSubmittedEventConsumer(
            _spuRepoMock.Object, _unitOfWorkMock.Object, _submitLoggerMock.Object, _idempotencyStoreMock.Object);
        var evt = new ReviewSubmittedEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 5);

        // Act
        var act = () => consumer.Consume(CreateConsumeContext(evt));

        // Assert
        await act.Should().NotThrowAsync();
        _unitOfWorkMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReviewSubmittedEventConsumer_MultipleReviews_ShouldCalculateWeightedAverage()
    {
        // Arrange
        var spu = CreateOnSaleSpu();
        spu.UpdateReviewScore(4); // 1 review at 4
        _spuRepoMock.Setup(r => r.GetByIdAsync(spu.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(spu);

        var consumer = new ReviewSubmittedEventConsumer(
            _spuRepoMock.Object, _unitOfWorkMock.Object, _submitLoggerMock.Object, _idempotencyStoreMock.Object);
        var evt = new ReviewSubmittedEvent(Guid.NewGuid(), Guid.NewGuid(), spu.Id, 5);

        // Act
        await consumer.Consume(CreateConsumeContext(evt));

        // Assert
        spu.Score.Should().Be(4.5); // (4+5)/2 = 4.5
        spu.ReviewCount.Should().Be(2);
    }

    [Fact]
    public async Task ReviewSubmittedEventConsumer_Idempotent_ShouldSkipDuplicateEvent()
    {
        // Arrange
        _idempotencyStoreMock.Setup(s => s.IsProcessedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true); // Already processed

        var consumer = new ReviewSubmittedEventConsumer(
            _spuRepoMock.Object, _unitOfWorkMock.Object, _submitLoggerMock.Object, _idempotencyStoreMock.Object);
        var evt = new ReviewSubmittedEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 5);

        // Act
        var act = () => consumer.Consume(CreateConsumeContext(evt));

        // Assert
        await act.Should().NotThrowAsync();
        _spuRepoMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region ReviewHiddenEventConsumer

    [Fact]
    public async Task ReviewHiddenEventConsumer_ShouldRemoveReviewScore()
    {
        // Arrange
        var spu = CreateOnSaleSpu();
        spu.UpdateReviewScore(5);
        spu.UpdateReviewScore(3);
        spu.UpdateReviewScore(4); // Score=4, Count=3
        _spuRepoMock.Setup(r => r.GetByIdAsync(spu.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(spu);

        var consumer = new ReviewHiddenEventConsumer(
            _spuRepoMock.Object, _unitOfWorkMock.Object, _hideLoggerMock.Object, _idempotencyStoreMock.Object);
        var evt = new ReviewHiddenEvent(Guid.NewGuid(), spu.Id, 3);

        // Act
        await consumer.Consume(CreateConsumeContext(evt));

        // Assert
        spu.Score.Should().Be(4.5); // (5+4)/2 = 4.5
        spu.ReviewCount.Should().Be(2);
        _spuRepoMock.Verify(r => r.UpdateAsync(spu, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReviewHiddenEventConsumer_ProductNotFound_ShouldNotThrow()
    {
        // Arrange
        _spuRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SPU?)null);

        var consumer = new ReviewHiddenEventConsumer(
            _spuRepoMock.Object, _unitOfWorkMock.Object, _hideLoggerMock.Object, _idempotencyStoreMock.Object);
        var evt = new ReviewHiddenEvent(Guid.NewGuid(), Guid.NewGuid(), 3);

        // Act
        var act = () => consumer.Consume(CreateConsumeContext(evt));

        // Assert
        await act.Should().NotThrowAsync();
        _unitOfWorkMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReviewHiddenEventConsumer_SingleReview_ShouldResetToZero()
    {
        // Arrange
        var spu = CreateOnSaleSpu();
        spu.UpdateReviewScore(4); // Score=4, Count=1
        _spuRepoMock.Setup(r => r.GetByIdAsync(spu.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(spu);

        var consumer = new ReviewHiddenEventConsumer(
            _spuRepoMock.Object, _unitOfWorkMock.Object, _hideLoggerMock.Object, _idempotencyStoreMock.Object);
        var evt = new ReviewHiddenEvent(Guid.NewGuid(), spu.Id, 4);

        // Act
        await consumer.Consume(CreateConsumeContext(evt));

        // Assert
        spu.Score.Should().Be(0);
        spu.ReviewCount.Should().Be(0);
    }

    [Fact]
    public async Task ReviewHiddenEventConsumer_Idempotent_ShouldSkipDuplicateEvent()
    {
        // Arrange
        _idempotencyStoreMock.Setup(s => s.IsProcessedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true); // Already processed

        var consumer = new ReviewHiddenEventConsumer(
            _spuRepoMock.Object, _unitOfWorkMock.Object, _hideLoggerMock.Object, _idempotencyStoreMock.Object);
        var evt = new ReviewHiddenEvent(Guid.NewGuid(), Guid.NewGuid(), 3);

        // Act
        var act = () => consumer.Consume(CreateConsumeContext(evt));

        // Assert
        await act.Should().NotThrowAsync();
        _spuRepoMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

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

    private static MassTransit.ConsumeContext<T> CreateConsumeContext<T>(T message)
        where T : class
    {
        var mock = new Mock<MassTransit.ConsumeContext<T>>();
        mock.Setup(c => c.Message).Returns(message);
        mock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);
        return mock.Object;
    }
}