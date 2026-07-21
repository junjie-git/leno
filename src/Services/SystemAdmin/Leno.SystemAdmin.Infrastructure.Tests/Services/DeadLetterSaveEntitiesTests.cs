using Leno.Infrastructure.Abstractions;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Repositories;
using Leno.SystemAdmin.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Net;
using System.Text;
using System.Text.Json;

namespace Leno.SystemAdmin.Infrastructure.Tests.Services;

/// <summary>
/// 验证死信管理器使用 <see cref="IUnitOfWork.SaveEntitiesAsync"/> 而非 <see cref="IUnitOfWork.SaveChangesAsync"/>，
/// 确保领域事件（如 DeadLetterRetriedEvent）经发件箱投递，不被丢弃。
/// 覆盖三处修复点：
/// - <see cref="DeadLetterQueueManager.RepublishAsync"/> 的状态持久化
/// - <see cref="RabbitMqDeadLetterManager.RepublishAsync"/> 的状态持久化
/// - <see cref="RabbitMqDeadLetterManager"/> 入库副本持久化（FetchAsync 路径）
/// </summary>
public sealed class DeadLetterSaveEntitiesTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task DeadLetterQueueManager_RepublishAsync_Should_Call_SaveEntitiesAsync_Not_SaveChangesAsync()
    {
        var message = CreatePendingMessageWithOrderCreatedEvent();
        var mockRepo = new Mock<IDeadLetterMessageRepository>();
        mockRepo.Setup(r => r.GetByIdAsync(message.MessageId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(message);

        var mockEventBus = new Mock<IEventBus>();
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var manager = new DeadLetterQueueManager(
            mockRepo.Object, mockEventBus.Object, mockUnitOfWork.Object,
            NullLogger<DeadLetterQueueManager>.Instance);

        await manager.RepublishAsync(message.MessageId, CancellationToken.None);

        mockUnitOfWork.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
        mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RabbitMqDeadLetterManager_RepublishAsync_Should_Call_SaveEntitiesAsync_Not_SaveChangesAsync()
    {
        var message = CreatePendingMessageWithOrderCreatedEvent();
        var mockRepo = new Mock<IDeadLetterMessageRepository>();
        mockRepo.Setup(r => r.GetByIdAsync(message.MessageId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(message);

        var mockEventBus = new Mock<IEventBus>();
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var manager = CreateRabbitMqManager(mockRepo, mockEventBus, mockUnitOfWork);

        await manager.RepublishAsync(message.MessageId, CancellationToken.None);

        mockUnitOfWork.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
        mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RabbitMqDeadLetterManager_FetchAsync_Should_Call_SaveEntitiesAsync_Not_SaveChangesAsync()
    {
        var responseBody = BuildRabbitMqGetResponse();
        var handler = new StubHttpMessageHandler(responseBody, HttpStatusCode.OK);

        var mockRepo = new Mock<IDeadLetterMessageRepository>();
        mockRepo.Setup(r => r.GetByOriginalMessageIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((DeadLetterMessage?)null);

        var mockEventBus = new Mock<IEventBus>();
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var manager = CreateRabbitMqManager(mockRepo, mockEventBus, mockUnitOfWork, handler);

        await manager.FetchAsync("OrderService", 1, 10);

        mockUnitOfWork.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
        mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private static RabbitMqDeadLetterManager CreateRabbitMqManager(
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
        return new RabbitMqDeadLetterManager(
            httpClient, configuration, mockRepo.Object, mockEventBus.Object,
            mockUnitOfWork.Object, NullLogger<RabbitMqDeadLetterManager>.Instance);
    }

    /// <summary>
    /// 构造一条 Pending 态死信消息，Payload 为 OrderCreatedEvent 的 JSON，Headers 含 MassTransit message-type URN。
    /// </summary>
    internal static DeadLetterMessage CreatePendingMessageWithOrderCreatedEvent()
    {
        var evt = new OrderCreatedEvent
        {
            EventId = Guid.Parse("00000000-0000-0000-0000-000000000099"),
            OrderId = Guid.Parse("00000000-0000-0000-0000-000000000098"),
            BuyerId = Guid.Parse("00000000-0000-0000-0000-000000000097"),
            SellerId = Guid.Parse("00000000-0000-0000-0000-000000000096"),
            TotalAmount = 88.88m,
            Currency = "CNY",
            CreatedAt = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            SourceCartItemIds = Array.Empty<Guid>()
        };

        var payload = JsonSerializer.Serialize(evt, SerializerOptions);
        var headers = "{\"message-type\":\"urn:message:Leno.SharedContracts.Events:OrderCreatedEvent\"}";

        return DeadLetterMessage.Create(
            Guid.NewGuid(),
            "MSG-SAVE-ENTITIES-001",
            "OrderService",
            "Leno.SharedContracts.Events:OrderCreatedEvent",
            payload,
            headers,
            "消费失败后进入死信");
    }

    /// <summary>
    /// 构造 RabbitMQ Management API <c>/api/queues/.../get</c> 端点的响应体，包含 1 条 OrderCreatedEvent 死信消息。
    /// </summary>
    private static string BuildRabbitMqGetResponse()
    {
        var evt = new OrderCreatedEvent
        {
            EventId = Guid.Parse("00000000-0000-0000-0000-000000000050"),
            OrderId = Guid.Parse("00000000-0000-0000-0000-000000000051"),
            BuyerId = Guid.Parse("00000000-0000-0000-0000-000000000052"),
            SellerId = Guid.Parse("00000000-0000-0000-0000-000000000053"),
            TotalAmount = 199.99m,
            Currency = "CNY",
            CreatedAt = new DateTime(2026, 7, 2, 3, 4, 5, DateTimeKind.Utc),
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
