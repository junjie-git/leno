using Leno.Infrastructure.Abstractions;
using Leno.Infrastructure.Outbox;
using Leno.SharedContracts.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace Leno.Infrastructure.Tests.Outbox;

/// <summary>
/// Outbox 两阶段标记（T13）测试：
/// - 正常发布成功：Pending → Publishing → Processed
/// - 发布失败重试：Pending → Publishing → Pending（RetryCount++）
/// - 发布成功但标记失败（Publishing 超时）：由 RecoverStalePublishingAsync 兜底回退 Pending
/// </summary>
public class OutboxPublisherTests
{
    /// <summary>
    /// 测试用 DbContext，仅承载 OutboxMessage 表。
    /// </summary>
    private sealed class TestOutboxDbContext : DbContext
    {
        public TestOutboxDbContext(DbContextOptions<TestOutboxDbContext> options) : base(options)
        {
        }

        public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    }

    /// <summary>
    /// 测试用集成事件，避免依赖具体业务事件类型。
    /// </summary>
    private sealed class TestIntegrationEvent : IntegrationEventBase
    {
        public string Content { get; init; } = string.Empty;
    }

    private static async Task<TestOutboxDbContext> CreateContextAsync(string dbName)
    {
        var options = new DbContextOptionsBuilder<TestOutboxDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        var context = new TestOutboxDbContext(options);
        await context.Database.EnsureCreatedAsync();
        return context;
    }

    private static (OutboxPublisher<TestOutboxDbContext> publisher, ServiceProvider services) CreateSut(
        TestOutboxDbContext context,
        Mock<IEventBus> eventBusMock)
    {
        var services = new ServiceCollection();
        services.AddSingleton(context);
        services.AddLogging();
        var sp = services.BuildServiceProvider();

        var logger = sp.GetRequiredService<ILogger<OutboxPublisher<TestOutboxDbContext>>>();
        var publisher = new OutboxPublisher<TestOutboxDbContext>(sp, eventBusMock.Object, logger);
        return (publisher, sp);
    }

    private static OutboxMessage CreatePendingMessage()
    {
        var evt = new TestIntegrationEvent { Content = "payload-" + Guid.NewGuid() };
        return OutboxMessage.Create(evt);
    }

