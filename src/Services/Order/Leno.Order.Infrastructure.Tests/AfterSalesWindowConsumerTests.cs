using System.Reflection;
using Leno.Order.Application.Messages;
using Leno.Order.Domain.Aggregates;
using Leno.Order.Domain.Repositories;
using Leno.Order.Domain.ValueObjects;
using Leno.Order.Infrastructure.Consumers;
using Leno.SharedKernel.Abstractions;
using MassTransit;
using Microsoft.Extensions.Logging;
using Moq;
using OrderAggregate = Leno.Order.Domain.Aggregates.Order;

namespace Leno.Order.Infrastructure.Tests;

public class AfterSalesWindowConsumerTests
{
    private readonly Mock<IOrderRepository> _orderRepoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<ILogger<AfterSalesWindowConsumer>> _loggerMock = new();
    private readonly AfterSalesWindowConsumer _sut;

    private static readonly Guid OrderId = Guid.NewGuid();
    private static readonly Guid SkuId = Guid.NewGuid();
    private static readonly Guid SellerId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    public AfterSalesWindowConsumerTests()
    {
        _sut = new AfterSalesWindowConsumer(
            _orderRepoMock.Object,
            _uowMock.Object,
            _loggerMock.Object);
    }

    private static Mock<ConsumeContext<AfterSalesWindowMessage>> CreateConsumeContext(AfterSalesWindowMessage message, CancellationToken ct = default)
    {
        var mock = new Mock<ConsumeContext<AfterSalesWindowMessage>>();
        mock.Setup(c => c.Message).Returns(message);
        mock.Setup(c => c.CancellationToken).Returns(ct == default ? CancellationToken.None : ct);
        return mock;
    }

    private static OrderAggregate CreateCompletedOrder()
    {
        var snapshot = ProductSnapshot.Create(SkuId, Guid.NewGuid(), "Test Product", "Red-XL", null, SellerId);
        var item = OrderItem.Create(Guid.NewGuid(), SkuId, snapshot, 99.99m, 1, null);
        var order = OrderAggregate.Create(
            OrderId, "ORD-001", OrderType.Normal, UserId, SellerId,
            new List<OrderItem> { item }, CreateAddress(), 10m, 0m, DateTime.UtcNow.AddHours(1));
        order.MarkPaymentInitiated(PaymentMethod.WeChatPay);
        order.MarkAsPaid(Guid.NewGuid(), "WeChatPay", DateTime.UtcNow, "T001", order.TotalAmount);
        order.Ship("SF123", "SF", DateTime.UtcNow, Guid.NewGuid());
        order.ConfirmReceipt();
        // Set AfterSalesWindowEndsAt to past
        typeof(OrderAggregate).GetProperty(nameof(OrderAggregate.AfterSalesWindowEndsAt))!
            .SetValue(order, DateTime.UtcNow.AddDays(-1));
        return order;
    }

    private static AddressSnapshot CreateAddress()
        => AddressSnapshot.Create("张三", "13800138000", "广东", "深圳", "南山区", "科技园路1号");

    [Fact]
    public async Task Consume_CompletedOrderWithPastWindow_ShouldCloseAndSave()
    {
        // Arrange
        var order = CreateCompletedOrder();
        _orderRepoMock.Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var msg = new AfterSalesWindowMessage(OrderId);
        var context = CreateConsumeContext(msg);

        // Act
        await _sut.Consume(context.Object);

        // Assert
        order.Status.Should().Be(OrderStatus.Closed);
        _orderRepoMock.Verify(r => r.UpdateAsync(order, It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Consume_OrderNotFound_ShouldSkip()
    {
        // Arrange
        _orderRepoMock.Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrderAggregate?)null);

        var msg = new AfterSalesWindowMessage(OrderId);
        var context = CreateConsumeContext(msg);

        // Act
        await _sut.Consume(context.Object);

        // Assert
        _orderRepoMock.Verify(r => r.UpdateAsync(It.IsAny<OrderAggregate>(), It.IsAny<CancellationToken>()), Times.Never);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Consume_OrderNotCompleted_ShouldSkip()
    {
        // Arrange
        var snapshot = ProductSnapshot.Create(SkuId, Guid.NewGuid(), "Test Product", "Red-XL", null, SellerId);
        var item = OrderItem.Create(Guid.NewGuid(), SkuId, snapshot, 99.99m, 1, null);
        var order = OrderAggregate.Create(
            OrderId, "ORD-001", OrderType.Normal, UserId, SellerId,
            new List<OrderItem> { item }, CreateAddress(), 10m, 0m, DateTime.UtcNow.AddHours(1));
        // Order is still PendingPayment
        _orderRepoMock.Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var msg = new AfterSalesWindowMessage(OrderId);
        var context = CreateConsumeContext(msg);

        // Act
        await _sut.Consume(context.Object);

        // Assert
        _orderRepoMock.Verify(r => r.UpdateAsync(It.IsAny<OrderAggregate>(), It.IsAny<CancellationToken>()), Times.Never);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}