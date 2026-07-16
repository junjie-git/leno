using System.Reflection;
using Leno.Cart.Domain.Aggregates;
using Leno.Cart.Domain.Repositories;
using Leno.Cart.Infrastructure.Consumers;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;
using Leno.Infrastructure.Abstractions;
using Moq;
using CartAggregate = Leno.Cart.Domain.Aggregates.Cart;

namespace Leno.Cart.Infrastructure.Tests;

public class OrderCreatedEventConsumerTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid SellerId = Guid.NewGuid();
    private static readonly Guid SkuId1 = Guid.NewGuid();
    private static readonly Guid SkuId2 = Guid.NewGuid();
    private static readonly Guid OrderId = Guid.NewGuid();

    [Fact]
    public async Task HandleAsync_WithSourceCartItemIds_ShouldClearCartItemsAndSave()
    {
        // Arrange
        var cart = CreateCartWithTwoItems(out var sourceItemIds);
        var mockCartRepo = new Mock<ICartRepository>();
        mockCartRepo.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cart);

        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var (consumer, _) = CreateConsumer(mockCartRepo, mockUnitOfWork);

        var integrationEvent = new OrderCreatedEvent
        {
            EventId = Guid.NewGuid(),
            OrderId = OrderId,
            BuyerId = UserId,
            SellerId = SellerId,
            TotalAmount = 100m,
            SourceCartItemIds = sourceItemIds
        };

        // Act
        await InvokeHandleAsync(consumer, integrationEvent);

        // Assert
        // 1. 购物车项应被清空
        cart.Items.Should().BeEmpty();

        // 2. 工作单元应保存
        mockUnitOfWork.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithEmptySourceCartItemIds_ShouldSkipWithoutClearing()
    {
        // Arrange
        var cart = CreateCartWithTwoItems(out _);
        var mockCartRepo = new Mock<ICartRepository>();
        mockCartRepo.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cart);

        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var (consumer, _) = CreateConsumer(mockCartRepo, mockUnitOfWork);

        var integrationEvent = new OrderCreatedEvent
        {
            EventId = Guid.NewGuid(),
            OrderId = OrderId,
            BuyerId = UserId,
            SellerId = SellerId,
            TotalAmount = 100m,
            SourceCartItemIds = Array.Empty<Guid>()
        };

        // Act
        await InvokeHandleAsync(consumer, integrationEvent);

        // Assert
        // 幂等性：无来源购物车项，跳过清空，购物车项保持不变
        cart.Items.Should().HaveCount(2);
        mockCartRepo.Verify(
            r => r.GetByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        mockUnitOfWork.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenCartNotFound_ShouldSkipWithoutSaving()
    {
        // Arrange
        var mockCartRepo = new Mock<ICartRepository>();
        mockCartRepo.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CartAggregate?)null);

        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var (consumer, _) = CreateConsumer(mockCartRepo, mockUnitOfWork);

        var integrationEvent = new OrderCreatedEvent
        {
            EventId = Guid.NewGuid(),
            OrderId = OrderId,
            BuyerId = UserId,
            SellerId = SellerId,
            TotalAmount = 100m,
            SourceCartItemIds = new[] { Guid.NewGuid() }
        };

        // Act
        await InvokeHandleAsync(consumer, integrationEvent);

        // Assert
        // 幂等性：购物车不存在，跳过保存
        mockUnitOfWork.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenRepositoryThrows_ShouldPropagateException()
    {
        // Arrange
        var mockCartRepo = new Mock<ICartRepository>();
        mockCartRepo.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("数据库连接失败"));

        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var (consumer, _) = CreateConsumer(mockCartRepo, mockUnitOfWork);

        var integrationEvent = new OrderCreatedEvent
        {
            EventId = Guid.NewGuid(),
            OrderId = OrderId,
            BuyerId = UserId,
            SellerId = SellerId,
            TotalAmount = 100m,
            SourceCartItemIds = new[] { Guid.NewGuid() }
        };

        // Act
        var act = async () => await InvokeHandleAsync(consumer, integrationEvent);

        // Assert
        // 依赖调用失败时抛异常，由 MassTransit 重试策略处理
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("数据库连接失败");
        mockUnitOfWork.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private static (OrderCreatedEventConsumer consumer, Mock<IIdempotencyStore> idempotencyStore) CreateConsumer(
        Mock<ICartRepository> mockCartRepo, Mock<IUnitOfWork> mockUnitOfWork)
    {
        var mockLogger = new Mock<ILogger<OrderCreatedEventConsumer>>();
        var mockIdempotencyStore = new Mock<IIdempotencyStore>();

        var consumer = new OrderCreatedEventConsumer(
            mockCartRepo.Object,
            mockUnitOfWork.Object,
            mockLogger.Object,
            mockIdempotencyStore.Object);

        return (consumer, mockIdempotencyStore);
    }

    private static async Task InvokeHandleAsync(OrderCreatedEventConsumer consumer, OrderCreatedEvent integrationEvent)
    {
        var handleMethod = typeof(OrderCreatedEventConsumer)
            .GetMethod("HandleAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(handleMethod);

        await (Task)handleMethod!.Invoke(consumer, [integrationEvent, CancellationToken.None])!;
    }

    private static CartAggregate CreateCartWithTwoItems(out List<Guid> sourceItemIds)
    {
        var cart = CartAggregate.Create(Guid.NewGuid(), UserId);
        cart.AddItem(SkuId1, 3, SellerId);
        cart.AddItem(SkuId2, 5, SellerId);

        sourceItemIds = cart.Items.Select(i => i.SourceCartItemId).ToList();
        return cart;
    }
}
