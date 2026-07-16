using System.Reflection;
using Leno.Cart.Domain.Repositories;
using Leno.Cart.Infrastructure.Consumers;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;
using Leno.Infrastructure.Abstractions;
using Moq;

namespace Leno.Cart.Infrastructure.Tests;

public class ProductEventConsumerTests
{
    private static readonly Guid ProductId = Guid.NewGuid();
    private static readonly Guid SellerId = Guid.NewGuid();

    [Fact]
    public async Task HandleAsync_ProductTakenDown_ShouldCompleteWithoutError()
    {
        // Arrange
        var (consumer, mockUnitOfWork) = CreateTakenDownConsumer();

        var integrationEvent = new ProductTakenDownEvent
        {
            EventId = Guid.NewGuid(),
            ProductId = ProductId,
            SellerId = SellerId
        };

        // Act
        await InvokeHandleAsync(consumer, integrationEvent);

        // Assert - 商品下架消费者为占位实现，应正常完成不抛异常
        mockUnitOfWork.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ProductPublished_ShouldCompleteWithoutError()
    {
        // Arrange
        var (consumer, mockUnitOfWork) = CreatePublishedConsumer();

        var integrationEvent = new ProductPublishedEvent
        {
            EventId = Guid.NewGuid(),
            ProductId = ProductId,
            SellerId = SellerId
        };

        // Act
        await InvokeHandleAsync(consumer, integrationEvent);

        // Assert - 商品上架消费者为占位实现，应正常完成不抛异常
        mockUnitOfWork.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ProductUpdated_ShouldCompleteWithoutError()
    {
        // Arrange
        var (consumer, mockUnitOfWork) = CreateUpdatedConsumer();

        var integrationEvent = new ProductUpdatedEvent
        {
            EventId = Guid.NewGuid(),
            ProductId = ProductId,
            SellerId = SellerId,
            Title = "更新后标题",
            MainImageUrl = "https://cdn.example.com/img.png"
        };

        // Act
        await InvokeHandleAsync(consumer, integrationEvent);

        // Assert - 商品更新消费者为占位实现，应正常完成不抛异常
        mockUnitOfWork.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private static (ProductTakenDownEventConsumer consumer, Mock<IUnitOfWork> unitOfWork) CreateTakenDownConsumer()
    {
        var mockCartRepo = new Mock<ICartRepository>();
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockLogger = new Mock<ILogger<ProductTakenDownEventConsumer>>();
        var mockIdempotencyStore = new Mock<IIdempotencyStore>();

        var consumer = new ProductTakenDownEventConsumer(
            mockCartRepo.Object, mockUnitOfWork.Object, mockLogger.Object, mockIdempotencyStore.Object);
        return (consumer, mockUnitOfWork);
    }

    private static (ProductPublishedEventConsumer consumer, Mock<IUnitOfWork> unitOfWork) CreatePublishedConsumer()
    {
        var mockCartRepo = new Mock<ICartRepository>();
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockLogger = new Mock<ILogger<ProductPublishedEventConsumer>>();
        var mockIdempotencyStore = new Mock<IIdempotencyStore>();

        var consumer = new ProductPublishedEventConsumer(
            mockCartRepo.Object, mockUnitOfWork.Object, mockLogger.Object, mockIdempotencyStore.Object);
        return (consumer, mockUnitOfWork);
    }

    private static (ProductUpdatedEventConsumer consumer, Mock<IUnitOfWork> unitOfWork) CreateUpdatedConsumer()
    {
        var mockCartRepo = new Mock<ICartRepository>();
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockLogger = new Mock<ILogger<ProductUpdatedEventConsumer>>();
        var mockIdempotencyStore = new Mock<IIdempotencyStore>();

        var consumer = new ProductUpdatedEventConsumer(
            mockCartRepo.Object, mockUnitOfWork.Object, mockLogger.Object, mockIdempotencyStore.Object);
        return (consumer, mockUnitOfWork);
    }

    private static async Task InvokeHandleAsync<TConsumer, TEvent>(TConsumer consumer, TEvent integrationEvent)
        where TConsumer : class
        where TEvent : class
    {
        var handleMethod = typeof(TConsumer)
            .GetMethod("HandleAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(handleMethod);

        await (Task)handleMethod!.Invoke(consumer, [integrationEvent, CancellationToken.None])!;
    }
}
