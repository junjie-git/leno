using Leno.Infrastructure.Abstractions;
using Leno.Infrastructure.Outbox;
using Leno.SharedContracts.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Leno.Infrastructure.Tests.Outbox;

/// <summary>
/// P1-T11/T12 验证：OutboxPublisher MarkAsProcessed 条件更新 + ChangeTracker 清理。
/// <para>
/// T11：MarkAsProcessed 使用 <c>ExecuteUpdateAsync</c> 条件更新（WHERE Status = Publishing），
/// 保证只有持有 Publishing 锁的实例能标记 Processed；若状态已被其他实例重置为 Pending，
/// 条件更新不命中（0 行），消息不会被误改为 Processed。
/// </para>
/// <para>
/// T12：阶段 3 的 <c>finally</c> 块调用 <c>context.ChangeTracker.Clear()</c>，
/// 清理 stage 1 加载的 stale tracked entity（其 Status 仍为 Publishing，而 DB 已通过
/// ExecuteUpdateAsync 更新为 Processed），避免残留实体在 context 复用时被意外持久化。
/// </para>
/// </summary>
public class OutboxPublisherConditionalUpdateTests
{
    private sealed class TestConditionalDbContext : DbContext
    {
        public TestConditionalDbContext(DbContextOptions<TestConditionalDbContext> options) : base(options)
        {
        }

        public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    }

    private sealed class TestConditionalEvent : IntegrationEventBase
    {
        public string Payload { get; init; } = string.Empty;
    }

    private static async Task<TestConditionalDbContext> CreateContextAsync(string dbName)
    {
        var options = new DbContextOptionsBuilder<TestConditionalDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        var context = new TestConditionalDbContext(options);
        await context.Database.EnsureCreatedAsync();
        return context;
    }

    private static (OutboxPublisher<TestConditionalDbContext> publisher, ServiceProvider services) CreateSut(
        TestConditionalDbContext context,
        Mock<IEventBus> eventBusMock)
    {
        var services = new ServiceCollection();
        services.AddSingleton(context);
        services.AddLogging();
        var sp = services.BuildServiceProvider();

        var logger = sp.GetRequiredService<ILogger<OutboxPublisher<TestConditionalDbContext>>>();
        var publisher = new OutboxPublisher<TestConditionalDbContext>(sp, eventBusMock.Object, logger);
        return (publisher, sp);
    }

