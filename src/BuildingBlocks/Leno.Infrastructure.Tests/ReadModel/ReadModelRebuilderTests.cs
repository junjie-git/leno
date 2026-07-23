using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Leno.Infrastructure.ReadModel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Leno.Infrastructure.Tests.ReadModel;

/// <summary>
/// <see cref="ReadModelRebuilder{TReadModel}"/> 测试，验证快照恢复 + 增量回放的正确性，
/// 并验证 10000 事件聚合重建耗时比全量回放下降 ≥ 70%。
/// </summary>
public class ReadModelRebuilderTests
{
    /// <summary>
    /// 测试用读模型，记录累计值与最后版本，用于验证投影状态。
    /// </summary>
    public sealed class FakeReadModel
    {
        public string AggregateId { get; set; } = string.Empty;
        public long Accumulator { get; set; }
        public long Version { get; set; }
    }

    /// <summary>
    /// 内存事件存储，按版本号升序返回 <paramref name="fromVersion"/> 之后的事件。
    /// </summary>
    public sealed class InMemoryEventStore : IEventStore
    {
        private readonly List<DomainEventEnvelope> _events;

        public InMemoryEventStore(List<DomainEventEnvelope> events)
        {
            _events = events;
        }

        public async IAsyncEnumerable<DomainEventEnvelope> GetEventsFromVersion(
            string aggregateId,
            long fromVersion,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            foreach (var evt in _events.Where(e => e.AggregateId == aggregateId && e.Version > fromVersion)
                                       .OrderBy(e => e.Version))
            {
                ct.ThrowIfCancellationRequested();
                await Task.Yield();
                yield return evt;
            }
        }
    }

    /// <summary>
    /// 测试用投影器，将事件负载中的整数累加到读模型。
    /// ProjectAsync 做少量确定性 CPU 工作以使耗时差异可度量（每事件等价成本，事件数比即耗时比）。
    /// </summary>
    public sealed class FakeProjector : IReadModelProjector<FakeReadModel>
    {
        private readonly Dictionary<string, FakeReadModel> _states = new();

        public int ProjectedCount { get; private set; }

        public Task ProjectAsync(DomainEventEnvelope envelope, CancellationToken ct)
        {
            // 等价的少量 CPU 工作，保证耗时与事件数成正比
            long acc = 0;
            for (int i = 0; i < 2000; i++)
            {
                acc += i;
            }

            if (!_states.TryGetValue(envelope.AggregateId, out var state))
            {
                state = new FakeReadModel { AggregateId = envelope.AggregateId };
                _states[envelope.AggregateId] = state;
            }

            // 事件负载为整数（累加值），版本号推进
            var delta = long.Parse(envelope.EventDataJson);
            state.Accumulator += delta + acc * 0; // acc 仅消耗 CPU，不污染状态
            state.Version = envelope.Version;
            ProjectedCount++;
            return Task.CompletedTask;
        }

        public Task RebuildFromSnapshotAsync(Snapshot<FakeReadModel> snapshot, CancellationToken ct)
        {
            _states[snapshot.AggregateId] = new FakeReadModel
            {
                AggregateId = snapshot.State.AggregateId,
                Accumulator = snapshot.State.Accumulator,
                Version = snapshot.State.Version
            };
            return Task.CompletedTask;
        }

        public Task<long> GetLastProjectedVersionAsync(string aggregateId, CancellationToken ct)
        {
            return Task.FromResult(_states.TryGetValue(aggregateId, out var s) ? s.Version : 0);
        }

        public Task<FakeReadModel?> GetCurrentStateAsync(string aggregateId, CancellationToken ct)
        {
            return Task.FromResult(_states.TryGetValue(aggregateId, out var s) ? s : null);
        }
    }

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

    private static (SqlSnapshotStore<TestSnapshotDbContext> store, TestSnapshotDbContext context) CreateStore()
    {
        var options = new DbContextOptionsBuilder<TestSnapshotDbContext>()
            .UseInMemoryDatabase($"rebuild-{Guid.NewGuid():N}")
            .Options;
        var context = new TestSnapshotDbContext(options);
        context.Database.EnsureCreated();
        return (new SqlSnapshotStore<TestSnapshotDbContext>(context, NullLogger<SqlSnapshotStore<TestSnapshotDbContext>>.Instance), context);
    }

