using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Leno.Infrastructure.ReadModel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Leno.Infrastructure.Tests.ReadModel;

/// <summary>
/// <see cref="SqlSnapshotStore{TContext}"/> CRUD 测试，使用 EF Core InMemory 提供程序。
/// 验证快照的保存、读取最新、按类型列出与删除。
/// </summary>
public class SqlSnapshotStoreTests
{
    /// <summary>
    /// 测试用读模型，模拟订单读模型的最小视图。
    /// </summary>
    public sealed class FakeReadModel
    {
        public string Id { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public long Version { get; set; }
    }

    /// <summary>
    /// 测试用 DbContext，注册 <see cref="ReadModelSnapshot"/> 实体配置。
    /// </summary>
    public sealed class TestSnapshotDbContext : DbContext
    {
        public DbSet<ReadModelSnapshot> ReadModelSnapshots => Set<ReadModelSnapshot>();

        public TestSnapshotDbContext(DbContextOptions<TestSnapshotDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            ArgumentNullException.ThrowIfNull(modelBuilder);
            modelBuilder.ApplyConfiguration(new ReadModelSnapshotConfiguration());
        }
    }

    private static SqlSnapshotStore<TestSnapshotDbContext> CreateStore(out TestSnapshotDbContext context)
    {
        var options = new DbContextOptionsBuilder<TestSnapshotDbContext>()
            .UseInMemoryDatabase($"snapshot-{Guid.NewGuid():N}")
            .Options;
        context = new TestSnapshotDbContext(options);
        context.Database.EnsureCreated();
        return new SqlSnapshotStore<TestSnapshotDbContext>(context, NullLogger<SqlSnapshotStore<TestSnapshotDbContext>>.Instance);
    }

