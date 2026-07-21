using Leno.Infrastructure.Abstractions;
using Leno.SharedKernel.Abstractions;
using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Repositories;
using Leno.SystemAdmin.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Net;
using System.Text;
using System.Text.Json;

namespace Leno.SystemAdmin.Infrastructure.Tests.Services;

/// <summary>
/// 验证 M-10 修复：<see cref="RabbitMqDeadLetterManager.PersistDeadLetterCopyAsync"/> 在并发拉取入库时，
/// 由 OriginalMessageId 唯一索引兜底捕获 <see cref="DbUpdateException"/>，消除 TOCTOU 竞态。
/// </summary>
public sealed class DeadLetterMessageUniqueIndexTests
{

    /// <summary>
    /// 场景：GetByOriginalMessageIdAsync 返回 null（快速路径未命中），但 SaveEntitiesAsync 抛出
    /// 包含 SQL Server 错误码 2601 的 DbUpdateException（模拟并发插入命中唯一索引）。
    /// 验证：FetchAsync 不抛异常，按幂等处理正常返回。
    /// </summary>
    [Fact]
    public async Task FetchAsync_When_SaveEntitiesAsync_Throws_DbUpdateException_With_UniqueConstraint_Violation_Should_Be_Idempotent()
    {
        var responseBody = BuildRabbitMqGetResponse("MSG-UNIQUE-2601");
        var handler = new StubHttpMessageHandler(responseBody, HttpStatusCode.OK);

        var mockRepo = new Mock<IDeadLetterMessageRepository>();
        mockRepo.Setup(r => r.GetByOriginalMessageIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((DeadLetterMessage?)null);

        var mockEventBus = new Mock<IEventBus>();
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        // 模拟 SQL Server 2601 唯一键冲突
        var dbUpdateEx = new DbUpdateException(
            "An error occurred while saving the entity changes. See the inner exception for details.",
            new InvalidOperationException("Cannot insert duplicate key row in object 'dbo.dead_letter_messages' with unique index 'ix_dead_letter_messages_original_message_id'. The duplicate key value is (MSG-UNIQUE-2601). The statement has been terminated. Error 2601"));
        mockUnitOfWork.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()))
                      .ThrowsAsync(dbUpdateEx);

        var manager = CreateRabbitMqManager(mockRepo, mockEventBus, mockUnitOfWork, handler);

        // 不应抛异常，唯一索引冲突按幂等处理
        var exception = await Record.ExceptionAsync(() => manager.FetchAsync("OrderService", 1, 10));
        Assert.Null(exception);

        mockRepo.Verify(r => r.AddAsync(It.IsAny<DeadLetterMessage>(), It.IsAny<CancellationToken>()), Times.Once);
        mockUnitOfWork.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// 场景：GetByOriginalMessageIdAsync 返回 null，SaveEntitiesAsync 抛出包含 PostgreSQL duplicate key 消息的 DbUpdateException。
    /// 验证：FetchAsync 按幂等处理正常返回，兼容 PostgreSQL 数据库。
    /// </summary>
    [Fact]
    public async Task FetchAsync_When_DbUpdateException_Contains_PostgreSql_Duplicate_Key_Should_Be_Idempotent()
    {
        var responseBody = BuildRabbitMqGetResponse("MSG-UNIQUE-PG");
        var handler = new StubHttpMessageHandler(responseBody, HttpStatusCode.OK);

        var mockRepo = new Mock<IDeadLetterMessageRepository>();
        mockRepo.Setup(r => r.GetByOriginalMessageIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((DeadLetterMessage?)null);

        var mockEventBus = new Mock<IEventBus>();
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var dbUpdateEx = new DbUpdateException(
            "An error occurred while saving the entity changes.",
            new PostgresExceptionStub("duplicate key value violates unique constraint \"ix_dead_letter_messages_original_message_id\""));
        mockUnitOfWork.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()))
                      .ThrowsAsync(dbUpdateEx);

        var manager = CreateRabbitMqManager(mockRepo, mockEventBus, mockUnitOfWork, handler);

