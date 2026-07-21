using Leno.Infrastructure.EventBus;
using Leno.Infrastructure.Outbox;
using Leno.Infrastructure.Persistence;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using FluentAssertions;

namespace Leno.Infrastructure.Tests.Persistence;

/// <summary>
/// 验证 <see cref="EfCoreUnitOfWork{TDbContext}.SaveChangesAsync"/> 标记 [Obsolete] 警告旁路 Outbox，
/// 且 <see cref="EfCoreUnitOfWork{TDbContext}.SaveEntitiesAsync"/> 正确将领域事件持久化到发件箱表。
/// </summary>
public class UnitOfWorkOutboxBypassTests
{
    /// <summary>
    /// SaveChangesAsync 应标记 [Obsolete] 特性，提示使用 SaveEntitiesAsync 以避免旁路 Outbox。
    /// </summary>
    [Fact]
    public void SaveChangesAsync_ShouldBeMarkedObsolete_ToPreventOutboxBypass()
    {
        // Arrange — 通过反射获取 SaveChangesAsync 方法
        var saveChangesMethod = typeof(EfCoreUnitOfWork<DbContext>)
            .GetMethod("SaveChangesAsync", new[] { typeof(CancellationToken) });

        // Assert — 应有 [Obsolete] 特性
        saveChangesMethod.Should().NotBeNull("SaveChangesAsync 方法应存在");
        var obsoleteAttr = saveChangesMethod!.GetCustomAttributes(typeof(ObsoleteAttribute), false);
        obsoleteAttr.Should().HaveCount(1, "SaveChangesAsync 应标记 [Obsolete] 警告旁路 Outbox");
        ((ObsoleteAttribute)obsoleteAttr[0]).Message.Should().Contain("SaveEntitiesAsync",
            "Obsolete 提示应引导使用 SaveEntitiesAsync");
    }

    /// <summary>
    /// SaveChangesAsync 应委托给 SaveChangesWithOutboxAsync（经发件箱扩展），
    /// 而非直接调 context.SaveChangesAsync 旁路 Outbox。
    /// </summary>
    [Fact]
    public async Task SaveChangesAsync_ShouldDelegateToSaveChangesWithOutboxAsync()
    {
        // Arrange — 使用真实 InMemoryDatabase 验证经发件箱扩展的完整路径
        var dbName = $"uow-bypass-{Guid.NewGuid()}";
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        await using var context = new TestDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var mapper = new TrackingIntegrationEventMapper();
        var uow = new EfCoreUnitOfWork<TestDbContext>(context, mapper);

        var aggregate = new TestAggregateRoot(Guid.NewGuid());
        aggregate.AddTestDomainEvent();
        context.TestAggregates.Add(aggregate);

        // Act — 调用标记 [Obsolete] 的 SaveChangesAsync（应与 SaveEntitiesAsync 走相同发件箱路径）
#pragma warning disable CS0618
        var result = await uow.SaveChangesAsync(CancellationToken.None);
#pragma warning restore CS0618

        // Assert — 领域事件被翻译为集成事件并写入发件箱
        result.Should().BeGreaterThan(0, "SaveChangesAsync 应返回受影响行数");
        mapper.MapCallCount.Should().Be(1, "聚合根有 1 个领域事件，mapper 应被调用一次");
        var outboxMessages = await context.OutboxMessages.ToListAsync();
        outboxMessages.Should().NotBeEmpty("SaveChangesAsync 经发件箱扩展应将领域事件持久化到 OutboxMessages");
        aggregate.DomainEvents.Should().BeEmpty("SaveChanges 后应清除聚合的领域事件");
    }

    /// <summary>
    /// SaveEntitiesAsync 应将领域事件翻译为集成事件并写入发件箱表。
    /// </summary>
    [Fact]
    public async Task SaveEntitiesAsync_ShouldPersistOutboxMessages()
    {
        // Arrange
        var dbName = $"uow-outbox-{Guid.NewGuid()}";
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        await using var context = new TestDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var mapper = new TrackingIntegrationEventMapper();
        var uow = new EfCoreUnitOfWork<TestDbContext>(context, mapper);

        var aggregate = new TestAggregateRoot(Guid.NewGuid());
        aggregate.AddTestDomainEvent();
        context.TestAggregates.Add(aggregate);

        // Act
        var result = await uow.SaveEntitiesAsync(CancellationToken.None);

        // Assert
        result.Should().BeTrue("SaveEntitiesAsync 始终返回 true");
        var outboxMessages = await context.OutboxMessages.ToListAsync();
        outboxMessages.Should().NotBeEmpty("SaveEntitiesAsync 应将领域事件翻译为集成事件写入发件箱");
        aggregate.DomainEvents.Should().BeEmpty("SaveEntities 后应清除聚合的领域事件");
    }

    private sealed class TestDbContext : DbContext
    {
        public TestDbContext(DbContextOptions<TestDbContext> options) : base(options)
        {
        }

        public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
        public DbSet<TestAggregateRoot> TestAggregates => Set<TestAggregateRoot>();
    }

    private sealed class TrackingIntegrationEventMapper : IIntegrationEventMapper
    {
        public int MapCallCount { get; private set; }

        public IIntegrationEvent? Map(IDomainEvent domainEvent)
        {
            MapCallCount++;
            return new TestIntegrationEvent();
        }
    }

    private sealed class TestAggregateRoot : AggregateRoot
    {
        public TestAggregateRoot(Guid id) : base(id)
        {
        }

        public void AddTestDomainEvent()
        {
            AddDomainEvent(new TestDomainEvent(Id));
        }
    }

    private sealed class TestDomainEvent : DomainEventBase
    {
        public TestDomainEvent(Guid aggregateId) : base(aggregateId)
        {
        }
    }

    private sealed class TestIntegrationEvent : IntegrationEventBase
    {
        public string Content { get; init; } = "test-content";
    }
}
