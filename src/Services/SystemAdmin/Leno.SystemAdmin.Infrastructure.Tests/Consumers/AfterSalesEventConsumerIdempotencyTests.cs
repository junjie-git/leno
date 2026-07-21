using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Repositories;
using Leno.SystemAdmin.Infrastructure.Consumers;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Leno.SystemAdmin.Infrastructure.Tests.Consumers;

/// <summary>
/// H-07 验证 AfterSalesEventConsumer 的幂等去重：
/// - 按 EventId 检查已存在的 OperationLog 时跳过写入
/// - 并发插入时通过捕获 DbUpdateException 判定唯一约束冲突后正常返回
/// - 未处理时正常写入 OperationLog
/// 测试风格参考 <see cref="AuditLogConsumerIdempotencyTests"/>（Mock{ConsumeContext{T}} 模式）。
/// </summary>
public sealed class AfterSalesEventConsumerIdempotencyTests
{
    private readonly Mock<IOperationLogRepository> _repoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly AfterSalesEventConsumer _consumer;

    public AfterSalesEventConsumerIdempotencyTests()
    {
        _consumer = new AfterSalesEventConsumer(
            _repoMock.Object,
            _unitOfWorkMock.Object,
            NullLogger<AfterSalesEventConsumer>.Instance);
    }

    [Fact]
    public async Task Consume_AfterSalesApproved_Should_Skip_When_EventId_Already_Processed()
    {
        var evt = new AfterSalesApprovedEvent
        {
            EventId = Guid.NewGuid(),
            AfterSalesId = Guid.NewGuid(),
            OrderId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            SellerId = Guid.NewGuid(),
            ApprovedAmount = 100m,
            Currency = "CNY",
            Type = 0,
            OccurredAt = DateTime.UtcNow
        };
        var context = CreateContext(evt);

        var existingLog = OperationLog.Create(
            Guid.NewGuid(), evt.SellerId, "Approve", "AfterSales",
            "已存在", null, null, null, evt.OccurredAt, evt.EventId);
        _repoMock.Setup(r => r.GetByEventIdAsync(evt.EventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingLog);

        await _consumer.Consume(context);

        _repoMock.Verify(r => r.AddAsync(It.IsAny<OperationLog>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Consume_AfterSalesApproved_Should_Write_OperationLog_When_Not_Processed()
    {
        var evt = new AfterSalesApprovedEvent
        {
            EventId = Guid.NewGuid(),
            AfterSalesId = Guid.NewGuid(),
            OrderId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            SellerId = Guid.NewGuid(),
            ApprovedAmount = 200m,
            Currency = "CNY",
            Type = 0,
            OccurredAt = DateTime.UtcNow
        };
        var context = CreateContext(evt);

        _repoMock.Setup(r => r.GetByEventIdAsync(evt.EventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((OperationLog?)null);
        _unitOfWorkMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await _consumer.Consume(context);

        _repoMock.Verify(r => r.AddAsync(It.IsAny<OperationLog>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Consume_AfterSalesApproved_Should_Swallow_DbUpdateException_For_Duplicate_EventId()
    {
        var evt = new AfterSalesApprovedEvent
        {
            EventId = Guid.NewGuid(),
            AfterSalesId = Guid.NewGuid(),
            OrderId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            SellerId = Guid.NewGuid(),
            ApprovedAmount = 300m,
            Currency = "CNY",
            Type = 0,
            OccurredAt = DateTime.UtcNow
        };
        var context = CreateContext(evt);

        _repoMock.Setup(r => r.GetByEventIdAsync(evt.EventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((OperationLog?)null);
        _unitOfWorkMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException(
                "唯一索引冲突",
                new InvalidOperationException("Violation of UNIQUE KEY constraint 'ix_operation_logs_event_id'")));

        // 不应抛异常——重复 EventId 视为已处理
        await _consumer.Consume(context);
    }

    private static ConsumeContext<T> CreateContext<T>(T message) where T : class
    {
        var mockContext = new Mock<ConsumeContext<T>>();
        mockContext.Setup(c => c.Message).Returns(message);
        mockContext.Setup(c => c.CancellationToken).Returns(CancellationToken.None);
        return mockContext.Object;
    }
}