    [Fact]
    public async Task ProcessBatch_NormalPublish_ShouldMarkAsProcessed()
    {
        // Arrange
        var dbName = $"outbox-normal-{Guid.NewGuid()}";
        await using var context = await CreateContextAsync(dbName);
        var message = CreatePendingMessage();
        context.OutboxMessages.Add(message);
        await context.SaveChangesAsync();

        var eventBusMock = new Mock<IEventBus>();
        eventBusMock
            .Setup(b => b.PublishAsync(It.IsAny<IIntegrationEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var (publisher, services) = CreateSut(context, eventBusMock);

        // Act
        await publisher.ProcessBatchForTestAsync(CancellationToken.None);

        // Assert
        var stored = await context.OutboxMessages.SingleAsync();
        stored.Status.Should().Be(OutboxMessageStatus.Processed);
        stored.ProcessedAt.Should().NotBeNull();
        stored.PublishingStartedAt.Should().BeNull();
        eventBusMock.Verify(
            b => b.PublishAsync(It.IsAny<IIntegrationEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);

        await services.DisposeAsync();
    }

    [Fact]
    public async Task ProcessBatch_PublishFails_ShouldRevertToPendingAndIncrementRetryCount()
    {
        // Arrange
        var dbName = $"outbox-fail-{Guid.NewGuid()}";
        await using var context = await CreateContextAsync(dbName);
        var message = CreatePendingMessage();
        context.OutboxMessages.Add(message);
        await context.SaveChangesAsync();

        var eventBusMock = new Mock<IEventBus>();
        eventBusMock
            .Setup(b => b.PublishAsync(It.IsAny<IIntegrationEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("MQ 不可用"));

        var (publisher, services) = CreateSut(context, eventBusMock);

        // Act
        await publisher.ProcessBatchForTestAsync(CancellationToken.None);

        // Assert：消息回退 Pending，RetryCount 递增，下次轮询可重试
        var stored = await context.OutboxMessages.SingleAsync();
        stored.Status.Should().Be(OutboxMessageStatus.Pending);
        stored.RetryCount.Should().Be(1);
        stored.Error.Should().Contain("MQ 不可用");
        stored.PublishingStartedAt.Should().BeNull();

        await services.DisposeAsync();
    }

    /// <summary>
    /// 模拟"发布成功但 Processed 标记失败"场景：
    /// 直接构造一条 Publishing 状态且超时的消息，验证 RecoverStalePublishingAsync 将其回退 Pending。
    /// </summary>
    [Fact]
    public async Task RecoverStalePublishing_StaleMessage_ShouldResetToPending()
    {
        // Arrange
        var dbName = $"outbox-stale-{Guid.NewGuid()}";
        await using var context = await CreateContextAsync(dbName);

        var evt = new TestIntegrationEvent { Content = "stale" };
        var message = OutboxMessage.Create(evt);
        // 模拟发布成功但 Processed 标记失败后应用重启的中间态
        message.MarkAsPublishing();
        context.OutboxMessages.Add(message);
        await context.SaveChangesAsync();

        // 通过反射将 PublishingStartedAt 改为 6 分钟前（超过 5 分钟超时阈值），
        // 模拟应用重启后扫描到的超时 Publishing 消息
        var staleTime = DateTime.UtcNow.AddMinutes(-6);
        var prop = typeof(OutboxMessage).GetProperty(nameof(OutboxMessage.PublishingStartedAt));
        prop!.SetValue(message, staleTime);
        await context.SaveChangesAsync();

        var eventBusMock = new Mock<IEventBus>();
        var (publisher, services) = CreateSut(context, eventBusMock);

        // Act
        await publisher.RecoverStalePublishingAsync(CancellationToken.None);

        // Assert：超时消息回退 Pending，可被下次 ProcessBatch 重新发布，依赖下游幂等兜底
        var stored = await context.OutboxMessages.SingleAsync();
        stored.Status.Should().Be(OutboxMessageStatus.Pending);
        stored.PublishingStartedAt.Should().BeNull();

        await services.DisposeAsync();
    }

    /// <summary>
    /// Publishing 状态消息未超时（&lt; 5 分钟）时不应被回退，避免与正在发布中的流程冲突。
    /// </summary>
    [Fact]
    public async Task RecoverStalePublishing_RecentMessage_ShouldNotReset()
    {
        // Arrange
        var dbName = $"outbox-recent-{Guid.NewGuid()}";
        await using var context = await CreateContextAsync(dbName);

        var evt = new TestIntegrationEvent { Content = "recent" };
        var message = OutboxMessage.Create(evt);
        message.MarkAsPublishing();
        context.OutboxMessages.Add(message);
        await context.SaveChangesAsync();

        var eventBusMock = new Mock<IEventBus>();
        var (publisher, services) = CreateSut(context, eventBusMock);

        // Act
        await publisher.RecoverStalePublishingAsync(CancellationToken.None);

        // Assert：未超时的 Publishing 消息保持原状态
        var stored = await context.OutboxMessages.SingleAsync();
        stored.Status.Should().Be(OutboxMessageStatus.Publishing);

        await services.DisposeAsync();
    }

    /// <summary>
    /// 端到端验证：发布失败 → 回退 Pending → 第二次 ProcessBatch 重试 → 成功标记 Processed。
    /// </summary>
    [Fact]
    public async Task ProcessBatch_RetryAfterFailure_ShouldEventuallySucceed()
    {
        // Arrange
        var dbName = $"outbox-retry-{Guid.NewGuid()}";
        await using var context = await CreateContextAsync(dbName);
        var message = CreatePendingMessage();
        context.OutboxMessages.Add(message);
        await context.SaveChangesAsync();

        var eventBusMock = new Mock<IEventBus>();
        var callCount = 0;
        eventBusMock
            .Setup(b => b.PublishAsync(It.IsAny<IIntegrationEvent>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                callCount++;
                if (callCount == 1)
                {
                    return Task.FromException(new InvalidOperationException("首次发布失败"));
                }
                return Task.CompletedTask;
            });

        var (publisher, services) = CreateSut(context, eventBusMock);

        // Act 1：首次发布失败
        await publisher.ProcessBatchForTestAsync(CancellationToken.None);
        var afterFirst = await context.OutboxMessages.SingleAsync();
        afterFirst.Status.Should().Be(OutboxMessageStatus.Pending);
        afterFirst.RetryCount.Should().Be(1);

        // Act 2：第二次重试发布成功
        await publisher.ProcessBatchForTestAsync(CancellationToken.None);
        var afterSecond = await context.OutboxMessages.SingleAsync();
        afterSecond.Status.Should().Be(OutboxMessageStatus.Processed);
        afterSecond.RetryCount.Should().Be(1); // 成功路径不递增重试计数

        eventBusMock.Verify(
            b => b.PublishAsync(It.IsAny<IIntegrationEvent>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));

        await services.DisposeAsync();
    }

    /// <summary>
    /// 重试次数超过 MaxRetryCount（5）后，消息应进入 DeadLetter 状态，不再重试。
    /// </summary>
    [Fact]
    public async Task ProcessBatch_ExceedMaxRetry_ShouldEnterDeadLetter()
    {
        // Arrange
        var dbName = $"outbox-deadletter-{Guid.NewGuid()}";
        await using var context = await CreateContextAsync(dbName);
        var message = CreatePendingMessage();
        context.OutboxMessages.Add(message);
        await context.SaveChangesAsync();

        var eventBusMock = new Mock<IEventBus>();
        eventBusMock
            .Setup(b => b.PublishAsync(It.IsAny<IIntegrationEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("永久故障"));

        var (publisher, services) = CreateSut(context, eventBusMock);

        // Act：连续 5 次发布失败
        for (int i = 0; i < 5; i++)
        {
            await publisher.ProcessBatchForTestAsync(CancellationToken.None);
        }

        // Assert：第 5 次失败后进入 DeadLetter
        var stored = await context.OutboxMessages.SingleAsync();
        stored.Status.Should().Be(OutboxMessageStatus.DeadLetter);
        stored.RetryCount.Should().Be(5);

        await services.DisposeAsync();
    }
}
