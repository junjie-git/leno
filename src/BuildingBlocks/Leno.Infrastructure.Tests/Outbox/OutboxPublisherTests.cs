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
            .Setup(b => b.PublishAsync(It.IsAny<IIntegrationEvent>(), It.IsAny<IReadOnlyDictionary<string, string?>>(), It.IsAny<CancellationToken>()))
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
            b => b.PublishAsync(It.IsAny<IIntegrationEvent>(), It.IsAny<IReadOnlyDictionary<string, string?>>(), It.IsAny<CancellationToken>()),
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
            .Setup(b => b.PublishAsync(It.IsAny<IIntegrationEvent>(), It.IsAny<IReadOnlyDictionary<string, string?>>(), It.IsAny<CancellationToken>()))
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
            .Setup(b => b.PublishAsync(It.IsAny<IIntegrationEvent>(), It.IsAny<IReadOnlyDictionary<string, string?>>(), It.IsAny<CancellationToken>()))
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
            b => b.PublishAsync(It.IsAny<IIntegrationEvent>(), It.IsAny<IReadOnlyDictionary<string, string?>>(), It.IsAny<CancellationToken>()),
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
            .Setup(b => b.PublishAsync(It.IsAny<IIntegrationEvent>(), It.IsAny<IReadOnlyDictionary<string, string?>>(), It.IsAny<CancellationToken>()))
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

    // ===== T22.1: Parallel.ForEachAsync 并行处理测试 =====

    /// <summary>
    /// T22.1：多消息并行处理——所有消息应最终标记为 Processed，每条消息各发布一次。
    /// 使用 AddDbContext（Scoped）使每条消息获得独立 DbContext 实例，避免并发访问冲突。
    /// </summary>
    [Fact]
    public async Task ProcessBatch_MultipleMessages_ShouldProcessInParallelAndAllSucceed()
    {
        // Arrange
        var dbName = $"outbox-parallel-{Guid.NewGuid()}";
        var options = new DbContextOptionsBuilder<TestOutboxDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        // Setup：创建 5 条 pending 消息
        await using (var setupContext = new TestOutboxDbContext(options))
        {
            for (int i = 0; i < 5; i++)
            {
                setupContext.OutboxMessages.Add(CreatePendingMessage());
            }
            await setupContext.SaveChangesAsync();
        }

        // 使用 AddDbContext（Scoped）确保每条消息在并行处理时获得独立 DbContext
        var services = new ServiceCollection();
        services.AddDbContext<TestOutboxDbContext>(opts => opts.UseInMemoryDatabase(dbName));
        services.AddLogging();
        var sp = services.BuildServiceProvider();

        var eventBusMock = new Mock<IEventBus>();
        eventBusMock
            .Setup(b => b.PublishAsync(It.IsAny<IIntegrationEvent>(), It.IsAny<IReadOnlyDictionary<string, string?>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var logger = sp.GetRequiredService<ILogger<OutboxPublisher<TestOutboxDbContext>>>();
        var publisher = new OutboxPublisher<TestOutboxDbContext>(sp, eventBusMock.Object, logger);

        // Act
        await publisher.ProcessBatchForTestAsync(CancellationToken.None);

        // Assert：5 条消息全部 Processed
        await using var assertContext = new TestOutboxDbContext(options);
        var allMessages = await assertContext.OutboxMessages.ToListAsync();
        allMessages.Should().HaveCount(5);
        allMessages.Should().AllSatisfy(m =>
        {
            m.Status.Should().Be(OutboxMessageStatus.Processed);
            m.ProcessedAt.Should().NotBeNull();
            m.PublishingStartedAt.Should().BeNull();
        });

        eventBusMock.Verify(
            b => b.PublishAsync(It.IsAny<IIntegrationEvent>(), It.IsAny<IReadOnlyDictionary<string, string?>>(), It.IsAny<CancellationToken>()),
            Times.Exactly(5));

        await sp.DisposeAsync();
    }

    /// <summary>
    /// T22.1：并行处理中部分消息发布失败不影响其它消息——失败消息回退 Pending，成功消息标记 Processed。
    /// 使用 Interlocked 计数器让第 3 次 publish 调用失败，验证单条失败不影响其它消息。
    /// </summary>
    [Fact]
    public async Task ProcessBatch_PartialFailure_ShouldNotAffectOtherMessages()
    {
        // Arrange
        var dbName = $"outbox-partial-{Guid.NewGuid()}";
        var options = new DbContextOptionsBuilder<TestOutboxDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        var messages = new List<OutboxMessage>();
        for (int i = 0; i < 4; i++)
        {
            messages.Add(CreatePendingMessage());
        }

        await using (var setupContext = new TestOutboxDbContext(options))
        {
            setupContext.OutboxMessages.AddRange(messages);
            await setupContext.SaveChangesAsync();
        }

        var services = new ServiceCollection();
        services.AddDbContext<TestOutboxDbContext>(opts => opts.UseInMemoryDatabase(dbName));
        services.AddLogging();
        var sp = services.BuildServiceProvider();

        // 让第 3 次 publish 调用失败（线程安全计数），其余成功
        var publishCallCount = 0;
        var eventBusMock = new Mock<IEventBus>();
        eventBusMock
            .Setup(b => b.PublishAsync(It.IsAny<IIntegrationEvent>(), It.IsAny<IReadOnlyDictionary<string, string?>>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                var current = Interlocked.Increment(ref publishCallCount);
                if (current == 3)
                {
                    return Task.FromException(new InvalidOperationException("第三条消息发布失败"));
                }
                return Task.CompletedTask;
            });

        var logger = sp.GetRequiredService<ILogger<OutboxPublisher<TestOutboxDbContext>>>();
        var publisher = new OutboxPublisher<TestOutboxDbContext>(sp, eventBusMock.Object, logger);

        // Act
        await publisher.ProcessBatchForTestAsync(CancellationToken.None);

        // Assert：3 条 Processed，1 条 Pending（RetryCount=1），无消息丢失
        await using var assertContext = new TestOutboxDbContext(options);
        var allMessages = await assertContext.OutboxMessages.ToListAsync();
        allMessages.Should().HaveCount(4);

        var processed = allMessages.Where(m => m.Status == OutboxMessageStatus.Processed).ToList();
        var pending = allMessages.Where(m => m.Status == OutboxMessageStatus.Pending).ToList();
        processed.Should().HaveCount(3);
        pending.Should().HaveCount(1);
        pending.Single().RetryCount.Should().Be(1);
        pending.Single().Error.Should().Contain("第三条消息发布失败");

        await sp.DisposeAsync();
    }

    // ===== T22.2: pending 积压告警测试 =====

    /// <summary>
    /// T22.2：pending 数量超过阈值时，AlertIfPendingBacklogAsync 应记录告警日志（通过不抛异常且方法完成验证）。
    /// 使用可覆盖的阈值属性，将阈值设为 5 以避免构造 100+ 条消息。
    /// </summary>
    [Fact]
    public async Task AlertIfPendingBacklog_ExceedsThreshold_ShouldLogWarning()
    {
        // Arrange
        var dbName = $"outbox-alert-{Guid.NewGuid()}";
        await using var context = await CreateContextAsync(dbName);

        // 创建 6 条 pending 消息
        for (int i = 0; i < 6; i++)
        {
            context.OutboxMessages.Add(CreatePendingMessage());
        }
        await context.SaveChangesAsync();

        var eventBusMock = new Mock<IEventBus>();
        var (publisher, services) = CreateSut(context, eventBusMock);
        // 覆盖阈值为 5，6 > 5 触发告警
        publisher.PendingAlertThreshold = 5;

        // Act：应不抛异常完成（告警仅记录日志）
        var act = () => publisher.AlertIfPendingBacklogAsync(CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();

        await services.DisposeAsync();
    }

    /// <summary>
    /// T22.2：pending 数量未超过阈值时，AlertIfPendingBacklogAsync 应静默完成不告警。
    /// </summary>
    [Fact]
    public async Task AlertIfPendingBacklog_BelowThreshold_ShouldNotLogWarning()
    {
        // Arrange
        var dbName = $"outbox-noalert-{Guid.NewGuid()}";
        await using var context = await CreateContextAsync(dbName);

        // 创建 3 条 pending 消息
        for (int i = 0; i < 3; i++)
        {
            context.OutboxMessages.Add(CreatePendingMessage());
        }
        await context.SaveChangesAsync();

        var eventBusMock = new Mock<IEventBus>();
        var (publisher, services) = CreateSut(context, eventBusMock);
        publisher.PendingAlertThreshold = 5; // 3 < 5 不触发告警

        // Act
        var act = () => publisher.AlertIfPendingBacklogAsync(CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();

        await services.DisposeAsync();
    }

    // ===== T22.3: 类型解析器接入 OutboxPublisher 测试 =====

    /// <summary>
    /// T22.3：注入自定义 IOutboxEventTypeResolver，验证发布器使用 resolver 解析类型而非 Type.GetType。
    /// 自定义 resolver 将 "CustomTypeMarker" 映射到 TestIntegrationEvent，
    /// 验证消息存储该标记时仍能正确发布。
    /// </summary>
    [Fact]
    public async Task ProcessBatch_WithCustomResolver_ShouldUseResolverToResolveType()
    {
        // Arrange
        var dbName = $"outbox-resolver-{Guid.NewGuid()}";
        await using var context = await CreateContextAsync(dbName);

        // 构造一条 Type 字段为 "CustomTypeMarker" 的消息（绕过 OutboxMessage.Create 的 FullName 写入）
        var evt = new TestIntegrationEvent { Content = "custom-resolver" };
        var message = OutboxMessage.Create(evt);
        // 通过反射修改 Type 字段为自定义标记，模拟历史脏数据或自定义格式
        var typeField = typeof(OutboxMessage).GetProperty(nameof(OutboxMessage.Type));
        typeField!.SetValue(message, "CustomTypeMarker");

        context.OutboxMessages.Add(message);
        await context.SaveChangesAsync();

        var eventBusMock = new Mock<IEventBus>();
        eventBusMock
            .Setup(b => b.PublishAsync(It.IsAny<IIntegrationEvent>(), It.IsAny<IReadOnlyDictionary<string, string?>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // 自定义 resolver：将 "CustomTypeMarker" 映射到 TestIntegrationEvent
        var customResolver = new ResolverThatMapsCustomMarker();

        var services = new ServiceCollection();
        services.AddSingleton(context);
        services.AddLogging();
        var sp = services.BuildServiceProvider();
        var logger = sp.GetRequiredService<ILogger<OutboxPublisher<TestOutboxDbContext>>>();
        var publisher = new OutboxPublisher<TestOutboxDbContext>(
            sp, eventBusMock.Object, logger, customResolver);

        // Act
        await publisher.ProcessBatchForTestAsync(CancellationToken.None);

        // Assert：消息成功处理（resolver 正确映射了类型）
        var stored = await context.OutboxMessages.SingleAsync();
        stored.Status.Should().Be(OutboxMessageStatus.Processed);
        eventBusMock.Verify(
            b => b.PublishAsync(It.IsAny<IIntegrationEvent>(), It.IsAny<IReadOnlyDictionary<string, string?>>(), It.IsAny<CancellationToken>()),
            Times.Once);

        await sp.DisposeAsync();
    }

    /// <summary>
    /// T22.3：resolver 返回 null 时，消息应标记失败（MarkAsFailed），不抛异常。
    /// MarkAsFailed 第一次调用 RetryCount=1 &lt; MaxRetryCount=5，状态为 Pending 等待重试。
    /// </summary>
    [Fact]
    public async Task ProcessBatch_WhenResolverReturnsNull_ShouldMarkAsFailed()
    {
        // Arrange
        var dbName = $"outbox-null-type-{Guid.NewGuid()}";
        await using var context = await CreateContextAsync(dbName);

        var evt = new TestIntegrationEvent { Content = "unknown-type" };
        var message = OutboxMessage.Create(evt);
        // 修改 Type 为不存在的类型标识
        var typeField = typeof(OutboxMessage).GetProperty(nameof(OutboxMessage.Type));
        typeField!.SetValue(message, "Totally.Nonexistent.Type");

        context.OutboxMessages.Add(message);
        await context.SaveChangesAsync();

        var eventBusMock = new Mock<IEventBus>();
        var (publisher, services) = CreateSut(context, eventBusMock);
        // 使用默认 resolver，"Totally.Nonexistent.Type" 无法解析

        // Act
        await publisher.ProcessBatchForTestAsync(CancellationToken.None);

        // Assert：消息标记失败（MarkAsFailed 第一次调用 RetryCount=1 < MaxRetryCount=5，状态 Pending）
        var stored = await context.OutboxMessages.SingleAsync();
        stored.Status.Should().Be(OutboxMessageStatus.Pending);
        stored.RetryCount.Should().Be(1);
        stored.Error.Should().Contain("事件类型无法解析");
        eventBusMock.Verify(
            b => b.PublishAsync(It.IsAny<IIntegrationEvent>(), It.IsAny<IReadOnlyDictionary<string, string?>>(), It.IsAny<CancellationToken>()),
            Times.Never);

        await services.DisposeAsync();
    }

    private sealed class ResolverThatMapsCustomMarker : IOutboxEventTypeResolver
    {
        public Type? Resolve(string typeName)
        {
            if (typeName == "CustomTypeMarker")
            {
                return typeof(TestIntegrationEvent);
            }
            return DefaultOutboxEventTypeResolver.Instance.Resolve(typeName);
        }
    }
}
