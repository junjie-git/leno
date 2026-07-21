using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Repositories;
using Leno.SystemAdmin.Infrastructure.Consumers;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Leno.SystemAdmin.Infrastructure.Tests.Consumers;

/// <summary>
/// H-04 验证 AuditLogConsumer 在并发插入 AuditLogEntry 时，
/// 通过捕获 DbUpdateException 判定为唯一约束冲突后正常返回，消除 TOCTOU 竞态。
/// 非唯一约束冲突的 DbUpdateException 仍需重抛以触发 MassTransit 重试。
/// </summary>
public sealed class AuditLogConsumerIdempotencyTests
{
    private readonly Mock<IAuditLogEntryRepository> _entryRepoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly AuditLogConsumer _consumer;

    public AuditLogConsumerIdempotencyTests()
    {
        _consumer = new AuditLogConsumer(
            _entryRepoMock.Object,
            _unitOfWorkMock.Object,
            NullLogger<AuditLogConsumer>.Instance);
    }

    [Fact]
    public async Task Consume_OrderCreated_Should_Swallow_DbUpdateException_For_Duplicate_EventId()
    {
        var evt = new OrderCreatedEvent
        {
            EventId = Guid.NewGuid(),
            OrderId = Guid.NewGuid(),
            BuyerId = Guid.NewGuid(),
            SellerId = Guid.NewGuid(),
            TotalAmount = 100m,
            Currency = "CNY",
            OccurredAt = DateTime.UtcNow
        };
        var context = CreateContext(evt);

        _entryRepoMock.Setup(r => r.GetByEventIdAsync(evt.EventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AuditLogEntry?)null);
        _unitOfWorkMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException(
                "唯一索引冲突",
                new InvalidOperationException("Violation of UNIQUE KEY constraint 'ix_audit_log_entries_event_id'")));

        // 不应抛异常——重复 EventId 视为已处理
        await _consumer.Consume(context);
    }

    [Fact]
    public async Task Consume_OrderCreated_Should_Rethrow_Non_Duplicate_DbUpdateException()
    {
        var evt = new OrderCreatedEvent
        {
            EventId = Guid.NewGuid(),
            OrderId = Guid.NewGuid(),
            BuyerId = Guid.NewGuid(),
            SellerId = Guid.NewGuid(),
            TotalAmount = 200m,
            Currency = "CNY",
            OccurredAt = DateTime.UtcNow
        };
        var context = CreateContext(evt);

        _entryRepoMock.Setup(r => r.GetByEventIdAsync(evt.EventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AuditLogEntry?)null);
        _unitOfWorkMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("连接超时", new TimeoutException("timeout")));

        await Assert.ThrowsAsync<DbUpdateException>(() => _consumer.Consume(context));
    }

    private static ConsumeContext<T> CreateContext<T>(T message) where T : class
    {
        var mockContext = new Mock<ConsumeContext<T>>();
        mockContext.Setup(c => c.Message).Returns(message);
        mockContext.Setup(c => c.CancellationToken).Returns(CancellationToken.None);
        return mockContext.Object;
    }
}
