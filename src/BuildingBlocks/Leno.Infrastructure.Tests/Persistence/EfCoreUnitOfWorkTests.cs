using Leno.Infrastructure.EventBus;
using Leno.Infrastructure.Outbox;
using Leno.Infrastructure.Persistence;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Leno.Infrastructure.Tests.Persistence;

/// <summary>
/// <see cref="EfCoreUnitOfWork{TDbContext}"/> 单元测试。
/// 验证构造参数校验、<see cref="IUnitOfWork.SaveChangesAsync"/> 委托、
/// <see cref="IUnitOfWork.SaveEntitiesAsync"/> 经发件箱扩展传入 mapper、
/// <see cref="IDisposable.Dispose"/> 释放上下文。
/// </summary>
public class EfCoreUnitOfWorkTests
{
    /// <summary>
    /// 构造时传入 null context 应抛出 <see cref="ArgumentNullException"/>。
    /// </summary>
    [Fact]
    public void Constructor_WithNullContext_ShouldThrow()
    {
        // Arrange
        DbContext? context = null;
        var mapper = new NullIntegrationEventMapper();

        // Act
        var act = () => new EfCoreUnitOfWork<DbContext>(context!, mapper);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("context");
    }

    /// <summary>
    /// 构造时传入 null mapper 应抛出 <see cref="ArgumentNullException"/>。
    /// </summary>
    [Fact]
    public void Constructor_WithNullMapper_ShouldThrow()
    {
        // Arrange
        var context = new Mock<DbContext>().Object;
        IIntegrationEventMapper? mapper = null;

        // Act
        var act = () => new EfCoreUnitOfWork<DbContext>(context, mapper!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("mapper");
    }

    /// <summary>
    /// <see cref="EfCoreUnitOfWork{TDbContext}.SaveChangesAsync"/> 应委托给底层 DbContext。
    /// </summary>
    [Fact]
    public async Task SaveChangesAsync_ShouldDelegateToContext()
    {
        // Arrange
        var contextMock = new Mock<DbContext>();
        contextMock
            .Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(42)
            .Verifiable();
        var uow = new EfCoreUnitOfWork<DbContext>(contextMock.Object, new NullIntegrationEventMapper());

        // Act
        var result = await uow.SaveChangesAsync(CancellationToken.None);

        // Assert：返回值来自 mock 设置，证明调用经 DbContext.SaveChangesAsync 委托
        result.Should().Be(42);
        contextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// <see cref="EfCoreUnitOfWork{TDbContext}.SaveEntitiesAsync"/> 应调用
    /// <see cref="OutboxDbContextExtensions.SaveChangesWithOutboxAsync"/> 并传入 mapper。
    /// 因扩展方法无法被 Moq 直接 mock，使用真实 InMemoryDatabase + 跟踪型 mapper 验证：
    /// mapper 被调用、OutboxMessage 被写入、领域事件被清除。
    /// </summary>
    [Fact]
    public async Task SaveEntitiesAsync_ShouldCallSaveChangesWithOutboxAsyncWithMapper()
    {
        // Arrange
        var dbName = $"uow-saveentities-{Guid.NewGuid()}";
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        await using var context = new TestDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var mapper = new TrackingIntegrationEventMapper();
        var uow = new EfCoreUnitOfWork<TestDbContext>(context, mapper);

        // 添加一个带领域事件的聚合根，触发 OutboxDbContextExtensions 内部 mapper.Map 调用
        var aggregate = new TestAggregateRoot(Guid.NewGuid());
        aggregate.AddTestDomainEvent();
        context.TestAggregates.Add(aggregate);

        // Act
        var result = await uow.SaveEntitiesAsync(CancellationToken.None);

        // Assert
        result.Should().BeTrue("SaveEntitiesAsync 始终返回 true");
        mapper.MapCallCount.Should().Be(1, "聚合根有 1 个领域事件，mapper 应被调用一次");

        var outboxMessages = await context.OutboxMessages.ToListAsync();
        outboxMessages.Should().HaveCount(1, "翻译成功的集成事件应被写入发件箱");

        aggregate.DomainEvents.Should().BeEmpty("SaveEntities 后应清除聚合的领域事件");
    }

    /// <summary>
    /// <see cref="EfCoreUnitOfWork{TDbContext}.Dispose"/> 应释放底层 DbContext。
    /// </summary>
    [Fact]
    public void Dispose_ShouldDisposeContext()
    {
        // Arrange
        var contextMock = new Mock<DbContext>();
        var uow = new EfCoreUnitOfWork<DbContext>(contextMock.Object, new NullIntegrationEventMapper());

        // Act
        uow.Dispose();

        // Assert
        contextMock.Verify(c => c.Dispose(), Times.Once);
    }

    /// <summary>
    /// 测试用 DbContext，承载 <see cref="OutboxMessage"/> 与 <see cref="TestAggregateRoot"/>。
    /// </summary>
    private sealed class TestDbContext : DbContext
    {
        public TestDbContext(DbContextOptions<TestDbContext> options) : base(options)
        {
        }

        public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
        public DbSet<TestAggregateRoot> TestAggregates => Set<TestAggregateRoot>();
    }

    /// <summary>
    /// 跟踪 <see cref="Map"/> 调用次数的测试用 mapper，将任意领域事件翻译为 <see cref="TestIntegrationEvent"/>。
    /// </summary>
    private sealed class TrackingIntegrationEventMapper : IIntegrationEventMapper
    {
        public int MapCallCount { get; private set; }

        public IIntegrationEvent? Map(IDomainEvent domainEvent)
        {
            MapCallCount++;
            return new TestIntegrationEvent();
        }
    }

    /// <summary>
    /// 测试用聚合根，继承 <see cref="AggregateRoot"/> 以触发 <see cref="OutboxDbContextExtensions"/> 的领域事件收集。
    /// </summary>
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

    /// <summary>
    /// 测试用领域事件。
    /// </summary>
    private sealed class TestDomainEvent : DomainEventBase
    {
        public TestDomainEvent(Guid aggregateId) : base(aggregateId)
        {
        }
    }

    /// <summary>
    /// 测试用集成事件。
    /// </summary>
    private sealed class TestIntegrationEvent : IntegrationEventBase
    {
        public string Content { get; init; } = "test-content";
    }
}
