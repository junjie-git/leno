using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Repositories;
using Leno.SystemAdmin.Infrastructure.Consumers;
using MassTransit;
using Microsoft.Extensions.Logging;
using Moq;

namespace Leno.SystemAdmin.Infrastructure.Tests;

public class AuditLogConsumerTests
{
    private static readonly Guid BuyerId = Guid.NewGuid();
    private static readonly Guid OrderId = Guid.NewGuid();
    private static readonly Guid SellerId = Guid.NewGuid();
    private static readonly Guid AfterSalesId = Guid.NewGuid();

    [Fact]
    public async Task Consume_OrderCreated_WhenEntryNotExists_ShouldCreateEntryAndSave()
    {
        // Arrange
        var mockAuditLogEntryRepo = new Mock<IAuditLogEntryRepository>();
        mockAuditLogEntryRepo.Setup(r => r.GetByEventIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AuditLogEntry?)null);

        var mockAuditLogRepo = new Mock<IAuditLogRepository>();
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var consumer = CreateConsumer(mockAuditLogRepo, mockAuditLogEntryRepo, mockUnitOfWork);

        var evt = new OrderCreatedEvent
        {
            EventId = Guid.NewGuid(),
            OrderId = OrderId,
            BuyerId = BuyerId,
            SellerId = SellerId,
            TotalAmount = 199.99m,
            Currency = "CNY",
            OccurredAt = DateTime.UtcNow
        };
        var context = CreateContext(evt);

        // Act
        await consumer.Consume(context);

        // Assert
        mockAuditLogEntryRepo.Verify(
            r => r.AddAsync(It.IsAny<AuditLogEntry>(), It.IsAny<CancellationToken>()), Times.Once);
        mockUnitOfWork.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Consume_OrderCreated_WhenEntryAlreadyExists_ShouldSkip()
    {
        // Arrange - 幂等性：EventId 已存在，跳过重复消费
        var existingEntry = AuditLogEntry.Create(
            Guid.NewGuid(), Guid.NewGuid(), "OrderCreatedEvent", OrderId, "Order",
            "OrderCreated", BuyerId, null, "订单创建", DateTime.UtcNow, null);

        var mockAuditLogEntryRepo = new Mock<IAuditLogEntryRepository>();
        mockAuditLogEntryRepo.Setup(r => r.GetByEventIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingEntry);

        var mockAuditLogRepo = new Mock<IAuditLogRepository>();
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var consumer = CreateConsumer(mockAuditLogRepo, mockAuditLogEntryRepo, mockUnitOfWork);

        var evt = new OrderCreatedEvent
        {
            EventId = Guid.NewGuid(),
            OrderId = OrderId,
            BuyerId = BuyerId,
            SellerId = SellerId,
            TotalAmount = 199.99m,
            Currency = "CNY",
            OccurredAt = DateTime.UtcNow
        };
        var context = CreateContext(evt);

        // Act
        await consumer.Consume(context);

        // Assert - 幂等去重：已存在条目，跳过创建与保存
        mockAuditLogEntryRepo.Verify(
            r => r.AddAsync(It.IsAny<AuditLogEntry>(), It.IsAny<CancellationToken>()), Times.Never);
        mockUnitOfWork.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Consume_OrderCreated_WhenRepositoryThrows_ShouldPropagateException()
    {
        // Arrange
        var mockAuditLogEntryRepo = new Mock<IAuditLogEntryRepository>();
        mockAuditLogEntryRepo.Setup(r => r.GetByEventIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("数据库连接失败"));

        var mockAuditLogRepo = new Mock<IAuditLogRepository>();
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var consumer = CreateConsumer(mockAuditLogRepo, mockAuditLogEntryRepo, mockUnitOfWork);

        var evt = new OrderCreatedEvent
        {
            EventId = Guid.NewGuid(),
            OrderId = OrderId,
            BuyerId = BuyerId,
            SellerId = SellerId,
            TotalAmount = 199.99m,
            Currency = "CNY",
            OccurredAt = DateTime.UtcNow
        };
        var context = CreateContext(evt);

        // Act
        var act = async () => await consumer.Consume(context);

        // Assert - 依赖调用失败时抛异常，由 MassTransit 重试策略处理
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("数据库连接失败");
        mockUnitOfWork.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private static AuditLogConsumer CreateConsumer(
        Mock<IAuditLogRepository> mockAuditLogRepo,
        Mock<IAuditLogEntryRepository> mockAuditLogEntryRepo,
        Mock<IUnitOfWork> mockUnitOfWork)
    {
        var mockLogger = new Mock<ILogger<AuditLogConsumer>>();
        return new AuditLogConsumer(
            mockAuditLogRepo.Object, mockAuditLogEntryRepo.Object, mockUnitOfWork.Object, mockLogger.Object);
    }

    private static ConsumeContext<T> CreateContext<T>(T message) where T : class
    {
        var mockContext = new Mock<ConsumeContext<T>>();
        mockContext.Setup(c => c.Message).Returns(message);
        mockContext.Setup(c => c.CancellationToken).Returns(CancellationToken.None);
        return mockContext.Object;
    }
}