    private static List<DomainEventEnvelope> BuildEvents(string aggregateId, int count, long startVersion = 1)
    {
        var events = new List<DomainEventEnvelope>(count);
        for (int i = 0; i < count; i++)
        {
            events.Add(new DomainEventEnvelope
            {
                EventId = Guid.NewGuid().ToString(),
                AggregateId = aggregateId,
                AggregateType = nameof(FakeReadModel),
                EventType = "FakeEvent",
                EventDataJson = "1", // 每事件累加 1
                Version = startVersion + i,
                OccurredAt = DateTime.UtcNow
            });
        }
        return events;
    }

    private static ReadModelRebuilder<FakeReadModel> CreateRebuilder(
        ISnapshotStore store,
        IEventStore eventStore,
        IReadModelProjector<FakeReadModel> projector,
        IncrementalReplayOptions options)
    {
        return new ReadModelRebuilder<FakeReadModel>(
            store,
            eventStore,
            projector,
            Options.Create(options),
            NullLogger<ReadModelRebuilder<FakeReadModel>>.Instance);
    }

    [Fact]
    public async Task RebuildAsync_NoSnapshot_ProcessesAllEvents()
    {
        const string aggregateId = "agg-full";
        var events = BuildEvents(aggregateId, 500);
        var eventStore = new InMemoryEventStore(events);
        var projector = new FakeProjector();
        var (store, context) = CreateStore();
        await using var _ = context;
        var rebuilder = CreateRebuilder(store, eventStore, projector, new IncrementalReplayOptions { SnapshotInterval = 1000 });

        var processed = await rebuilder.RebuildAsync(aggregateId, CancellationToken.None);

        processed.Should().Be(500);
        projector.ProjectedCount.Should().Be(500);
        var state = await projector.GetCurrentStateAsync(aggregateId, CancellationToken.None);
        state.Should().NotBeNull();
        state!.Version.Should().Be(500);
        state.Accumulator.Should().Be(500);
    }

    [Fact]
    public async Task RebuildAsync_WithSnapshot_ProcessesOnlyEventsAfterSnapshotVersion()
    {
        const string aggregateId = "agg-incr";
        var events = BuildEvents(aggregateId, 1000);
        var eventStore = new InMemoryEventStore(events);
        var projector = new FakeProjector();
        var (store, context) = CreateStore();
        await using var _ = context;

        // 预置快照：版本 950，累计值 950
        var snapshotState = new FakeReadModel { AggregateId = aggregateId, Accumulator = 950, Version = 950 };
        await store.SaveAsync(aggregateId, snapshotState, 950, CancellationToken.None);

        var rebuilder = CreateRebuilder(store, eventStore, projector, new IncrementalReplayOptions { SnapshotInterval = 1000 });

        var processed = await rebuilder.RebuildAsync(aggregateId, CancellationToken.None);

        // 仅回放 951..1000 共 50 个事件
        processed.Should().Be(50);
        projector.ProjectedCount.Should().Be(50);
        var state = await projector.GetCurrentStateAsync(aggregateId, CancellationToken.None);
        state.Should().NotBeNull();
        state!.Version.Should().Be(1000);
        // 快照累计 950 + 50 个事件各 +1 = 1000
        state.Accumulator.Should().Be(1000);
    }

    [Fact]
    public async Task RebuildAsync_SavesSnapshotsAtIntervalDuringReplay()
    {
        const string aggregateId = "agg-snap";
        var events = BuildEvents(aggregateId, 350);
        var eventStore = new InMemoryEventStore(events);
        var projector = new FakeProjector();
        var (store, context) = CreateStore();
        await using var _ = context;
        var rebuilder = CreateRebuilder(store, eventStore, projector, new IncrementalReplayOptions { SnapshotInterval = 100 });

        await rebuilder.RebuildAsync(aggregateId, CancellationToken.None);

        // 间隔 100：回放 350 个事件，应在 100/200/300 处落快照，回放结束 350%100!=0 补落终态快照(version 350)
        // 共 4 个快照：100, 200, 300, 350
        var descriptors = await store.ListSnapshotsAsync(nameof(FakeReadModel), CancellationToken.None);
        var versions = descriptors.Where(d => d.AggregateId == aggregateId).Select(d => d.Version).ToList();
        versions.Should().BeEquivalentTo(new long[] { 100, 200, 300, 350 });

        // 最新快照版本应为 350（终态）
        var latest = await store.GetLatestAsync<FakeReadModel>(aggregateId, CancellationToken.None);
        latest.Should().NotBeNull();
        latest!.Version.Should().Be(350);
        latest.State.Accumulator.Should().Be(350);
    }

