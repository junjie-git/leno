using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using Leno.SystemAdmin.Domain.Repositories;
using Leno.SystemAdmin.Infrastructure.Consumers;
using MassTransit;
using Microsoft.Extensions.Logging;
using Moq;

namespace Leno.SystemAdmin.Infrastructure.Tests;

public class AfterSalesEventConsumerTests
{
    private static readonly Guid SellerId = Guid.NewGuid();
    private static readonly Guid AfterSalesId = Guid.NewGuid();
    private static readonly Guid OrderId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid RefundId = Guid.NewGuid();

    [Fact]
    public async Task Consume_AfterSalesApproved_WithSellerId_ShouldCreateOperationLogAndSave()
    {
        // Arrange
        var mockOperationLogRepo = new Mock<IOperationLogRepository>();
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var consumer = CreateConsumer(mockOperationLogRepo, mockUnitOfWork);

        var evt = new AfterSalesApprovedEvent
        {
            EventId = Guid.NewGuid(),
            AfterSalesId = AfterSalesId,
            OrderId = OrderId,
            UserId = UserId,
            SellerId = SellerId,
            ApprovedAmount = 99.50m,
            Currency = "CNY",
            OccurredAt = DateTime.UtcNow
        };
        var context = CreateContext(evt);

        // Act
        await consumer.Consume(context);

        // Assert
        mockOperationLogRepo.Verify(
            r => r.AddAsync(It.IsAny<Leno.SystemAdmin.Domain.Aggregates.OperationLog>(), It.IsAny<CancellationToken>()),
            Times.Once);
        mockUnitOfWork.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Consume_AfterSalesApproved_WithEmptySellerId_ShouldSkipWithoutSaving()
    {
        // Arrange
        var mockOperationLogRepo = new Mock<IOperationLogRepository>();
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var consumer = CreateConsumer(mockOperationLogRepo, mockUnitOfWork);

        var evt = new AfterSalesApprovedEvent
        {
            EventId = Guid.NewGuid(),
            AfterSalesId = AfterSalesId,
            OrderId = OrderId,
            UserId = UserId,
            SellerId = Guid.Empty, // 缺少操作人上下文
            ApprovedAmount = 99.50m,
            Currency = "CNY",
            OccurredAt = DateTime.UtcNow
        };
        var context = CreateContext(evt);

        // Act
        await consumer.Consume(context);

        // Assert - 幂等性：缺少操作人，跳过日志记录
        mockOperationLogRepo.Verify(
            r => r.AddAsync(It.IsAny<Leno.SystemAdmin.Domain.Aggregates.OperationLog>(), It.IsAny<CancellationToken>()),
            Times.Never);
        mockUnitOfWork.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Consume_RefundCompleted_ShouldSkipWithoutSaving()
    {
        // Arrange
        var mockOperationLogRepo = new Mock<IOperationLogRepository>();
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var consumer = CreateConsumer(mockOperationLogRepo, mockUnitOfWork);

        var evt = new RefundCompletedEvent
        {
            EventId = Guid.NewGuid(),
            OrderId = OrderId,
            UserId = UserId,
            RefundId = RefundId,
            RefundAmount = 50m,
            Currency = "CNY",
            OccurredAt = DateTime.UtcNow
        };
        var context = CreateContext(evt);

        // Act
        await consumer.Consume(context);

        // Assert - 退款完成事件无操作人上下文，仅记录日志跳过
        mockOperationLogRepo.Verify(
            r => r.AddAsync(It.IsAny<Leno.SystemAdmin.Domain.Aggregates.OperationLog>(), It.IsAny<CancellationToken>()),
            Times.Never);
        mockUnitOfWork.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private static AfterSalesEventConsumer CreateConsumer(
        Mock<IOperationLogRepository> mockOperationLogRepo, Mock<IUnitOfWork> mockUnitOfWork)
    {
        var mockLogger = new Mock<ILogger<AfterSalesEventConsumer>>();
        return new AfterSalesEventConsumer(
            mockOperationLogRepo.Object, mockUnitOfWork.Object, mockLogger.Object);
    }

    private static ConsumeContext<T> CreateContext<T>(T message) where T : class
    {
        var mockContext = new Mock<ConsumeContext<T>>();
        mockContext.Setup(c => c.Message).Returns(message);
        mockContext.Setup(c => c.CancellationToken).Returns(CancellationToken.None);
        return mockContext.Object;
    }
}
