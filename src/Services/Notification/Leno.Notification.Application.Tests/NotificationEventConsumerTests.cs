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
    public async Task Consume_OrderCreatedEvent_ShouldSendNotification()
    {
        // Arrange
        SetupSendAsyncSuccess();
        var evt = new OrderCreatedEvent(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 99.99m, "CNY", DateTime.UtcNow, []);
        var context = new Mock<ConsumeContext<OrderCreatedEvent>>();
        context.Setup(c => c.Message).Returns(evt);

        // Act - 直接 await Consume，异常会冒泡到 MassTransit 重试
        await _sut.Consume(context.Object);

        // Assert - SendAsync 被调用一次
        _notificationServiceMock.Verify(
            s => s.SendAsync(It.IsAny<NotificationRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Consume_OrderShippedEvent_ShouldSendNotification()
    {
        // Arrange
        SetupSendAsyncSuccess();
        var evt = new OrderShippedEvent(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SF1234567890", DateTime.UtcNow);
        var context = new Mock<ConsumeContext<OrderShippedEvent>>();
        context.Setup(c => c.Message).Returns(evt);

        // Act
        await _sut.Consume(context.Object);

        // Assert
        _notificationServiceMock.Verify(
            s => s.SendAsync(It.IsAny<NotificationRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Consume_OrderCompletedEvent_ShouldSendNotification()
    {
        // Arrange
        SetupSendAsyncSuccess();
        var evt = new OrderCompletedEvent(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 150.00m, "CNY", DateTime.UtcNow);
        var context = new Mock<ConsumeContext<OrderCompletedEvent>>();
        context.Setup(c => c.Message).Returns(evt);

        // Act
        await _sut.Consume(context.Object);

        // Assert
        _notificationServiceMock.Verify(
            s => s.SendAsync(It.IsAny<NotificationRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Consume_PaymentSucceededEvent_ShouldSendNotification()
    {
        // Arrange
        SetupSendAsyncSuccess();
        var evt = new PaymentSucceededEvent(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "WeChat", "T20240101001", 99.99m, "CNY", DateTime.UtcNow);
        var context = new Mock<ConsumeContext<PaymentSucceededEvent>>();
        context.Setup(c => c.Message).Returns(evt);

        // Act
        await _sut.Consume(context.Object);

        // Assert
        _notificationServiceMock.Verify(
            s => s.SendAsync(It.IsAny<NotificationRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Consume_PaymentFailedEvent_ShouldSendNotification()
    {
        // Arrange
        SetupSendAsyncSuccess();
        var evt = new PaymentFailedEvent(
            Guid.NewGuid(), Guid.NewGuid(), "Insufficient balance", DateTime.UtcNow);
        var context = new Mock<ConsumeContext<PaymentFailedEvent>>();
        context.Setup(c => c.Message).Returns(evt);

        // Act
        await _sut.Consume(context.Object);

        // Assert
        _notificationServiceMock.Verify(
            s => s.SendAsync(It.IsAny<NotificationRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Consume_AfterSalesApprovedEvent_ShouldSendNotification()
    {
        // Arrange
        SetupSendAsyncSuccess();
        var evt = new AfterSalesApprovedEvent(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 50.00m, "CNY", 0);
        var context = new Mock<ConsumeContext<AfterSalesApprovedEvent>>();
        context.Setup(c => c.Message).Returns(evt);

        // Act
        await _sut.Consume(context.Object);

        // Assert
        _notificationServiceMock.Verify(
            s => s.SendAsync(It.IsAny<NotificationRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Consume_RefundCompletedEvent_ShouldSendNotification()
    {
        // Arrange
        SetupSendAsyncSuccess();
        var evt = new RefundCompletedEvent(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 50.00m, "CNY", DateTime.UtcNow);
        var context = new Mock<ConsumeContext<RefundCompletedEvent>>();
        context.Setup(c => c.Message).Returns(evt);

        // Act
        await _sut.Consume(context.Object);

        // Assert
        _notificationServiceMock.Verify(
            s => s.SendAsync(It.IsAny<NotificationRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Consume_UserRegisteredEvent_ShouldSendNotification()
    {
        // Arrange
        SetupSendAsyncSuccess();
        var evt = new UserRegisteredEvent(Guid.NewGuid(), "testuser", "test@example.com", "13800138000");
        var context = new Mock<ConsumeContext<UserRegisteredEvent>>();
        context.Setup(c => c.Message).Returns(evt);

        // Act
        await _sut.Consume(context.Object);

        // Assert
        _notificationServiceMock.Verify(
            s => s.SendAsync(It.IsAny<NotificationRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
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
            Guid.NewGuid(), buyerId, Guid.NewGuid(), 99.99m, "CNY", DateTime.UtcNow, []);
        var context = new Mock<ConsumeContext<OrderCreatedEvent>>();
        context.Setup(c => c.Message).Returns(evt);

        // Act - 直接 await Consume，无需 Task.Delay 等待 fire-and-forget
        await _sut.Consume(context.Object);

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
        await _sut.Consume(context.Object);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.IdempotencyKey.Should().Be(evt.EventId.ToString());
    }

    [Fact]
    public async Task Consume_SendAsyncFails_ShouldThrow()
    {
        // Arrange - 模拟通知发送失败（如 SMS 网关宕机）
        _notificationServiceMock
            .Setup(s => s.SendAsync(It.IsAny<NotificationRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SMS gateway down"));

        var evt = new UserRegisteredEvent(Guid.NewGuid(), "testuser", "test@example.com", "13800138000");
        var context = new Mock<ConsumeContext<UserRegisteredEvent>>();
        context.Setup(c => c.Message).Returns(evt);

        // Act + Assert - 异常应冒泡到 MassTransit 触发重试，而非被吞掉
        // 当前 fire-and-forget 实现下 Consume 立即返回 Task.CompletedTask，本测试应失败
        await FluentActions.Awaiting(() => _sut.Consume(context.Object))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("SMS gateway down");
    }

    [Fact]
    public async Task Consume_OrderCreated_ShouldSendNotification()
    {
        // Arrange - 验证 await 后 SendAsync 被调用且参数正确
        NotificationRequest? capturedRequest = null;
        _notificationServiceMock
            .Setup(s => s.SendAsync(It.IsAny<NotificationRequest>(), It.IsAny<CancellationToken>()))
            .Callback<NotificationRequest, CancellationToken>((r, _) => capturedRequest = r)
            .ReturnsAsync(new NotificationSendResult { Succeeded = true });

        var buyerId = Guid.NewGuid();
        var evt = new OrderCreatedEvent(
            Guid.NewGuid(), buyerId, Guid.NewGuid(), 99.99m, "CNY", DateTime.UtcNow, []);
        var context = new Mock<ConsumeContext<OrderCreatedEvent>>();
        context.Setup(c => c.Message).Returns(evt);

        // Act - 直接 await Consume（不再 fire-and-forget）
        await _sut.Consume(context.Object);

        // Assert - SendAsync 被调用一次，参数正确
        _notificationServiceMock.Verify(
            s => s.SendAsync(It.IsAny<NotificationRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
        capturedRequest.Should().NotBeNull();
        capturedRequest!.UserId.Should().Be(buyerId);
        capturedRequest.TemplateCode.Should().Be("order_created");
        capturedRequest.IdempotencyKey.Should().Be(evt.EventId.ToString());
        capturedRequest.Variables.Should().ContainKey("orderId");
        capturedRequest.Variables.Should().ContainKey("totalAmount");
        capturedRequest.Variables.Should().ContainKey("currency");
    }

    [Fact]
    public async Task Consume_SendAsyncSucceeds_ShouldAckMessage()
    {
        // Arrange - 验证成功后 Consume 正常返回（隐式 ACK，无异常）
        SetupSendAsyncSuccess();
        var evt = new UserRegisteredEvent(Guid.NewGuid(), "testuser", "test@example.com", "13800138000");
        var context = new Mock<ConsumeContext<UserRegisteredEvent>>();
        context.Setup(c => c.Message).Returns(evt);

        // Act - Consume 正常完成意味着 MassTransit 会 ACK 消息
        await _sut.Consume(context.Object);

        // Assert - SendAsync 被调用一次
        _notificationServiceMock.Verify(
            s => s.SendAsync(It.IsAny<NotificationRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