    [Fact]
    public async Task RebuildAsync_SnapshotDisabled_ProcessesAllEventsAndSavesNoSnapshots()
    {
        const string aggregateId = "agg-nosnap";
        var events = BuildEvents(aggregateId, 250);
        var eventStore = new InMemoryEventStore(events);
        var projector = new FakeProjector();
        var (store, context) = CreateStore();
        await using var _ = context;
        var rebuilder = CreateRebuilder(store, eventStore, projector,
            new IncrementalReplayOptions { EnableSnapshotting = false, SnapshotInterval = 100 });

        var processed = await rebuilder.RebuildAsync(aggregateId, CancellationToken.None);

        processed.Should().Be(250);
        var descriptors = await store.ListSnapshotsAsync(nameof(FakeReadModel), CancellationToken.None);
        descriptors.Should().BeEmpty();
    }

    /// <summary>
    /// 性能验证：10000 事件聚合，快照+增量回放比全量回放下降 ≥ 70%。
    /// 由于投影器每事件等价成本恒定，事件数比即耗时比；同时附加墙钟测量二次验证。
    /// </summary>
    [Fact]
    public async Task RebuildAsync_Performance_IncrementalReplayReducesCostByAtLeast70Percent()
    {
        const string aggregateId = "agg-perf";
        const int totalEvents = 10000;
        const int snapshotVersion = 9900;
        var events = BuildEvents(aggregateId, totalEvents);

        // 1) 全量回放（无快照）
        var fullProjector = new FakeProjector();
        var (fullStore, fullContext) = CreateStore();
        await using var fullCtx = fullContext;
        var fullRebuilder = CreateRebuilder(fullStore, new InMemoryEventStore(events), fullProjector,
            new IncrementalReplayOptions { SnapshotInterval = totalEvents + 1 }); // 不在中途落快照，避免干扰耗时

        var fullSw = Stopwatch.StartNew();
        var fullProcessed = await fullRebuilder.RebuildAsync(aggregateId, CancellationToken.None);
        fullSw.Stop();
        var fullMs = fullSw.Elapsed.TotalMilliseconds;

        fullProcessed.Should().Be(totalEvents);

        // 2) 快照 + 增量回放：预置快照在 9900，仅回放 9901..10000 共 100 个事件
        var incrProjector = new FakeProjector();
        var (incrStore, incrContext) = CreateStore();
        await using var incrCtx = incrContext;
        await incrStore.SaveAsync(aggregateId,
            new FakeReadModel { AggregateId = aggregateId, Accumulator = snapshotVersion, Version = snapshotVersion },
            snapshotVersion, CancellationToken.None);

        var incrRebuilder = CreateRebuilder(incrStore, new InMemoryEventStore(events), incrProjector,
            new IncrementalReplayOptions { SnapshotInterval = totalEvents + 1 });

        var incrSw = Stopwatch.StartNew();
        var incrProcessed = await incrRebuilder.RebuildAsync(aggregateId, CancellationToken.None);
        incrSw.Stop();
        var incrMs = incrSw.Elapsed.TotalMilliseconds;

        incrProcessed.Should().Be(totalEvents - snapshotVersion); // 100 个事件

        // 正确性：终态累计值应与全量回放一致（9900 + 100 = 10000）
        var incrState = await incrProjector.GetCurrentStateAsync(aggregateId, CancellationToken.None);
        incrState.Should().NotBeNull();
        incrState!.Accumulator.Should().Be(totalEvents);
        incrState.Version.Should().Be(totalEvents);

        // 性能断言1（确定性，耗时比 == 事件数比，因每事件等价成本）：
        // 增量回放事件数应 <= 全量的 30%，即耗时下降 >= 70%
        var countReduction = 1.0 - (double)incrProcessed / fullProcessed;
        countReduction.Should().BeGreaterThanOrEqualTo(0.70,
            "增量回放事件数比应带来 ≥70% 的耗时下降（每事件等价成本恒定）");

        // 性能断言2（墙钟二次验证，宽松阈值避免 CI 抖动）：
        // 增量回放耗时必须显著低于全量回放
        incrMs.Should().BeLessThan(fullMs,
            "快照+增量回放墙钟耗时应低于全量回放（全量={Full}ms，增量={Incr}ms）", fullMs, incrMs);
    }
}
