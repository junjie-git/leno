using Leno.Infrastructure.AntiCorruption;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using System.Collections.Concurrent;
using System.Reflection;

namespace Leno.Infrastructure.Tests.AntiCorruption;

/// <summary>
/// AntiCorruptionDispatcher.Dispose 不应销毁 KeyedSingleton CircuitBreakerState。
/// 验证 P0-T10：Scoped Dispatcher.Dispose 不调用 _circuitBreaker.Dispose()，
/// 避免同进程其他 Scope 的熔断器指标状态丢失。
/// </summary>
public class AntiCorruptionDispatcherDisposeTests
{
    /// <summary>
    /// 测试用服务接口，用于构造 AntiCorruptionDispatcher&lt;TService&gt;。
    /// </summary>
    public interface ITestService
    {
        Task<string> GetValueAsync(CancellationToken ct);
    }

    private sealed class HttpImpl : ITestService
    {
        public Task<string> GetValueAsync(CancellationToken ct) => Task.FromResult("http-value");
    }

    private static IOptionsMonitor<AntiCorruptionOptions> CreateOptionsMonitor(bool useGrpc)
    {
        var mock = new Mock<IOptionsMonitor<AntiCorruptionOptions>>();
        mock.SetupGet(o => o.CurrentValue).Returns(new AntiCorruptionOptions { UseGrpc = useGrpc });
        return mock.Object;
    }

    /// <summary>
    /// 通过反射获取 AntiCorruptionMetrics._circuitOpenStates 字典中指定 service 的值。
    /// </summary>
    private static int? GetCircuitOpenStateValue(string serviceName)
    {
        var field = typeof(AntiCorruptionMetrics).GetField("_circuitOpenStates",
            BindingFlags.NonPublic | BindingFlags.Static);
        var dict = (ConcurrentDictionary<string, int>)field!.GetValue(null)!;
        return dict.TryGetValue(serviceName, out var value) ? value : (int?)null;
    }

    /// <summary>
    /// 通过反射清理 AntiCorruptionMetrics._circuitOpenStates 中指定 service 的条目，
    /// 避免影响其他测试。
    /// </summary>
    private static void CleanupCircuitOpenState(string serviceName)
    {
        var field = typeof(AntiCorruptionMetrics).GetField("_circuitOpenStates",
            BindingFlags.NonPublic | BindingFlags.Static);
        var dict = (ConcurrentDictionary<string, int>)field!.GetValue(null)!;
        dict.TryRemove(serviceName, out _);
    }

    [Fact]
    public void Dispose_ShouldNotDisposeKeyedSingletonCircuitBreaker()
    {
        // Arrange — 使用唯一 service 名避免与其他测试冲突
        var serviceName = $"dispose-test-{Guid.NewGuid():N}";
        var circuitBreaker = new CircuitBreakerState(
            serviceName, failureThreshold: 2, successThreshold: 1, openDuration: TimeSpan.FromSeconds(30));

        // 记录足够失败次数打开熔断器
        circuitBreaker.RecordFailure();
        circuitBreaker.RecordFailure();

        // 验证熔断器已打开
        circuitBreaker.GetState().Should().Be(CircuitState.Open,
            "记录 2 次失败（阈值 2）后熔断器应打开");

        // 验证 metrics 已记录 Open 状态
        GetCircuitOpenStateValue(serviceName).Should().Be(1,
            "熔断器打开后 metrics 应记录 1");

        var dispatcher = new AntiCorruptionDispatcher<ITestService>(
            new HttpImpl(),
            grpcImplementation: null,
            CreateOptionsMonitor(useGrpc: false),
            NullLogger<AntiCorruptionDispatcher<ITestService>>.Instance,
            serviceName,
            circuitBreaker);

        // Act — Dispose Dispatcher
        dispatcher.Dispose();

        // Assert — KeyedSingleton CircuitBreakerState 未被 Dispose，metrics 状态保持
        GetCircuitOpenStateValue(serviceName).Should().Be(1,
            "Scoped Dispatcher.Dispose 不应销毁 KeyedSingleton 的 CircuitBreakerState，metrics 应保持 Open=1");

        // 熔断器状态机仍可用
        circuitBreaker.GetState().Should().Be(CircuitState.Open,
            "熔断器状态机应仍然可用且状态不变");

        // Cleanup
        CleanupCircuitOpenState(serviceName);
    }

