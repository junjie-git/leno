using Leno.Infrastructure.Abstractions;
using Leno.Infrastructure.EventBus;
using Leno.SharedContracts.Events;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Collections.Concurrent;

namespace Leno.Infrastructure.Tests.EventBus;

/// <summary>
/// 验证 IntegrationEventConsumerBase 的幂等检查是原子的（TryMarkAsProcessing 原子获取）。
/// </summary>
public class IntegrationEventConsumerAtomicityTests
{
    [Fact]
    public async Task ConcurrentConsume_SameEvent_ShouldOnlyProcessOnce()
    {
        // Arrange — 使用原子 TryMarkAsProcessing 的 IIdempotencyStore
        var processedKeys = new ConcurrentDictionary<string, byte>();
        var processingKeys = new ConcurrentDictionary<string, byte>();

        var store = new Mock<IIdempotencyStore>();
        // 关键：显式启用原子路径（DIM 默认 false，Mock 默认返回 false）
        store.SetupGet(x => x.SupportsAtomicProcessing).Returns(true);

        // 模拟原子 TryMarkAsProcessing：ConcurrentDictionary.TryAdd 保证只有一个线程成功
        store.Setup(x => x.TryMarkAsProcessingAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid eventId, CancellationToken _) =>
                processingKeys.TryAdd(eventId.ToString(), 0));

        // 处理失败时释放锁（本次测试无失败路径，但仍需 setup 以覆盖 catch 分支）
        store.Setup(x => x.ReleaseProcessingLockAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        store.Setup(x => x.MarkAsProcessedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        store.Setup(x => x.IsProcessedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid eventId, CancellationToken _) =>
                processedKeys.ContainsKey(eventId.ToString()));

        var executionCount = 0;
        var consumer = new TestIntegrationEventConsumer(
            store.Object, () => Interlocked.Increment(ref executionCount));

        var evt = new TestIntegrationEvent { EventId = Guid.NewGuid() };
        var context = MockConsumeContext(evt);

        // Act — 10 个并发消费者同时处理同一事件
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => consumer.Consume(context))
            .ToArray();
        await Task.WhenAll(tasks);

        // Assert — HandleAsync 只执行一次
        executionCount.Should().Be(1, "原子幂等检查应保证同一事件只处理一次");
    }

    [Fact]
    public async Task Consume_SupportsAtomicProcessingFalse_ShouldFallBackToPreCheckPath()
    {
        // Arrange — 不支持原子操作（DIM 默认）：走 IsProcessedAsync 预检查 + TryAcquireProcessingLockAsync 返回 true 的兼容路径
        var store = new Mock<IIdempotencyStore>();
        // SupportsAtomicProcessing 默认 false（Mock 不 setup 即返回 default）

        store.Setup(x => x.IsProcessedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        store.Setup(x => x.MarkAsProcessedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var executionCount = 0;
        var consumer = new TestIntegrationEventConsumer(
            store.Object, () => Interlocked.Increment(ref executionCount));

        var evt = new TestIntegrationEvent { EventId = Guid.NewGuid() };
        var context = MockConsumeContext(evt);

        // Act
        await consumer.Consume(context);

        // Assert — 兼容路径下 HandleAsync 应执行一次（无并发场景）
        executionCount.Should().Be(1);
        // 不应调用 TryMarkAsProcessingAsync（因为 SupportsAtomicProcessing 为 false）
        store.Verify(
            x => x.TryMarkAsProcessingAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Consume_AlreadyProcessed_ShouldSkipHandle()
    {
        // Arrange — 已处理事件应跳过 HandleAsync
        var store = new Mock<IIdempotencyStore>();
        store.SetupGet(x => x.SupportsAtomicProcessing).Returns(true);
        store.Setup(x => x.IsProcessedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var executionCount = 0;
        var consumer = new TestIntegrationEventConsumer(
            store.Object, () => Interlocked.Increment(ref executionCount));

        var evt = new TestIntegrationEvent { EventId = Guid.NewGuid() };
        var context = MockConsumeContext(evt);

        // Act
        await consumer.Consume(context);

        // Assert — 已处理则不进入 HandleAsync
        executionCount.Should().Be(0);
        // 既已处理，不应再尝试获取处理锁
        store.Verify(
            x => x.TryMarkAsProcessingAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Consume_HandleThrows_ShouldReleaseLockAndRethrow()
    {
        // Arrange — HandleAsync 抛异常应释放处理锁并重新抛出
        var store = new Mock<IIdempotencyStore>();
        store.SetupGet(x => x.SupportsAtomicProcessing).Returns(true);

        store.Setup(x => x.IsProcessedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        store.Setup(x => x.TryMarkAsProcessingAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        store.Setup(x => x.ReleaseProcessingLockAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var consumer = new TestIntegrationEventConsumer(
            store.Object, () => throw new InvalidOperationException("boom"));

        var evt = new TestIntegrationEvent { EventId = Guid.NewGuid() };
        var context = MockConsumeContext(evt);

        // Act
        var act = () => consumer.Consume(context);

        // Assert — 异常向上抛出
        await act.Should().ThrowAsync<InvalidOperationException>();
        // 释放锁被调用一次
        store.Verify(
            x => x.ReleaseProcessingLockAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Once);
        // 处理失败，不应标记为已处理
        store.Verify(
            x => x.MarkAsProcessedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Consume_EventIdEmpty_ShouldThrow()
    {
        // Arrange — EventId 为 Guid.Empty 时应拒绝消费
        var store = new Mock<IIdempotencyStore>();
        var consumer = new TestIntegrationEventConsumer(
            store.Object, () => { });

        var evt = new TestIntegrationEvent { EventId = Guid.Empty };
        var context = MockConsumeContext(evt);

        // Act
        var act = () => consumer.Consume(context);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    private static ConsumeContext<TestIntegrationEvent> MockConsumeContext(TestIntegrationEvent evt)
    {
        var mock = new Mock<ConsumeContext<TestIntegrationEvent>>();
        mock.SetupGet(x => x.Message).Returns(evt);
        mock.SetupGet(x => x.CancellationToken).Returns(CancellationToken.None);
        return mock.Object;
    }

    private sealed class TestIntegrationEvent : IIntegrationEvent
    {
        public Guid EventId { get; set; }
        public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
        public string IdempotencyKey { get; set; } = string.Empty;
    }

    private sealed class TestIntegrationEventConsumer : IntegrationEventConsumerBase<TestIntegrationEvent>
    {
        private readonly Action _onHandle;

        public TestIntegrationEventConsumer(IIdempotencyStore store, Action onHandle)
            : base(NullLogger.Instance, store)
        {
            _onHandle = onHandle;
        }

        protected override Task HandleAsync(TestIntegrationEvent integrationEvent, CancellationToken ct)
        {
            _onHandle();
            return Task.CompletedTask;
        }
    }
}
