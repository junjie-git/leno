using Leno.Infrastructure.Abstractions;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Repositories;
using Leno.SystemAdmin.Domain.ValueObjects;
using Leno.SystemAdmin.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using System.Net;
using System.Text;
using System.Text.Json;

namespace Leno.SystemAdmin.Infrastructure.Tests;

/// <summary>
/// <see cref="RabbitMqDeadLetterManager"/> 单元测试。
/// 验证：
/// - RepublishAsync 真正通过 <see cref="IEventBus"/> 重投原始集成事件（与 <see cref="DeadLetterQueueManager"/> 行为一致）。
/// - FetchAsync 采用 ack_requeue_true 拉取 + 入库副本策略，本地入库失败时抛异常但消息不丢失（因消息回队）。
/// 测试风格参考 <see cref="AuditLogConsumerTests"/>（Moq + FluentAssertions + xUnit）。
/// </summary>
public class RabbitMqDeadLetterManagerTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task RepublishAsync_WithValidMessage_ShouldPublishEventAndMarkRetried()
    {
        // Arrange
        var message = DeadLetterQueueManagerTests.CreatePendingMessageWithOrderCreatedEvent();
        var mockRepo = new Mock<IDeadLetterMessageRepository>();
        mockRepo.Setup(r => r.GetByIdAsync(message.MessageId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(message);

        var mockEventBus = new Mock<IEventBus>();
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var manager = CreateManager(mockRepo, mockEventBus, mockUnitOfWork);

        // Act
        await manager.RepublishAsync(message.MessageId);

        // Assert - 真正重投：IEventBus.PublishAsync 被调用一次（与 DeadLetterQueueManager 行为一致）
        mockEventBus.Verify(
            b => b.PublishAsync(It.IsAny<IIntegrationEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);

        mockRepo.Verify(
            r => r.UpdateAsync(It.Is<DeadLetterMessage>(m => m.Status == DeadLetterStatus.Retried), It.IsAny<CancellationToken>()),
            Times.Once);
        mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
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
        await act.Should().ThrowAsync<InvalidOperationException>();
        mockEventBus.Verify(
            b => b.PublishAsync(It.IsAny<IIntegrationEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RepublishAsync_WithAlreadyRetriedMessage_ShouldSkipPublishForIdempotency()
    {
        // Arrange
        var message = DeadLetterQueueManagerTests.CreatePendingMessageWithOrderCreatedEvent();
        message.Retry("previous-operator");

        var mockRepo = new Mock<IDeadLetterMessageRepository>();
        mockRepo.Setup(r => r.GetByIdAsync(message.MessageId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(message);

        var mockEventBus = new Mock<IEventBus>();
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var manager = CreateManager(mockRepo, mockEventBus, mockUnitOfWork);

        // Act
        await manager.RepublishAsync(message.MessageId);

        // Assert - 幂等：已重投不再发布
        mockEventBus.Verify(
            b => b.PublishAsync(It.IsAny<IIntegrationEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task FetchAsync_WithNewMessage_ShouldPersistCopyAndReturnMessages()
    {
        // Arrange - RabbitMQ Management API 返回 1 条死信消息
        var responseBody = BuildRabbitMqGetResponse();
        var handler = new StubHttpMessageHandler(responseBody, HttpStatusCode.OK);

        var mockRepo = new Mock<IDeadLetterMessageRepository>();
        mockRepo.Setup(r => r.GetByOriginalMessageIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((DeadLetterMessage?)null);

        var mockEventBus = new Mock<IEventBus>();
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var manager = CreateManager(mockRepo, mockEventBus, mockUnitOfWork, handler);

        // Act
        var result = await manager.FetchAsync("OrderService", 1, 10);

        // Assert - 入库副本被创建
        result.Should().HaveCount(1);
        mockRepo.Verify(
            r => r.AddAsync(It.IsAny<DeadLetterMessage>(), It.IsAny<CancellationToken>()),
            Times.Once);
        mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task FetchAsync_WhenRepositoryAddThrows_ShouldPropagateAndKeepMessageInDlq()
    {
        // Arrange - 拉取策略：ack_requeue_true（消息回队，不删除）+ 入库副本。
        // 入库失败时抛异常让调用方感知；因 ack_requeue_true，消息仍保留在 DLQ，下次拉取仍能拿到——不丢失。
        var responseBody = BuildRabbitMqGetResponse();
        var handler = new StubHttpMessageHandler(responseBody, HttpStatusCode.OK);

        var mockRepo = new Mock<IDeadLetterMessageRepository>();
        mockRepo.Setup(r => r.GetByOriginalMessageIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((DeadLetterMessage?)null);
        mockRepo.Setup(r => r.AddAsync(It.IsAny<DeadLetterMessage>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("数据库连接失败"));

        var mockEventBus = new Mock<IEventBus>();
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var manager = CreateManager(mockRepo, mockEventBus, mockUnitOfWork, handler);

        // Act
        var act = async () => await manager.FetchAsync("OrderService", 1, 10);

        // Assert - 入库失败抛异常；消息因 ack_requeue_true 已回 DLQ，不丢失（下次拉取仍能拿到）
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*数据库连接失败*");
        mockRepo.Verify(
            r => r.AddAsync(It.IsAny<DeadLetterMessage>(), It.IsAny<CancellationToken>()),
            Times.Once);
        // SaveChanges 不应被调用（AddAsync 已抛异常）
        mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task FetchAsync_WhenMessageAlreadyPersisted_ShouldSkipDuplicateInsert()
    {
        // Arrange - 同一 OriginalMessageId 已入库，再次拉取应跳过重复入库
        var responseBody = BuildRabbitMqGetResponse();
        var handler = new StubHttpMessageHandler(responseBody, HttpStatusCode.OK);

        var existing = DeadLetterQueueManagerTests.CreatePendingMessageWithOrderCreatedEvent();
        var mockRepo = new Mock<IDeadLetterMessageRepository>();
        mockRepo.Setup(r => r.GetByOriginalMessageIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(existing);

        var mockEventBus = new Mock<IEventBus>();
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var manager = CreateManager(mockRepo, mockEventBus, mockUnitOfWork, handler);

        // Act
        var result = await manager.FetchAsync("OrderService", 1, 10);

        // Assert - 已存在则跳过 AddAsync，但仍返回拉取到的消息
        result.Should().HaveCount(1);
        mockRepo.Verify(
            r => r.AddAsync(It.IsAny<DeadLetterMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);
        mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private static RabbitMqDeadLetterManager CreateManager(
        Mock<IDeadLetterMessageRepository> mockRepo,
        Mock<IEventBus> mockEventBus,
        Mock<IUnitOfWork> mockUnitOfWork,
        HttpMessageHandler? handler = null)
    {
        var httpClient = new HttpClient(handler ?? new StubHttpMessageHandler("[]", HttpStatusCode.OK));
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RabbitMQ:ManagementApi:Host"] = "http://localhost:15672",
                ["RabbitMQ:ManagementApi:Username"] = "guest",
                ["RabbitMQ:ManagementApi:Password"] = "guest",
                ["RabbitMQ:ManagementApi:VHost"] = "%2F"
            })
            .Build();
        var mockLogger = new Mock<ILogger<RabbitMqDeadLetterManager>>();
        return new RabbitMqDeadLetterManager(
            httpClient, configuration, mockRepo.Object, mockEventBus.Object, mockUnitOfWork.Object, mockLogger.Object);
    }

    /// <summary>
    /// 构造 RabbitMQ Management API <c>/api/queues/.../get</c> 端点的响应体，包含 1 条 OrderCreatedEvent 死信消息。
    /// payload 为 JSON 字符串（带引号转义），模拟 Management API 实际返回格式。
    /// </summary>
    private static string BuildRabbitMqGetResponse()
    {
        var evt = new OrderCreatedEvent
        {
            EventId = Guid.Parse("00000000-0000-0000-0000-000000000010"),
            OrderId = Guid.Parse("00000000-0000-0000-0000-000000000020"),
            BuyerId = Guid.Parse("00000000-0000-0000-0000-000000000030"),
            SellerId = Guid.Parse("00000000-0000-0000-0000-000000000040"),
            TotalAmount = 199.99m,
            Currency = "CNY",
            CreatedAt = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc),
            SourceCartItemIds = Array.Empty<Guid>()
        };
        var payloadJson = JsonSerializer.Serialize(evt, SerializerOptions);

        var responseObj = new[]
        {
            new
            {
                payload = payloadJson,
                routing_key = "Leno.SharedContracts.Events:OrderCreatedEvent",
                payload_encoding = "string",
                properties = new
                {
                    headers = new Dictionary<string, string>
                    {
                        ["message-type"] = "urn:message:Leno.SharedContracts.Events:OrderCreatedEvent"
                    }
                }
            }
        };
        return JsonSerializer.Serialize(responseObj);
    }

    /// <summary>
    /// 简单的 <see cref="HttpMessageHandler"/> 桩，对所有请求返回固定响应。
    /// </summary>
    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly string _responseBody;
        private readonly HttpStatusCode _statusCode;

        public StubHttpMessageHandler(string responseBody, HttpStatusCode statusCode)
        {
            _responseBody = responseBody;
            _statusCode = statusCode;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_responseBody, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }
}