    [Fact]
    public void Dispose_MultipleDispatchers_SharingSameCircuitBreaker_ShouldNotDisposeIt()
    {
        // Arrange — 模拟两个 Scoped Dispatcher 共享同一个 KeyedSingleton CircuitBreakerState
        var serviceName = $"shared-cb-{Guid.NewGuid():N}";
        var sharedCircuitBreaker = new CircuitBreakerState(
            serviceName, failureThreshold: 1, successThreshold: 1, openDuration: TimeSpan.FromSeconds(30));

        // 打开熔断器
        sharedCircuitBreaker.RecordFailure();
        sharedCircuitBreaker.GetState().Should().Be(CircuitState.Open);
        GetCircuitOpenStateValue(serviceName).Should().Be(1);

        // 第一个 Scope 的 Dispatcher
        var dispatcher1 = new AntiCorruptionDispatcher<ITestService>(
            new HttpImpl(), null, CreateOptionsMonitor(false),
            NullLogger<AntiCorruptionDispatcher<ITestService>>.Instance,
            serviceName, sharedCircuitBreaker);

        // Act — 第一个 Dispatcher Dispose
        dispatcher1.Dispose();

        // Assert — 共享的 CircuitBreakerState 仍可用，metrics 未被清除
        GetCircuitOpenStateValue(serviceName).Should().Be(1,
            "第一个 Dispatcher Dispose 后，共享 CircuitBreakerState 的 metrics 不应被清除");

        // 第二个 Scope 的 Dispatcher 仍能正常使用共享的 CircuitBreakerState
        var dispatcher2 = new AntiCorruptionDispatcher<ITestService>(
            new HttpImpl(), null, CreateOptionsMonitor(false),
            NullLogger<AntiCorruptionDispatcher<ITestService>>.Instance,
            serviceName, sharedCircuitBreaker);

        var state = sharedCircuitBreaker.GetState();
        state.Should().Be(CircuitState.Open,
            "第二个 Scope 的 Dispatcher 应能正常使用共享的 CircuitBreakerState");

        dispatcher2.Dispose();

        // 最终 metrics 仍保持
        GetCircuitOpenStateValue(serviceName).Should().Be(1,
            "两个 Dispatcher 都 Dispose 后，共享 CircuitBreakerState 的 metrics 仍不应被清除");

        // Cleanup
        CleanupCircuitOpenState(serviceName);
    }

    [Fact]
    public async Task Dispose_CircuitBreakerStillFunctional_AfterDispatcherDispose()
    {
        // Arrange — Dispose 后 CircuitBreakerState 应仍能正常记录成功/失败并更新状态
        var serviceName = $"functional-{Guid.NewGuid():N}";
        var circuitBreaker = new CircuitBreakerState(
            serviceName, failureThreshold: 3, successThreshold: 2, openDuration: TimeSpan.FromMilliseconds(100));

        var dispatcher = new AntiCorruptionDispatcher<ITestService>(
            new HttpImpl(), null, CreateOptionsMonitor(false),
            NullLogger<AntiCorruptionDispatcher<ITestService>>.Instance,
            serviceName, circuitBreaker);

        // Act
        dispatcher.Dispose();

        // Assert — CircuitBreakerState 仍可正常使用（未被 Dispose）
        // 记录失败不应抛 ObjectDisposedException
        var act = () =>
        {
            circuitBreaker.RecordFailure();
            circuitBreaker.RecordSuccess();
            return Task.CompletedTask;
        };

        await act.Should().NotThrowAsync(
            "CircuitBreakerState 未被 Dispatcher Dispose 销毁，应仍可正常调用");

        circuitBreaker.GetState().Should().Be(CircuitState.Closed,
            "记录 1 次失败 + 1 次成功（阈值 3）后熔断器应仍为 Closed");

        // Cleanup
        CleanupCircuitOpenState(serviceName);
    }
}
