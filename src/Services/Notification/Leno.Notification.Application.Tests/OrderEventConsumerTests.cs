using Leno.Notification.Domain.Services;
using Leno.Notification.Domain.ValueObjects;
using Leno.Notification.Infrastructure.Consumers;
using Leno.SharedContracts.Events;
using MassTransit;
using Microsoft.Extensions.Logging;
using Moq;

namespace Leno.Notification.Application.Tests;

public class OrderEventConsumerTests
{
    private readonly Mock<INotificationService> _notificationServiceMock;
    private readonly Mock<ILogger<OrderEventConsumer>> _loggerMock;
    private readonly OrderEventConsumer _sut;

    public OrderEventConsumerTests()
    {
        _notificationServiceMock = new Mock<INotificationService>(MockBehavior.Strict);
        _loggerMock = new Mock<ILogger<OrderEventConsumer>>();
        _sut = new OrderEventConsumer(_notificationServiceMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task Consume_OrderCreatedEvent_ShouldSendNotificationRequest()
    {
        // Arrange
        NotificationRequest? capturedRequest = null;
        _notificationServiceMock
            .Setup(s => s.SendAsync(It.IsAny<NotificationRequest>(), It.IsAny<CancellationToken>()))
            .Callback<NotificationRequest, CancellationToken>((r, _) => capturedRequest = r)
            .ReturnsAsync(new NotificationSendResult { Succeeded = true });

        var buyerId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var evt = new OrderCreatedEvent(orderId, buyerId, Guid.NewGuid(), 199.99m, "CNY", DateTime.UtcNow, []);
        var context = new Mock<ConsumeContext<OrderCreatedEvent>>();
        context.Setup(c => c.Message).Returns(evt);
        context.Setup(c => c.CancellationToken).Returns(CancellationToken.None);

        // Act
        await _sut.Consume(context.Object);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.TemplateCode.Should().Be("order_created");
        capturedRequest.UserId.Should().Be(buyerId);
        capturedRequest.IdempotencyKey.Should().Be(evt.EventId.ToString());
        capturedRequest.Variables["orderId"].Should().Be(orderId.ToString());
        capturedRequest.Variables["totalAmount"].Should().Be("199.99");
        capturedRequest.Variables["currency"].Should().Be("CNY");
    }

    [Fact]
    public async Task Consume_OrderShippedEvent_ShouldSendNotificationRequest()
    {
        // Arrange
        NotificationRequest? capturedRequest = null;
        _notificationServiceMock
            .Setup(s => s.SendAsync(It.IsAny<NotificationRequest>(), It.IsAny<CancellationToken>()))
            .Callback<NotificationRequest, CancellationToken>((r, _) => capturedRequest = r)
            .ReturnsAsync(new NotificationSendResult { Succeeded = true });

        var userId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var evt = new OrderShippedEvent(orderId, userId, Guid.NewGuid(), "SF1234567890", DateTime.UtcNow);
        var context = new Mock<ConsumeContext<OrderShippedEvent>>();
        context.Setup(c => c.Message).Returns(evt);
        context.Setup(c => c.CancellationToken).Returns(CancellationToken.None);

        // Act
        await _sut.Consume(context.Object);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.TemplateCode.Should().Be("order_shipped");
        capturedRequest.UserId.Should().Be(userId);
        capturedRequest.IdempotencyKey.Should().Be(evt.EventId.ToString());
        capturedRequest.Variables["orderId"].Should().Be(orderId.ToString());
        capturedRequest.Variables["logisticsNo"].Should().Be("SF1234567890");
    }

    [Fact]
    public async Task Consume_OrderCompletedEvent_ShouldSendNotificationRequest()
    {
        // Arrange
        NotificationRequest? capturedRequest = null;
        _notificationServiceMock
            .Setup(s => s.SendAsync(It.IsAny<NotificationRequest>(), It.IsAny<CancellationToken>()))
            .Callback<NotificationRequest, CancellationToken>((r, _) => capturedRequest = r)
            .ReturnsAsync(new NotificationSendResult { Succeeded = true });

        var userId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var evt = new OrderCompletedEvent(orderId, userId, Guid.NewGuid(), 299.99m, "CNY", DateTime.UtcNow);
        var context = new Mock<ConsumeContext<OrderCompletedEvent>>();
        context.Setup(c => c.Message).Returns(evt);
        context.Setup(c => c.CancellationToken).Returns(CancellationToken.None);

        // Act
        await _sut.Consume(context.Object);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.TemplateCode.Should().Be("order_completed");
        capturedRequest.UserId.Should().Be(userId);
        capturedRequest.IdempotencyKey.Should().Be(evt.EventId.ToString());
        capturedRequest.Variables["orderId"].Should().Be(orderId.ToString());
        capturedRequest.Variables["totalAmount"].Should().Be("299.99");
        capturedRequest.Variables["currency"].Should().Be("CNY");
    }

    [Fact]
    public async Task Consume_NullContext_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        var act = () => _sut.Consume((ConsumeContext<OrderCreatedEvent>)null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}