        var exception = await Record.ExceptionAsync(() => manager.FetchAsync("OrderService", 1, 10));
        Assert.Null(exception);
    }

    /// <summary>
    /// 场景：GetByOriginalMessageIdAsync 返回 null，SaveEntitiesAsync 抛出包含 MySQL Duplicate entry 消息的 DbUpdateException。
    /// 验证：FetchAsync 按幂等处理正常返回，兼容 MySQL 数据库。
    /// </summary>
    [Fact]
    public async Task FetchAsync_When_DbUpdateException_Contains_MySql_Duplicate_Entry_Should_Be_Idempotent()
    {
        var responseBody = BuildRabbitMqGetResponse("MSG-UNIQUE-MYSQL");
        var handler = new StubHttpMessageHandler(responseBody, HttpStatusCode.OK);

        var mockRepo = new Mock<IDeadLetterMessageRepository>();
        mockRepo.Setup(r => r.GetByOriginalMessageIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((DeadLetterMessage?)null);

        var mockEventBus = new Mock<IEventBus>();
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var dbUpdateEx = new DbUpdateException(
            "An error occurred while saving the entity changes.",
            new InvalidOperationException("Duplicate entry 'MSG-UNIQUE-MYSQL' for key 'ix_dead_letter_messages_original_message_id'"));
        mockUnitOfWork.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()))
                      .ThrowsAsync(dbUpdateEx);

        var manager = CreateRabbitMqManager(mockRepo, mockEventBus, mockUnitOfWork, handler);

        var exception = await Record.ExceptionAsync(() => manager.FetchAsync("OrderService", 1, 10));
        Assert.Null(exception);
    }

    /// <summary>
    /// 场景：GetByOriginalMessageIdAsync 返回 null，SaveEntitiesAsync 抛出包含 SQL Server 错误码 2627（违反约束）的 DbUpdateException。
    /// 验证：FetchAsync 按幂等处理正常返回，兼容 2627 错误码。
    /// </summary>
    [Fact]
    public async Task FetchAsync_When_DbUpdateException_Contains_SqlServer_2627_Should_Be_Idempotent()
    {
        var responseBody = BuildRabbitMqGetResponse("MSG-UNIQUE-2627");
        var handler = new StubHttpMessageHandler(responseBody, HttpStatusCode.OK);

        var mockRepo = new Mock<IDeadLetterMessageRepository>();
        mockRepo.Setup(r => r.GetByOriginalMessageIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((DeadLetterMessage?)null);

        var mockEventBus = new Mock<IEventBus>();
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var dbUpdateEx = new DbUpdateException(
            "An error occurred while saving the entity changes.",
            new InvalidOperationException("Violation of UNIQUE KEY constraint 'ix_dead_letter_messages_original_message_id'. Cannot insert duplicate key in object 'dbo.dead_letter_messages'. Error 2627"));
        mockUnitOfWork.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()))
                      .ThrowsAsync(dbUpdateEx);

        var manager = CreateRabbitMqManager(mockRepo, mockEventBus, mockUnitOfWork, handler);

        var exception = await Record.ExceptionAsync(() => manager.FetchAsync("OrderService", 1, 10));
        Assert.Null(exception);
    }

    /// <summary>
    /// 场景：GetByOriginalMessageIdAsync 返回 null，SaveEntitiesAsync 抛出包含索引名称 ix_dead_letter_messages_original_message_id 的 DbUpdateException。
    /// 验证：FetchAsync 按幂等处理正常返回，索引名兜底匹配。
    /// </summary>
    [Fact]
    public async Task FetchAsync_When_DbUpdateException_Contains_Index_Name_Should_Be_Idempotent()
    {
        var responseBody = BuildRabbitMqGetResponse("MSG-UNIQUE-IDX");
        var handler = new StubHttpMessageHandler(responseBody, HttpStatusCode.OK);

        var mockRepo = new Mock<IDeadLetterMessageRepository>();
        mockRepo.Setup(r => r.GetByOriginalMessageIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((DeadLetterMessage?)null);

        var mockEventBus = new Mock<IEventBus>();
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var dbUpdateEx = new DbUpdateException(
            "An error occurred while saving the entity changes.",
            new InvalidOperationException("conflict on ix_dead_letter_messages_original_message_id"));
        mockUnitOfWork.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()))
                      .ThrowsAsync(dbUpdateEx);

        var manager = CreateRabbitMqManager(mockRepo, mockEventBus, mockUnitOfWork, handler);

        var exception = await Record.ExceptionAsync(() => manager.FetchAsync("OrderService", 1, 10));
        Assert.Null(exception);
    }

    /// <summary>
    /// 场景：GetByOriginalMessageIdAsync 返回 null，SaveEntitiesAsync 抛出非唯一约束冲突的 DbUpdateException（如连接失败）。
    /// 验证：FetchAsync 应抛出异常，不应被吞掉，确保仅捕获唯一约束冲突。
    /// </summary>
    [Fact]
    public async Task FetchAsync_When_DbUpdateException_Is_Not_Unique_Constraint_Should_Propagate_Exception()
    {
        var responseBody = BuildRabbitMqGetResponse("MSG-NON-UNIQUE");
        var handler = new StubHttpMessageHandler(responseBody, HttpStatusCode.OK);

        var mockRepo = new Mock<IDeadLetterMessageRepository>();
        mockRepo.Setup(r => r.GetByOriginalMessageIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((DeadLetterMessage?)null);

        var mockEventBus = new Mock<IEventBus>();
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        // 非唯一约束冲突：连接超时
        var dbUpdateEx = new DbUpdateException(
            "An error occurred while saving the entity changes.",
            new InvalidOperationException("Timeout expired. The timeout period elapsed prior to completion of the operation or the server is not responding."));
        mockUnitOfWork.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()))
                      .ThrowsAsync(dbUpdateEx);

        var manager = CreateRabbitMqManager(mockRepo, mockEventBus, mockUnitOfWork, handler);

        // 非唯一约束冲突的异常应传播
        await Assert.ThrowsAsync<DbUpdateException>(() => manager.FetchAsync("OrderService", 1, 10));
    }

    /// <summary>
    /// 场景：GetByOriginalMessageIdAsync 返回已存在的消息（快速路径命中）。
    /// 验证：不再调用 AddAsync 与 SaveEntitiesAsync，直接跳过。
    /// </summary>
    [Fact]
    public async Task FetchAsync_When_Existing_Message_Found_Should_Skip_Insert_And_Not_Call_SaveEntitiesAsync()
    {
        var responseBody = BuildRabbitMqGetResponse("MSG-EXISTING");
        var handler = new StubHttpMessageHandler(responseBody, HttpStatusCode.OK);

        var existingMessage = CreateDeadLetterMessage("MSG-EXISTING");
        var mockRepo = new Mock<IDeadLetterMessageRepository>();
        mockRepo.Setup(r => r.GetByOriginalMessageIdAsync("MSG-EXISTING", It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingMessage);

        var mockEventBus = new Mock<IEventBus>();
        var mockUnitOfWork = new Mock<IUnitOfWork>();

        var manager = CreateRabbitMqManager(mockRepo, mockEventBus, mockUnitOfWork, handler);

        await manager.FetchAsync("OrderService", 1, 10);

        mockRepo.Verify(r => r.AddAsync(It.IsAny<DeadLetterMessage>(), It.IsAny<CancellationToken>()), Times.Never);
        mockUnitOfWork.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// 场景：DbUpdateException 的 InnerException 为 null（无法判定是否唯一约束冲突）。
    /// 验证：异常应传播，不应误判为幂等。
    /// </summary>
    [Fact]
    public async Task FetchAsync_When_DbUpdateException_Has_No_InnerException_Should_Propagate()
    {
        var responseBody = BuildRabbitMqGetResponse("MSG-NO-INNER");
        var handler = new StubHttpMessageHandler(responseBody, HttpStatusCode.OK);

        var mockRepo = new Mock<IDeadLetterMessageRepository>();
        mockRepo.Setup(r => r.GetByOriginalMessageIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((DeadLetterMessage?)null);

        var mockEventBus = new Mock<IEventBus>();
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var dbUpdateEx = new DbUpdateException("An error occurred while saving the entity changes.", (Exception?)null);
        mockUnitOfWork.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()))
                      .ThrowsAsync(dbUpdateEx);

        var manager = CreateRabbitMqManager(mockRepo, mockEventBus, mockUnitOfWork, handler);

        await Assert.ThrowsAsync<DbUpdateException>(() => manager.FetchAsync("OrderService", 1, 10));
    }

    /// <summary>
    /// 场景：多个消息并发入库时，部分消息触发唯一索引冲突，部分成功。
    /// 验证：成功的消息正常入库，冲突的消息被吞掉，FetchAsync 整体不抛异常。
    /// </summary>
    [Fact]
    public async Task FetchAsync_When_Multiple_Messages_With_Mixed_Success_And_Conflict_Should_Complete_Successfully()
    {
        // 构造两条消息的响应
        var responseBody = BuildRabbitMqGetResponseMultiple("MSG-MIXED-OK", "MSG-MIXED-CONFLICT");
        var handler = new StubHttpMessageHandler(responseBody, HttpStatusCode.OK);

        var mockRepo = new Mock<IDeadLetterMessageRepository>();
        // 两条消息的快速路径都返回 null（模拟并发，都未命中）
        mockRepo.Setup(r => r.GetByOriginalMessageIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((DeadLetterMessage?)null);

        var mockEventBus = new Mock<IEventBus>();
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        // 第一次 SaveEntitiesAsync 成功，第二次抛唯一索引冲突
        var dbUpdateEx = new DbUpdateException(
            "An error occurred while saving the entity changes.",
            new InvalidOperationException("Cannot insert duplicate key row. Error 2601"));
        var callCount = 0;
        mockUnitOfWork.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()))
                      .ReturnsAsync(() =>
                      {
                          callCount++;
                          if (callCount == 2)
                          {
                              throw dbUpdateEx;
                          }
                          return true;
                      });

        var manager = CreateRabbitMqManager(mockRepo, mockEventBus, mockUnitOfWork, handler);

        var exception = await Record.ExceptionAsync(() => manager.FetchAsync("OrderService", 1, 10));
        Assert.Null(exception);

        // 两条消息都尝试 AddAsync，但 SaveEntitiesAsync 只成功一次
        mockRepo.Verify(r => r.AddAsync(It.IsAny<DeadLetterMessage>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        mockUnitOfWork.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
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

    private static DeadLetterMessage CreateDeadLetterMessage(string originalMessageId)
    {
        return DeadLetterMessage.Create(
            Guid.NewGuid(),
            originalMessageId,
            "OrderService",
            "Leno.SharedContracts.Events:OrderCreatedEvent",
            "{}",
            "{}",
            "测试死信消息");
    }

    /// <summary>
    /// 构造 RabbitMQ Management API <c>/api/queues/.../get</c> 端点的响应体，包含 1 条死信消息。
    /// </summary>
    private static string BuildRabbitMqGetResponse(string originalMessageId)
    {
        var responseObj = new[]
        {
            new
            {
                payload = "{}",
                routing_key = "Leno.SharedContracts.Events:OrderCreatedEvent",
                payload_encoding = "string",
                properties = new
                {
                    headers = new Dictionary<string, string>
                    {
                        ["message_id"] = originalMessageId
                    }
                }
            }
        };
        return JsonSerializer.Serialize(responseObj);
    }

    /// <summary>
    /// 构造 RabbitMQ Management API <c>/api/queues/.../get</c> 端点的响应体，包含 2 条死信消息。
    /// </summary>
    private static string BuildRabbitMqGetResponseMultiple(string firstMessageId, string secondMessageId)
    {
        var responseObj = new[]
        {
            new
            {
                payload = "{}",
                routing_key = "Leno.SharedContracts.Events:OrderCreatedEvent",
                payload_encoding = "string",
                properties = new
                {
                    headers = new Dictionary<string, string>
                    {
                        ["message_id"] = firstMessageId
                    }
                }
            },
            new
            {
                payload = "{}",
                routing_key = "Leno.SharedContracts.Events:OrderCreatedEvent",
                payload_encoding = "string",
                properties = new
                {
                    headers = new Dictionary<string, string>
                    {
                        ["message_id"] = secondMessageId
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

    /// <summary>
    /// 模拟 PostgreSQL 异常的消息载体（不依赖 Npgsql 包，仅用于携带 duplicate key 消息）。
    /// </summary>
    private sealed class PostgresExceptionStub : Exception
    {
        public PostgresExceptionStub(string message) : base(message) { }
    }
}
