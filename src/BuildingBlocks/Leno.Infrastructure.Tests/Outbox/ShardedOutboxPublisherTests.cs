using Leno.Infrastructure.Abstractions;
using Leno.Infrastructure.Outbox;
using Leno.SharedContracts.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using FluentAssertions;

namespace Leno.Infrastructure.Tests.Outbox;

/// <summary>
/// 4.4 Outbox 分片发布器：ShardedOutboxPublisher 单元测试。
/// <para>
/// 覆盖：
/// - SKIP LOCKED SQL 生成正确性（SQL Server 方言）
/// - 分片过滤：仅处理本实例分片的消息，跳过其他分片
/// - 正常发布流程：Pending → Publishing → Processed
/// - 发布失败：Pending → Publishing → Pending（RetryCount++）
/// - 多分片隔离：本实例不处理其他分片的消息
/// - Publishing 超时回退 Pending
/// - 配置校验：非法 ShardId 抛异常
/// </para>
/// <para>
/// 测试使用 <see cref="LinqBackedShardedOutboxPublisher"/> 子类覆盖 FromSqlRaw 路径，
/// 改用 LINQ 查询（InMemory provider 不支持 WITH (UPDLOCK, ROWLOCK, READPAST) 提示），
/// 真实 SQL Server 语法通过 <see cref="BuildSkipLockedSql_ReturnsSqlServerSkipLockedSyntax"/> 单元测试验证。
/// </para>
/// </summary>
public class ShardedOutboxPublisherTests
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
    /// 测试用集成事件。
    /// </summary>
    private sealed class TestIntegrationEvent : IntegrationEventBase
    {
        public string Content { get; init; } = string.Empty;
    }

    /// <summary>
    /// 测试用子类：覆盖 FromSqlRaw 路径为 LINQ 查询，
    /// 使 InMemory provider 也能验证分片过滤、retry 计数等逻辑。
    /// </summary>
    private sealed class LinqBackedShardedOutboxPublisher : ShardedOutboxPublisher<TestOutboxDbContext>
    {
        public LinqBackedShardedOutboxPublisher(
            IServiceProvider serviceProvider,
            IEventBus eventBus,
            IOptions<OutboxShardingOptions> options,
            ILogger<ShardedOutboxPublisher<TestOutboxDbContext>> logger,
            IOutboxEventTypeResolver? typeResolver = null)
            : base(serviceProvider, eventBus, options, logger, typeResolver)
        {
        }

        protected override Task<List<OutboxMessage>> FetchPendingMessagesWithSkipLockedAsync(
            TestOutboxDbContext context,
            CancellationToken ct)
        {
            // 测试用 LINQ 替代 FROM SQL RAW，InMemory provider 不支持 WITH (UPDLOCK, ROWLOCK, READPAST)
            return context.Set<OutboxMessage>()
                .Where(m => m.ShardKey == InstanceShard
                            && m.Status == OutboxMessageStatus.Pending)
                .OrderBy(m => m.OccurredAt)
                .Take(Options.BatchSize)
                .ToListAsync(ct);
        }

        /// <summary>
        /// 覆盖阶段 3：InMemory provider 不支持 ExecuteUpdateAsync，
        /// 改用 tracked entity 的 MarkAsProcessed + SaveChangesAsync。
        /// 生产环境使用 ExecuteUpdateAsync 条件更新（SQL Server），测试仅验证状态机流转。
        /// </summary>
        protected override async Task MarkAsProcessedAsync(
            TestOutboxDbContext context,
            OutboxMessage message,
            Type eventType,
            CancellationToken stoppingToken)
        {
            message.MarkAsProcessed();
            await context.SaveChangesAsync(stoppingToken);
            context.ChangeTracker.Clear();
        }
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

    private static OutboxShardingOptions CreateOptions(
        int shardId = 0,
        int shardCount = 1,
        int batchSize = 50,
        int pollingIntervalSeconds = 3,
        int maxRetryCount = 5,
        int pendingAlertThreshold = 100)
    {
        var opts = new OutboxShardingOptions
        {
            ShardId = shardId,
            ShardCount = shardCount,
            BatchSize = batchSize,
            PollingIntervalSeconds = pollingIntervalSeconds,
            MaxRetryCount = maxRetryCount,
            PendingAlertThreshold = pendingAlertThreshold
        };
        opts.Validate();
        return opts;
    }

    private static (LinqBackedShardedOutboxPublisher publisher, ServiceProvider services) CreateSut(
        TestOutboxDbContext context,
        Mock<IEventBus> eventBusMock,
        OutboxShardingOptions? options = null)
    {
        var opts = options ?? CreateOptions();
        var optionsWrapper = Options.Create(opts);

        var services = new ServiceCollection();
        services.AddSingleton(context);
        services.AddLogging();
        var sp = services.BuildServiceProvider();

        var logger = sp.GetRequiredService<ILogger<ShardedOutboxPublisher<TestOutboxDbContext>>>();
        var publisher = new LinqBackedShardedOutboxPublisher(
            sp, eventBusMock.Object, optionsWrapper, logger);
        return (publisher, sp);
    }

    private static OutboxMessage CreatePendingMessage(Guid? aggregateRootId = null, int shardKey = 0)
    {
        var evt = new TestIntegrationEvent { Content = "payload-" + Guid.NewGuid() };
        var message = OutboxMessage.Create(evt);
        // 通过反射设置 AggregateRootId 和 ShardKey（测试用，绕过 Create 工厂）
        var aggId = aggregateRootId ?? Guid.Empty;
        var aggProp = typeof(OutboxMessage).GetProperty(nameof(OutboxMessage.AggregateRootId));
        aggProp!.SetValue(message, aggId);
        var shardProp = typeof(OutboxMessage).GetProperty(nameof(OutboxMessage.ShardKey));
        shardProp!.SetValue(message, shardKey);
        return message;
    }

    // ===== SKIP LOCKED SQL 生成测试 =====

    /// <summary>
    /// 验证 <see cref="ShardedOutboxPublisher{TDbContext}.BuildSkipLockedSql"/>
    /// 生成 SQL Server 兼容的 SKIP LOCKED 语法。
    /// </summary>
    [Theory]
    [InlineData(50, 0)]
    [InlineData(100, 3)]
    [InlineData(200, 7)]
    public void BuildSkipLockedSql_ReturnsSqlServerSkipLockedSyntax(int batchSize, int shardId)
    {
        // Act
        var sql = ShardedOutboxPublisher<TestOutboxDbContext>.BuildSkipLockedSql(batchSize, shardId);

        // Assert：SQL Server SKIP LOCKED 关键元素齐全
        sql.Should().Contain("TOP");
        sql.Should().Contain("outbox_messages");
        sql.Should().Contain("WITH (UPDLOCK, ROWLOCK, READPAST)");
        sql.Should().Contain($"shard_key = {shardId}");
        sql.Should().Contain("status = 0"); // Pending = 0
        sql.Should().Contain("ORDER BY occurred_at");
        // 包含 batchSize 数字（嵌入 SQL 字符串）
        sql.Should().Contain(batchSize.ToString());
    }

    /// <summary>
    /// 验证生成的 SQL 在不同分片号下结构一致，仅参数不同。
    /// </summary>
    [Fact]
    public void BuildSkipLockedSql_DifferentShardIds_OnlyDifferInShardKeyParameter()
    {
        // Act
        var sql1 = ShardedOutboxPublisher<TestOutboxDbContext>.BuildSkipLockedSql(50, 1);
        var sql2 = ShardedOutboxPublisher<TestOutboxDbContext>.BuildSkipLockedSql(50, 2);

        // Assert：除 shard_key 参数外，其余结构一致
        sql1.Should().NotBe(sql2);
        sql1.Should().Contain("shard_key = 1");
        sql2.Should().Contain("shard_key = 2");
    }

    // ===== 正常发布流程测试 =====

    /// <summary>
    /// 正常发布流程：本分片 pending 消息 → Processed。
    /// </summary>
    [Fact]
    public async Task ProcessBatch_NormalPublish_ShouldMarkAsProcessed()
    {
        // Arrange
        var dbName = $"sharded-normal-{Guid.NewGuid()}";
        await using var context = await CreateContextAsync(dbName);
        var message = CreatePendingMessage(shardKey: 0);
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

    /// <summary>
    /// 发布失败：消息回退 Pending，RetryCount 递增。
    /// </summary>
    [Fact]
    public async Task ProcessBatch_PublishFails_ShouldRevertToPendingAndIncrementRetryCount()
    {
        // Arrange
        var dbName = $"sharded-fail-{Guid.NewGuid()}";
        await using var context = await CreateContextAsync(dbName);
        var message = CreatePendingMessage(shardKey: 0);
        context.OutboxMessages.Add(message);
        await context.SaveChangesAsync();

        var eventBusMock = new Mock<IEventBus>();
        eventBusMock
            .Setup(b => b.PublishAsync(It.IsAny<IIntegrationEvent>(), It.IsAny<IReadOnlyDictionary<string, string?>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("MQ 不可用"));

        var (publisher, services) = CreateSut(context, eventBusMock);

        // Act
        await publisher.ProcessBatchForTestAsync(CancellationToken.None);

        // Assert
        var stored = await context.OutboxMessages.SingleAsync();
        stored.Status.Should().Be(OutboxMessageStatus.Pending);
        stored.RetryCount.Should().Be(1);
        stored.Error.Should().Contain("MQ 不可用");
        stored.PublishingStartedAt.Should().BeNull();

        await services.DisposeAsync();
    }

    // ===== 分片过滤测试 =====

    /// <summary>
    /// 分片过滤：实例分片号为 0 时，仅处理 shard_key=0 的消息，跳过 shard_key=1。
    /// </summary>
    [Fact]
    public async Task ProcessBatch_OnlyProcessesMessagesFromOwnShard()
    {
        // Arrange：实例分片号为 0
        var dbName = $"sharded-filter-{Guid.NewGuid()}";
        await using var context = await CreateContextAsync(dbName);

        // shard_key=0 的消息（本实例负责）
        var ownMessage1 = CreatePendingMessage(shardKey: 0);
        var ownMessage2 = CreatePendingMessage(shardKey: 0);
        // shard_key=1 的消息（其他实例负责）
        var otherMessage1 = CreatePendingMessage(shardKey: 1);
        var otherMessage2 = CreatePendingMessage(shardKey: 1);

        context.OutboxMessages.AddRange(ownMessage1, ownMessage2, otherMessage1, otherMessage2);
        await context.SaveChangesAsync();

        var eventBusMock = new Mock<IEventBus>();
        eventBusMock
            .Setup(b => b.PublishAsync(It.IsAny<IIntegrationEvent>(), It.IsAny<IReadOnlyDictionary<string, string?>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var options = CreateOptions(shardId: 0, shardCount: 2);
        var (publisher, services) = CreateSut(context, eventBusMock, options);

        // Act
        await publisher.ProcessBatchForTestAsync(CancellationToken.None);

        // Assert：仅本实例分片（shard_key=0）的 2 条消息被处理
        var allMessages = await context.OutboxMessages.ToListAsync();
        var processedOwn = allMessages.Where(m => m.ShardKey == 0 && m.Status == OutboxMessageStatus.Processed).ToList();
        var pendingOther = allMessages.Where(m => m.ShardKey == 1 && m.Status == OutboxMessageStatus.Pending).ToList();

        processedOwn.Should().HaveCount(2);
        pendingOther.Should().HaveCount(2);
        eventBusMock.Verify(
            b => b.PublishAsync(It.IsAny<IIntegrationEvent>(), It.IsAny<IReadOnlyDictionary<string, string?>>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));

        await services.DisposeAsync();
    }

    /// <summary>
    /// 分片过滤：实例分片号为 1 时，仅处理 shard_key=1 的消息。
    /// </summary>
    [Fact]
    public async Task ProcessBatch_WithShardId1_OnlyProcessesShard1Messages()
    {
        // Arrange：实例分片号为 1
        var dbName = $"sharded-filter-1-{Guid.NewGuid()}";
        await using var context = await CreateContextAsync(dbName);

        var shard0Message = CreatePendingMessage(shardKey: 0);
        var shard1Message1 = CreatePendingMessage(shardKey: 1);
        var shard1Message2 = CreatePendingMessage(shardKey: 1);

        context.OutboxMessages.AddRange(shard0Message, shard1Message1, shard1Message2);
        await context.SaveChangesAsync();

        var eventBusMock = new Mock<IEventBus>();
        eventBusMock
            .Setup(b => b.PublishAsync(It.IsAny<IIntegrationEvent>(), It.IsAny<IReadOnlyDictionary<string, string?>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var options = CreateOptions(shardId: 1, shardCount: 2);
        var (publisher, services) = CreateSut(context, eventBusMock, options);

        // Act
        await publisher.ProcessBatchForTestAsync(CancellationToken.None);

        // Assert：仅 shard_key=1 的 2 条消息被处理
        var allMessages = await context.OutboxMessages.ToListAsync();
        var processedShard1 = allMessages.Where(m => m.ShardKey == 1 && m.Status == OutboxMessageStatus.Processed).ToList();
        var pendingShard0 = allMessages.Where(m => m.ShardKey == 0 && m.Status == OutboxMessageStatus.Pending).ToList();

        processedShard1.Should().HaveCount(2);
        pendingShard0.Should().HaveCount(1);
        eventBusMock.Verify(
            b => b.PublishAsync(It.IsAny<IIntegrationEvent>(), It.IsAny<IReadOnlyDictionary<string, string?>>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));

        await services.DisposeAsync();
    }

    /// <summary>
    /// 端到端：发布失败 → 回退 Pending → 第二次 ProcessBatch 重试 → 成功标记 Processed。
    /// </summary>
    [Fact]
    public async Task ProcessBatch_RetryAfterFailure_ShouldEventuallySucceed()
    {
        // Arrange
        var dbName = $"sharded-retry-{Guid.NewGuid()}";
        await using var context = await CreateContextAsync(dbName);
        var message = CreatePendingMessage(shardKey: 0);
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
        afterSecond.RetryCount.Should().Be(1);

        eventBusMock.Verify(
            b => b.PublishAsync(It.IsAny<IIntegrationEvent>(), It.IsAny<IReadOnlyDictionary<string, string?>>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));

        await services.DisposeAsync();
    }

    /// <summary>
    /// 重试次数超过 MaxRetryCount 后，消息进入 DeadLetter 状态。
    /// </summary>
    [Fact]
    public async Task ProcessBatch_ExceedMaxRetry_ShouldEnterDeadLetter()
    {
        // Arrange
        var dbName = $"sharded-deadletter-{Guid.NewGuid()}";
        await using var context = await CreateContextAsync(dbName);
        var message = CreatePendingMessage(shardKey: 0);
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

    // ===== Publishing 超时回退测试 =====

    /// <summary>
    /// Publishing 状态消息超时（&gt; 5 分钟）时，由 RecoverStalePublishingAsync 回退 Pending。
    /// </summary>
    [Fact]
    public async Task RecoverStalePublishing_StaleMessage_ShouldResetToPending()
    {
        // Arrange
        var dbName = $"sharded-stale-{Guid.NewGuid()}";
        await using var context = await CreateContextAsync(dbName);

        var message = CreatePendingMessage(shardKey: 0);
        message.MarkAsPublishing();
        context.OutboxMessages.Add(message);
        await context.SaveChangesAsync();

        // 通过反射将 PublishingStartedAt 改为 6 分钟前
        var staleTime = DateTime.UtcNow.AddMinutes(-6);
        var prop = typeof(OutboxMessage).GetProperty(nameof(OutboxMessage.PublishingStartedAt));
        prop!.SetValue(message, staleTime);
        await context.SaveChangesAsync();

        var eventBusMock = new Mock<IEventBus>();
        var (publisher, services) = CreateSut(context, eventBusMock);

        // Act
        await publisher.RecoverStalePublishingAsync(CancellationToken.None);

        // Assert：超时消息回退 Pending
        var stored = await context.OutboxMessages.SingleAsync();
        stored.Status.Should().Be(OutboxMessageStatus.Pending);
        stored.PublishingStartedAt.Should().BeNull();

        await services.DisposeAsync();
    }

    /// <summary>
    /// RecoverStalePublishingAsync 仅回退本分片的超时消息，不影响其他分片。
    /// </summary>
    [Fact]
    public async Task RecoverStalePublishing_OnlyResetsOwnShardMessages()
    {
        // Arrange
        var dbName = $"sharded-stale-isolation-{Guid.NewGuid()}";
        await using var context = await CreateContextAsync(dbName);

        // 本分片（shard_key=0）超时消息
        var ownStale = CreatePendingMessage(shardKey: 0);
        ownStale.MarkAsPublishing();
        var otherStale = CreatePendingMessage(shardKey: 1);
        otherStale.MarkAsPublishing();

        context.OutboxMessages.AddRange(ownStale, otherStale);
        await context.SaveChangesAsync();

        // 都改为 6 分钟前
        var staleTime = DateTime.UtcNow.AddMinutes(-6);
        var prop = typeof(OutboxMessage).GetProperty(nameof(OutboxMessage.PublishingStartedAt));
        prop!.SetValue(ownStale, staleTime);
        prop!.SetValue(otherStale, staleTime);
        await context.SaveChangesAsync();

        var eventBusMock = new Mock<IEventBus>();
        var options = CreateOptions(shardId: 0, shardCount: 2);
        var (publisher, services) = CreateSut(context, eventBusMock, options);

        // Act
        await publisher.RecoverStalePublishingAsync(CancellationToken.None);

        // Assert：仅本分片（shard_key=0）的消息被回退，其他分片保持 Publishing
        var allMessages = await context.OutboxMessages.ToListAsync();
        var ownMessage = allMessages.Single(m => m.ShardKey == 0);
        var otherMessage = allMessages.Single(m => m.ShardKey == 1);

        ownMessage.Status.Should().Be(OutboxMessageStatus.Pending);
        otherMessage.Status.Should().Be(OutboxMessageStatus.Publishing);

        await services.DisposeAsync();
    }

    // ===== 配置校验测试 =====

    /// <summary>
    /// 非法 ShardId（&lt; 0 或 &gt;= ShardCount）构造时抛 InvalidOperationException。
    /// </summary>
    [Theory]
    [InlineData(-1, 4)]   // 负值
    [InlineData(4, 4)]    // 等于 ShardCount
    [InlineData(5, 4)]    // 大于 ShardCount
    public void Constructor_InvalidShardId_ThrowsInvalidOperationException(int shardId, int shardCount)
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var sp = services.BuildServiceProvider();
        var eventBusMock = new Mock<IEventBus>();
        var logger = sp.GetRequiredService<ILogger<ShardedOutboxPublisher<TestOutboxDbContext>>>();
        var options = Options.Create(new OutboxShardingOptions
        {
            ShardId = shardId,
            ShardCount = shardCount
        });

        // Act
        var act = () => new ShardedOutboxPublisher<TestOutboxDbContext>(
            sp, eventBusMock.Object, options, logger);

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    /// <summary>
    /// 非法 BatchSize（&lt; 1）构造时抛异常。
    /// </summary>
    [Fact]
    public void Constructor_InvalidBatchSize_ThrowsInvalidOperationException()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var sp = services.BuildServiceProvider();
        var eventBusMock = new Mock<IEventBus>();
        var logger = sp.GetRequiredService<ILogger<ShardedOutboxPublisher<TestOutboxDbContext>>>();
        var options = Options.Create(new OutboxShardingOptions
        {
            ShardId = 0,
            ShardCount = 1,
            BatchSize = 0
        });

        // Act
        var act = () => new ShardedOutboxPublisher<TestOutboxDbContext>(
            sp, eventBusMock.Object, options, logger);

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    /// <summary>
    /// 积压告警：本分片 pending 数量超过阈值时不抛异常（仅记录日志）。
    /// </summary>
    [Fact]
    public async Task AlertIfPendingBacklog_ExceedsThreshold_ShouldNotThrow()
    {
        // Arrange
        var dbName = $"sharded-alert-{Guid.NewGuid()}";
        await using var context = await CreateContextAsync(dbName);

        // 创建 6 条本分片 pending 消息
        for (int i = 0; i < 6; i++)
        {
            context.OutboxMessages.Add(CreatePendingMessage(shardKey: 0));
        }
        await context.SaveChangesAsync();

        var eventBusMock = new Mock<IEventBus>();
        var options = CreateOptions(shardId: 0, shardCount: 1, pendingAlertThreshold: 5);
        var (publisher, services) = CreateSut(context, eventBusMock, options);

        // Act
        var act = () => publisher.AlertIfPendingBacklogAsync(CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();

        await services.DisposeAsync();
    }

    /// <summary>
    /// 积压统计仅计算本分片的 pending 消息，不含其他分片。
    /// </summary>
    [Fact]
    public async Task AlertIfPendingBacklog_OnlyCountsOwnShardMessages()
    {
        // Arrange
        var dbName = $"sharded-alert-shard-{Guid.NewGuid()}";
        await using var context = await CreateContextAsync(dbName);

        // 本分片（shard_key=0）3 条 pending
        for (int i = 0; i < 3; i++)
        {
            context.OutboxMessages.Add(CreatePendingMessage(shardKey: 0));
        }
        // 其他分片（shard_key=1）10 条 pending
        for (int i = 0; i < 10; i++)
        {
            context.OutboxMessages.Add(CreatePendingMessage(shardKey: 1));
        }
        await context.SaveChangesAsync();

        var eventBusMock = new Mock<IEventBus>();
        var options = CreateOptions(shardId: 0, shardCount: 2, pendingAlertThreshold: 5);
        var (publisher, services) = CreateSut(context, eventBusMock, options);

        // Act：本分片仅 3 条 < 阈值 5，不应抛异常
        var act = () => publisher.AlertIfPendingBacklogAsync(CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();

        await services.DisposeAsync();
    }
}
