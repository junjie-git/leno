using Leno.Notification.Domain.Services;
using Leno.Notification.Domain.ValueObjects;
using Leno.Notification.Infrastructure.Consumers;
using Leno.SharedContracts.Events;
using MassTransit;
using Microsoft.Extensions.Logging;
using Moq;

namespace Leno.Notification.Application.Tests;

/// <summary>
/// P0-4 修复验证：OrderCancelledEvent 契约当前未携带 BuyerId，
/// Consumer 不应以 Guid.Empty 调用 SendAsync 触发 NotificationRecord.Create
/// 的 NOTIFICATION_USER_EMPTY 异常。
/// 修复方案：使用事件中的 SellerId 作为通知接收人 fallback；
/// 若 SellerId 也为 Guid.Empty（如会员订阅订单），记录警告并跳过发送。
/// </summary>
public class OrderEventConsumerCancelledTests
{
    private readonly Mock<INotificationService> _notificationServiceMock;
    private readonly Mock<ILogger<OrderEventConsumer>> _loggerMock;
    private readonly OrderEventConsumer _sut;

    public OrderEventConsumerCancelledTests()
    {
        _notificationServiceMock = new Mock<INotificationService>(MockBehavior.Strict);
        _loggerMock = new Mock<ILogger<OrderEventConsumer>>();
        _sut = new OrderEventConsumer(_notificationServiceMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task Consume_OrderCancelledEvent_WithSellerId_ShouldUseSellerIdAsUserIdNotGuidEmpty()
    {
        // Arrange — OrderCancelledEvent 契约只有 SellerId 无 BuyerId，
        // 修复前：UserId=Guid.Empty 调用 SendAsync 触发 NOTIFICATION_USER_EMPTY 异常；
        // 修复后：使用 SellerId 作为通知接收人 fallback，不再使用 Guid.Empty。
        NotificationRequest? capturedRequest = null;
        _notificationServiceMock
            .Setup(s => s.SendAsync(It.IsAny<NotificationRequest>(), It.IsAny<CancellationToken>()))
            .Callback<NotificationRequest, CancellationToken>((r, _) => capturedRequest = r)
            .ReturnsAsync(new NotificationSendResult { Succeeded = true });

        var sellerId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var evt = new OrderCancelledEvent(
            orderId,
            sellerId,
            "user-cancelled",
            DateTime.UtcNow,
            "buyer",
            0);
        var context = new Mock<ConsumeContext<OrderCancelledEvent>>();
        context.Setup(c => c.Message).Returns(evt);
        context.Setup(c => c.CancellationToken).Returns(CancellationToken.None);

        // Act
        await _sut.Consume(context.Object);

        // Assert — 修复后：UserId 应为 SellerId，不再是 Guid.Empty
        capturedRequest.Should().NotBeNull();
        capturedRequest!.UserId.Should().Be(sellerId);
        capturedRequest.UserId.Should().NotBe(Guid.Empty);
        capturedRequest.TemplateCode.Should().Be("order_cancelled");
        capturedRequest.IdempotencyKey.Should().Be(evt.EventId.ToString());
        capturedRequest.Variables["orderId"].Should().Be(orderId.ToString());
        capturedRequest.Variables["cancelReason"].Should().Be("user-cancelled");
        capturedRequest.Variables["cancelledBy"].Should().Be("buyer");
        _notificationServiceMock.Verify(
            s => s.SendAsync(It.IsAny<NotificationRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Consume_OrderCancelledEvent_WithEmptySellerId_ShouldSkipSendAndLogWarning()
    {
        // Arrange — 会员订阅订单的 SellerId 可能为 Guid.Empty，
        // 此时不应调用 SendAsync 触发聚合根异常，应记录警告并跳过发送。
        _notificationServiceMock
            .Setup(s => s.SendAsync(It.IsAny<NotificationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationSendResult { Succeeded = true });

        var evt = new OrderCancelledEvent(
            Guid.NewGuid(),
            Guid.Empty,
            "membership-cancelled",
            DateTime.UtcNow,
            "system",
            0);
        var context = new Mock<ConsumeContext<OrderCancelledEvent>>();
        context.Setup(c => c.Message).Returns(evt);
        context.Setup(c => c.CancellationToken).Returns(CancellationToken.None);

        // Act
        await _sut.Consume(context.Object);

        // Assert — 不应调用 SendAsync，避免 Guid.Empty 触发聚合根异常
        _notificationServiceMock.Verify(
            s => s.SendAsync(It.IsAny<NotificationRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Consume_OrderCancelledEvent_NullContext_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        var act = () => _sut.Consume((ConsumeContext<OrderCancelledEvent>)null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
