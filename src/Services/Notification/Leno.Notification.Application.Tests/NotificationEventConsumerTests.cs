using Leno.Notification.Domain.Services;
using Leno.Notification.Domain.ValueObjects;
using Leno.Notification.Infrastructure.Consumers;
using Leno.SharedContracts.Events;
using MassTransit;
using Microsoft.Extensions.Logging;
using Moq;

namespace Leno.Notification.Application.Tests;

public class NotificationEventConsumerTests
{
    private readonly Mock<INotificationService> _notificationServiceMock;
    private readonly Mock<ILogger<NotificationEventConsumer>> _loggerMock;
    private readonly NotificationEventConsumer _sut;

    public NotificationEventConsumerTests()
    {
        _notificationServiceMock = new Mock<INotificationService>(MockBehavior.Strict);
        _loggerMock = new Mock<ILogger<NotificationEventConsumer>>();
        _sut = new NotificationEventConsumer(_notificationServiceMock.Object, _loggerMock.Object);
    }

    private void SetupSendAsyncSuccess()
    {
        _notificationServiceMock
            .Setup(s => s.SendAsync(It.IsAny<NotificationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationSendResult { Succeeded = true });
    }

    [Fact]
    public async Task Consume_OrderCreatedEvent_ShouldFireAndForget()
    {
        // Arrange
        SetupSendAsyncSuccess();
        var evt = new OrderCreatedEvent(
            Guid.NewGuid(), Guid.NewGuid(), 99.99m, "CNY", DateTime.UtcNow, []);
        var context = new Mock<ConsumeContext<OrderCreatedEvent>>();
        context.Setup(c => c.Message).Returns(evt);

        // Act
        var result = _sut.Consume(context.Object);

        // Assert - should return completed task immediately (fire-and-forget)
        result.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public async Task Consume_OrderShippedEvent_ShouldFireAndForget()
    {
        // Arrange
        SetupSendAsyncSuccess();
        var evt = new OrderShippedEvent(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SF1234567890", DateTime.UtcNow);
        var context = new Mock<ConsumeContext<OrderShippedEvent>>();
        context.Setup(c => c.Message).Returns(evt);

        // Act
        var result = _sut.Consume(context.Object);

        // Assert
        result.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public async Task Consume_OrderCompletedEvent_ShouldFireAndForget()
    {
        // Arrange
        SetupSendAsyncSuccess();
        var evt = new OrderCompletedEvent(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 150.00m, "CNY", DateTime.UtcNow);
        var context = new Mock<ConsumeContext<OrderCompletedEvent>>();
        context.Setup(c => c.Message).Returns(evt);

        // Act
        var result = _sut.Consume(context.Object);

        // Assert
        result.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public async Task Consume_PaymentSucceededEvent_ShouldFireAndForget()
    {
        // Arrange
        SetupSendAsyncSuccess();
        var evt = new PaymentSucceededEvent(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "WeChat", "T20240101001", 99.99m, "CNY", DateTime.UtcNow);
        var context = new Mock<ConsumeContext<PaymentSucceededEvent>>();
        context.Setup(c => c.Message).Returns(evt);

        // Act
        var result = _sut.Consume(context.Object);

        // Assert
        result.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public async Task Consume_PaymentFailedEvent_ShouldFireAndForget()
    {
        // Arrange
        SetupSendAsyncSuccess();
        var evt = new PaymentFailedEvent(
            Guid.NewGuid(), Guid.NewGuid(), "Insufficient balance", DateTime.UtcNow);
        var context = new Mock<ConsumeContext<PaymentFailedEvent>>();
        context.Setup(c => c.Message).Returns(evt);

        // Act
        var result = _sut.Consume(context.Object);

        // Assert
        result.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public async Task Consume_AfterSalesApprovedEvent_ShouldFireAndForget()
    {
        // Arrange
        SetupSendAsyncSuccess();
        var evt = new AfterSalesApprovedEvent(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 50.00m, "CNY", 0);
        var context = new Mock<ConsumeContext<AfterSalesApprovedEvent>>();
        context.Setup(c => c.Message).Returns(evt);

        // Act
        var result = _sut.Consume(context.Object);

        // Assert
        result.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public async Task Consume_RefundCompletedEvent_ShouldFireAndForget()
    {
        // Arrange
        SetupSendAsyncSuccess();
        var evt = new RefundCompletedEvent(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 50.00m, "CNY", DateTime.UtcNow);
        var context = new Mock<ConsumeContext<RefundCompletedEvent>>();
        context.Setup(c => c.Message).Returns(evt);

        // Act
        var result = _sut.Consume(context.Object);

        // Assert
        result.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public async Task Consume_UserRegisteredEvent_ShouldFireAndForget()
    {
        // Arrange
        SetupSendAsyncSuccess();
        var evt = new UserRegisteredEvent(Guid.NewGuid(), "testuser", "test@example.com", "13800138000");
        var context = new Mock<ConsumeContext<UserRegisteredEvent>>();
        context.Setup(c => c.Message).Returns(evt);

        // Act
        var result = _sut.Consume(context.Object);

        // Assert
        result.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public async Task Consume_OrderCreatedEvent_ShouldUseBuyerIdAsUserId()
    {
        // Arrange
        NotificationRequest? capturedRequest = null;
        _notificationServiceMock
            .Setup(s => s.SendAsync(It.IsAny<NotificationRequest>(), It.IsAny<CancellationToken>()))
            .Callback<NotificationRequest, CancellationToken>((r, _) => capturedRequest = r)
            .ReturnsAsync(new NotificationSendResult { Succeeded = true });

        var buyerId = Guid.NewGuid();
        var evt = new OrderCreatedEvent(
            Guid.NewGuid(), buyerId, 99.99m, "CNY", DateTime.UtcNow, []);
        var context = new Mock<ConsumeContext<OrderCreatedEvent>>();
        context.Setup(c => c.Message).Returns(evt);

        // Act
        _ = _sut.Consume(context.Object);
        // Wait for fire-and-forget to complete
        await Task.Delay(100);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.UserId.Should().Be(buyerId);
        capturedRequest.TemplateCode.Should().Be("order_created");
        capturedRequest.IdempotencyKey.Should().Be(evt.EventId.ToString());
    }

    [Fact]
    public async Task Consume_ShouldUseEventIdForIdempotency()
    {
        // Arrange
        NotificationRequest? capturedRequest = null;
        _notificationServiceMock
            .Setup(s => s.SendAsync(It.IsAny<NotificationRequest>(), It.IsAny<CancellationToken>()))
            .Callback<NotificationRequest, CancellationToken>((r, _) => capturedRequest = r)
            .ReturnsAsync(new NotificationSendResult { Succeeded = true });

        var evt = new UserRegisteredEvent(Guid.NewGuid(), "testuser", "test@example.com", "13800138000");
        var context = new Mock<ConsumeContext<UserRegisteredEvent>>();
        context.Setup(c => c.Message).Returns(evt);

        // Act
        _ = _sut.Consume(context.Object);
        await Task.Delay(100);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.IdempotencyKey.Should().Be(evt.EventId.ToString());
    }

    [Fact]
    public async Task Consume_SendAsyncFails_ShouldNotThrow()
    {
        // Arrange
        _notificationServiceMock
            .Setup(s => s.SendAsync(It.IsAny<NotificationRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Send failed"));

        var evt = new UserRegisteredEvent(Guid.NewGuid(), "testuser", "test@example.com", "13800138000");
        var context = new Mock<ConsumeContext<UserRegisteredEvent>>();
        context.Setup(c => c.Message).Returns(evt);

        // Act - should not throw
        var result = _sut.Consume(context.Object);

        // Assert
        result.IsCompletedSuccessfully.Should().BeTrue();
    }
}