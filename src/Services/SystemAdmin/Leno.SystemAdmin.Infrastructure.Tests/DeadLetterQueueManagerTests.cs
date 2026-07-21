using Leno.Infrastructure.Abstractions;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Repositories;
using Leno.SystemAdmin.Domain.ValueObjects;
using Leno.SystemAdmin.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;

namespace Leno.SystemAdmin.Infrastructure.Tests;

/// <summary>
/// <see cref="DeadLetterQueueManager"/> 单元测试。
/// 验证 RepublishAsync 真正通过 <see cref="IEventBus"/> 重投原始集成事件，并正确处理幂等与不存在场景。
/// 测试风格参考 <see cref="AuditLogConsumerTests"/>（Moq + FluentAssertions + xUnit）。
/// </summary>
public class DeadLetterQueueManagerTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task RepublishAsync_WithValidMessage_ShouldPublishEventAndMarkRetried()
    {
        // Arrange
        var message = CreatePendingMessageWithOrderCreatedEvent();
        var mockRepo = new Mock<IDeadLetterMessageRepository>();
        mockRepo.Setup(r => r.GetByIdAsync(message.MessageId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(message);

        var mockEventBus = new Mock<IEventBus>();
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var manager = CreateManager(mockRepo, mockEventBus, mockUnitOfWork);

        // Act
        await manager.RepublishAsync(message.MessageId);

        // Assert - 真正重投：IEventBus.PublishAsync 被调用一次
        mockEventBus.Verify(
            b => b.PublishAsync(It.IsAny<IIntegrationEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);

        // 状态更新：消息标记为 Retried 并持久化
        mockRepo.Verify(
            r => r.UpdateAsync(It.Is<DeadLetterMessage>(m => m.Status == DeadLetterStatus.Retried), It.IsAny<CancellationToken>()),
            Times.Once);
        mockUnitOfWork.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RepublishAsync_WithNonExistentMessage_ShouldThrowAndSkipPublish()
    {
        // Arrange
        var messageId = Guid.NewGuid();
        var mockRepo = new Mock<IDeadLetterMessageRepository>();
        mockRepo.Setup(r => r.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((DeadLetterMessage?)null);

        var mockEventBus = new Mock<IEventBus>();
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var manager = CreateManager(mockRepo, mockEventBus, mockUnitOfWork);

        // Act
        var act = async () => await manager.RepublishAsync(messageId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*{messageId}*");
        mockEventBus.Verify(
            b => b.PublishAsync(It.IsAny<IIntegrationEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
        mockRepo.Verify(r => r.UpdateAsync(It.IsAny<DeadLetterMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RepublishAsync_WithAlreadyRetriedMessage_ShouldSkipPublishForIdempotency()
    {
        // Arrange - 已重投的消息，再次重投应幂等跳过
        var message = CreatePendingMessageWithOrderCreatedEvent();
        message.Retry("previous-operator");

        var mockRepo = new Mock<IDeadLetterMessageRepository>();
        mockRepo.Setup(r => r.GetByIdAsync(message.MessageId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(message);

        var mockEventBus = new Mock<IEventBus>();
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var manager = CreateManager(mockRepo, mockEventBus, mockUnitOfWork);

        // Act
        await manager.RepublishAsync(message.MessageId);

        // Assert - 幂等：已重投不再发布，也不更新仓储
        mockEventBus.Verify(
            b => b.PublishAsync(It.IsAny<IIntegrationEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
        mockRepo.Verify(r => r.UpdateAsync(It.IsAny<DeadLetterMessage>(), It.IsAny<CancellationToken>()), Times.Never);
        mockUnitOfWork.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RepublishAsync_WithDiscardedMessage_ShouldThrowAndSkipPublish()
    {
        // Arrange - 已丢弃的消息不可重投
        var message = CreatePendingMessageWithOrderCreatedEvent();
        message.Discard("operator-1", "无效消息");

        var mockRepo = new Mock<IDeadLetterMessageRepository>();
        mockRepo.Setup(r => r.GetByIdAsync(message.MessageId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(message);

        var mockEventBus = new Mock<IEventBus>();
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var manager = CreateManager(mockRepo, mockEventBus, mockUnitOfWork);

        // Act
        var act = async () => await manager.RepublishAsync(message.MessageId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        mockEventBus.Verify(
            b => b.PublishAsync(It.IsAny<IIntegrationEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static DeadLetterQueueManager CreateManager(
        Mock<IDeadLetterMessageRepository> mockRepo,
        Mock<IEventBus> mockEventBus,
        Mock<IUnitOfWork> mockUnitOfWork)
    {
        var mockLogger = new Mock<ILogger<DeadLetterQueueManager>>();
        return new DeadLetterQueueManager(
            mockRepo.Object, mockEventBus.Object, mockUnitOfWork.Object, mockLogger.Object);
    }

    /// <summary>
    /// 构造一条 Pending 态死信消息，Payload 为 OrderCreatedEvent 的 JSON，Headers 含 MassTransit message-type URN。
    /// </summary>
    internal static DeadLetterMessage CreatePendingMessageWithOrderCreatedEvent()
    {
        var evt = new OrderCreatedEvent
        {
            EventId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            OrderId = Guid.Parse("00000000-0000-0000-0000-000000000002"),
            BuyerId = Guid.Parse("00000000-0000-0000-0000-000000000003"),
            SellerId = Guid.Parse("00000000-0000-0000-0000-000000000004"),
            TotalAmount = 100m,
            Currency = "CNY",
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            SourceCartItemIds = Array.Empty<Guid>()
        };

        var payload = JsonSerializer.Serialize(evt, SerializerOptions);
        var headers = "{\"message-type\":\"urn:message:Leno.SharedContracts.Events:OrderCreatedEvent\"}";

        return DeadLetterMessage.Create(
            Guid.NewGuid(),
            "MSG-ORDER-001",
            "OrderService",
            "Leno.SharedContracts.Events:OrderCreatedEvent",
            payload,
            headers,
            "消费失败 5 次后进入死信");
    }
}