    /// <summary>
    /// T11 验证：正常发布流程中，MarkAsProcessed 条件更新命中（Status == Publishing），
    /// 消息变为 Processed，且 stage 1 加载的 stale tracked entity 被清理（T12）。
    /// </summary>
    [Fact]
    public async Task ProcessBatch_NormalPublish_ConditionalUpdate_ShouldMarkAsProcessed()
    {
        // Arrange
        var dbName = $"conditional-normal-{Guid.NewGuid()}";
        await using var context = await CreateContextAsync(dbName);
        var evt = new TestConditionalEvent { Payload = "normal" };
        var message = OutboxMessage.Create(evt);
        context.OutboxMessages.Add(message);
        await context.SaveChangesAsync();

        var eventBusMock = new Mock<IEventBus>();
        eventBusMock
            .Setup(b => b.PublishAsync(It.IsAny<IIntegrationEvent>(), It.IsAny<IReadOnlyDictionary<string, string?>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var (publisher, services) = CreateSut(context, eventBusMock);

        // Act
        await publisher.ProcessBatchForTestAsync(CancellationToken.None);

        // Assert — 条件更新命中，消息标记为 Processed
        // 清除 ChangeTracker 以确保从 InMemory store 读取最新状态
        context.ChangeTracker.Clear();
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
    /// T11 验证：发布成功后，若消息状态已被其他实例从 Publishing 重置为 Pending（如 RecoverStalePublishing），
    /// 条件更新（WHERE Status = Publishing）不命中（0 行），消息保持 Pending 不被误改为 Processed。
    /// 同时验证 T12：条件未命中时 ChangeTracker 仍被清理。
    /// </summary>
    [Fact]
    public async Task ProcessBatch_StatusRevertedBeforeMarkAsProcessed_ShouldNotMarkAsProcessed()
    {
        // Arrange
        var dbName = $"conditional-reverted-{Guid.NewGuid()}";
        await using var context = await CreateContextAsync(dbName);
        var evt = new TestConditionalEvent { Payload = "reverted" };
        var message = OutboxMessage.Create(evt);
        context.OutboxMessages.Add(message);
        await context.SaveChangesAsync();

        // 拦截发布成功后的时刻：在 PublishAsync 完成后、MarkAsProcessed 之前，
        // 通过回调将消息状态重置为 Pending（模拟另一实例的 RecoverStalePublishing 已将其回退）
        var eventBusMock = new Mock<IEventBus>();
        eventBusMock
            .Setup(b => b.PublishAsync(It.IsAny<IIntegrationEvent>(), It.IsAny<IReadOnlyDictionary<string, string?>>(), It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                // 模拟并发场景：发布成功的瞬间，另一实例的 RecoverStalePublishing 已将消息回退为 Pending
                // 直接操作 InMemory store：将 Status 改为 Pending（绕过当前 tracked entity，使用 ExecuteUpdate 直接更新）
                context.OutboxMessages
                    .Where(m => m.Id == message.Id)
                    .ExecuteUpdate(s => s.SetProperty(m => m.Status, OutboxMessageStatus.Pending));
            })
            .Returns(Task.CompletedTask);

        var (publisher, services) = CreateSut(context, eventBusMock);

        // Act
        await publisher.ProcessBatchForTestAsync(CancellationToken.None);

        // Assert — 条件更新不命中（Status == Pending != Publishing），消息保持 Pending
        context.ChangeTracker.Clear();
        var stored = await context.OutboxMessages.SingleAsync();
        stored.Status.Should().Be(OutboxMessageStatus.Pending,
            "条件更新 WHERE Status=Publishing 不命中（已被重置为 Pending），消息不应被标记为 Processed");

        await services.DisposeAsync();
    }

    /// <summary>
    /// T12 验证：正常发布成功后，stage 1 加载的 tracked entity（Status=Publishing，stale）
    /// 被 finally 块的 ChangeTracker.Clear() 清理。验证方式：
    /// 重新从 DB 加载消息，状态应为 Processed（而非 tracked entity 中的 stale Publishing），
    /// 且 ChangeTracker.Entries() 为空。
    /// </summary>
    [Fact]
    public async Task ProcessBatch_AfterSuccess_ChangeTrackerShouldBeCleared()
    {
        // Arrange
        var dbName = $"changetracker-clear-{Guid.NewGuid()}";
        await using var context = await CreateContextAsync(dbName);
        var evt = new TestConditionalEvent { Payload = "clear-test" };
        var message = OutboxMessage.Create(evt);
        context.OutboxMessages.Add(message);
        await context.SaveChangesAsync();

        var eventBusMock = new Mock<IEventBus>();
        eventBusMock
            .Setup(b => b.PublishAsync(It.IsAny<IIntegrationEvent>(), It.IsAny<IReadOnlyDictionary<string, string?>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var (publisher, services) = CreateSut(context, eventBusMock);

        // Act
        await publisher.ProcessBatchForTestAsync(CancellationToken.None);

        // Assert — T12：ChangeTracker 已被 finally 块清理，无残留 Tracked Entity
        // stage 1 加载的 message entity（Status=Publishing，stale）不应残留
        context.ChangeTracker.Entries().Should().BeEmpty(
            "stage 3 的 finally 块应调用 ChangeTracker.Clear() 清理 stale tracked entity，避免残留实体在 context 复用时被意外持久化");

        // 从 DB 重新加载确认实际状态为 Processed（ExecuteUpdateAsync 已更新 DB，tracked entity 已被清理）
        var stored = await context.OutboxMessages.SingleAsync();
        stored.Status.Should().Be(OutboxMessageStatus.Processed,
            "ExecuteUpdateAsync 已将 DB 中消息更新为 Processed，tracked entity 被清理不影响 DB 状态");

        await services.DisposeAsync();
    }

    /// <summary>
    /// T12 验证：条件更新未命中（0 行）时，finally 块仍清理 ChangeTracker。
    /// 此场景下 stage 1 已加载 tracked entity（Status=Publishing），但 DB 中状态已被重置为 Pending，
    /// tracked entity 与 DB 不一致。finally 块清理后，tracked entity 不残留。
    /// </summary>
    [Fact]
    public async Task ProcessBatch_ConditionalMiss_ChangeTrackerShouldBeCleared()
    {
        // Arrange
        var dbName = $"changetracker-miss-{Guid.NewGuid()}";
        await using var context = await CreateContextAsync(dbName);
        var evt = new TestConditionalEvent { Payload = "miss-test" };
        var message = OutboxMessage.Create(evt);
        context.OutboxMessages.Add(message);
        await context.SaveChangesAsync();

        var eventBusMock = new Mock<IEventBus>();
        eventBusMock
            .Setup(b => b.PublishAsync(It.IsAny<IIntegrationEvent>(), It.IsAny<IReadOnlyDictionary<string, string?>>(), It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                // 模拟并发：发布成功瞬间，另一实例将状态重置为 Pending
                context.OutboxMessages
                    .Where(m => m.Id == message.Id)
                    .ExecuteUpdate(s => s.SetProperty(m => m.Status, OutboxMessageStatus.Pending));
            })
            .Returns(Task.CompletedTask);

        var (publisher, services) = CreateSut(context, eventBusMock);

        // Act
        await publisher.ProcessBatchForTestAsync(CancellationToken.None);

        // Assert — T12：条件未命中时 ChangeTracker 仍被清理
        context.ChangeTracker.Entries().Should().BeEmpty(
            "条件更新未命中时，finally 块仍应清理 ChangeTracker 中的 stale tracked entity");

        await services.DisposeAsync();
    }
}