    [Fact]
    public async Task GetLatestAsync_NoSnapshot_ReturnsNull()
    {
        var store = CreateStore(out var context);
        await using var _ = context;

        var result = await store.GetLatestAsync<FakeReadModel>("agg-1", CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task SaveAsync_NewSnapshot_PersistsAndCanBeRead()
    {
        var store = CreateStore(out var context);
        await using var _ = context;

        var state = new FakeReadModel { Id = "agg-1", Status = "Paid", TotalAmount = 99.5m, Version = 5 };
        await store.SaveAsync("agg-1", state, 5, CancellationToken.None);

        var latest = await store.GetLatestAsync<FakeReadModel>("agg-1", CancellationToken.None);

        latest.Should().NotBeNull();
        latest!.AggregateId.Should().Be("agg-1");
        latest.Version.Should().Be(5);
        latest.State.Id.Should().Be("agg-1");
        latest.State.Status.Should().Be("Paid");
        latest.State.TotalAmount.Should().Be(99.5m);
        latest.State.Version.Should().Be(5);
    }

    [Fact]
    public async Task GetLatestAsync_MultipleSnapshots_ReturnsHighestVersion()
    {
        var store = CreateStore(out var context);
        await using var _ = context;

        await store.SaveAsync("agg-1", new FakeReadModel { Id = "agg-1", Status = "Created", Version = 1 }, 1, CancellationToken.None);
        await store.SaveAsync("agg-1", new FakeReadModel { Id = "agg-1", Status = "Shipped", Version = 30 }, 30, CancellationToken.None);
        await store.SaveAsync("agg-1", new FakeReadModel { Id = "agg-1", Status = "Paid", Version = 10 }, 10, CancellationToken.None);

        var latest = await store.GetLatestAsync<FakeReadModel>("agg-1", CancellationToken.None);

        latest.Should().NotBeNull();
        latest!.Version.Should().Be(30);
        latest.State.Status.Should().Be("Shipped");
    }

    [Fact]
    public async Task SaveAsync_SameAggregateAndVersion_OverwritesExisting()
    {
        var store = CreateStore(out var context);
        await using var _ = context;

        await store.SaveAsync("agg-1", new FakeReadModel { Id = "agg-1", Status = "Created", Version = 5 }, 5, CancellationToken.None);
        await store.SaveAsync("agg-1", new FakeReadModel { Id = "agg-1", Status = "Completed", Version = 5 }, 5, CancellationToken.None);

        var all = await context.ReadModelSnapshots
            .Where(s => s.AggregateId == "agg-1" && s.Version == 5)
            .ToListAsync(CancellationToken.None);
        all.Should().HaveCount(1);

        var latest = await store.GetLatestAsync<FakeReadModel>("agg-1", CancellationToken.None);
        latest!.State.Status.Should().Be("Completed");
    }

    [Fact]
    public async Task SaveAsync_DifferentAggregates_AreIsolated()
    {
        var store = CreateStore(out var context);
        await using var _ = context;

        await store.SaveAsync("agg-1", new FakeReadModel { Id = "agg-1", Status = "Paid", Version = 3 }, 3, CancellationToken.None);
        await store.SaveAsync("agg-2", new FakeReadModel { Id = "agg-2", Status = "Shipped", Version = 7 }, 7, CancellationToken.None);

        var latest1 = await store.GetLatestAsync<FakeReadModel>("agg-1", CancellationToken.None);
        var latest2 = await store.GetLatestAsync<FakeReadModel>("agg-2", CancellationToken.None);

        latest1!.Version.Should().Be(3);
        latest1.State.Status.Should().Be("Paid");
        latest2!.Version.Should().Be(7);
        latest2.State.Status.Should().Be("Shipped");
    }

    [Fact]
    public async Task ListSnapshotsAsync_ReturnsDescriptorsByAggregateType()
    {
        var store = CreateStore(out var context);
        await using var _ = context;

        // SaveAsync 用 typeof(T).Name 作为 AggregateType，此处为 FakeReadModel
        await store.SaveAsync("agg-1", new FakeReadModel { Id = "agg-1", Status = "A", Version = 1 }, 1, CancellationToken.None);
        await store.SaveAsync("agg-1", new FakeReadModel { Id = "agg-1", Status = "B", Version = 2 }, 2, CancellationToken.None);
        await store.SaveAsync("agg-2", new FakeReadModel { Id = "agg-2", Status = "C", Version = 1 }, 1, CancellationToken.None);

        var descriptors = await store.ListSnapshotsAsync(nameof(FakeReadModel), CancellationToken.None);

        descriptors.Should().HaveCount(3);
        descriptors.Should().OnlyContain(d => d.AggregateType == nameof(FakeReadModel));
        // 按 Version 降序排列
        descriptors[0].Version.Should().Be(2);
        descriptors.Select(d => d.AggregateId).Distinct().Should().BeEquivalentTo(new[] { "agg-1", "agg-2" });
    }

    [Fact]
    public async Task ListSnapshotsAsync_NoMatches_ReturnsEmpty()
    {
        var store = CreateStore(out var context);
        await using var _ = context;

        var descriptors = await store.ListSnapshotsAsync("NonExistentType", CancellationToken.None);

        descriptors.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteAsync_RemovesSpecifiedVersion()
    {
        var store = CreateStore(out var context);
        await using var _ = context;

        await store.SaveAsync("agg-1", new FakeReadModel { Id = "agg-1", Status = "A", Version = 1 }, 1, CancellationToken.None);
        await store.SaveAsync("agg-1", new FakeReadModel { Id = "agg-1", Status = "B", Version = 2 }, 2, CancellationToken.None);

        await store.DeleteAsync("agg-1", 1, CancellationToken.None);

        var remaining = await context.ReadModelSnapshots
            .Where(s => s.AggregateId == "agg-1")
            .ToListAsync(CancellationToken.None);
        remaining.Should().HaveCount(1);
        remaining[0].Version.Should().Be(2);
    }

    [Fact]
    public async Task DeleteAsync_NonExistent_NoOp()
    {
        var store = CreateStore(out var context);
        await using var _ = context;

        await store.SaveAsync("agg-1", new FakeReadModel { Id = "agg-1", Status = "A", Version = 1 }, 1, CancellationToken.None);

        // 删除不存在的版本不应抛异常
        var act = async () => await store.DeleteAsync("agg-1", 999, CancellationToken.None);
        await act.Should().NotThrowAsync();

        var latest = await store.GetLatestAsync<FakeReadModel>("agg-1", CancellationToken.None);
        latest.Should().NotBeNull();
        latest!.Version.Should().Be(1);
    }

    [Fact]
    public async Task SaveAsync_NegativeVersion_Throws()
    {
        var store = CreateStore(out var context);
        await using var _ = context;

        var state = new FakeReadModel { Id = "agg-1", Status = "A", Version = -1 };
        var act = async () => await store.SaveAsync("agg-1", state, -1, CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }
}
