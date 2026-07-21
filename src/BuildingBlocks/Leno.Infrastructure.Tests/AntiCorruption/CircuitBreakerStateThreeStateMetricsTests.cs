using FluentAssertions;
using Leno.Infrastructure.AntiCorruption;
using System.Reflection;
using System.Collections.Concurrent;
using Xunit;

namespace Leno.Infrastructure.Tests.AntiCorruption;

/// <summary>
/// P1-T13/T14/T39 验证：CircuitBreakerState 初始状态、三态指标、GetStateUnsafe 提取。
/// </summary>
public class CircuitBreakerStateThreeStateMetricsTests
{
    private static CircuitBreakerState CreateState(int failureThreshold = 3, int successThreshold = 2, int openSeconds = 30)
        => new("three_state_svc", failureThreshold, successThreshold, TimeSpan.FromSeconds(openSeconds));

    /// <summary>
    /// 通过反射获取 AntiCorruptionMetrics._circuitOpenStates 字典中指定 service 的值。
    /// 映射：0=Closed, 1=Open, 2=HalfOpen（保持 Open=1 向后兼容既有测试）。
    /// </summary>
    private static int? GetCircuitStateValue(string serviceName)
    {
        var field = typeof(AntiCorruptionMetrics).GetField("_circuitOpenStates",
            BindingFlags.NonPublic | BindingFlags.Static);
        var dict = (ConcurrentDictionary<string, int>)field!.GetValue(null)!;
        return dict.TryGetValue(serviceName, out var value) ? value : (int?)null;
    }

    private static void CleanupCircuitState(string serviceName)
    {
        var field = typeof(AntiCorruptionMetrics).GetField("_circuitOpenStates",
            BindingFlags.NonPublic | BindingFlags.Static);
        var dict = (ConcurrentDictionary<string, int>)field!.GetValue(null)!;
        dict.TryRemove(serviceName, out _);
    }

    [Fact]
    public void Initial_State_ShouldBe_Closed_Not_HalfOpen()
    {
        // T13: 初始状态必须为 Closed，而非 HalfOpen
        // 原实现 _openedAt = DateTime.MinValue 导致 DateTime.UtcNow - _openedAt 永远大于 OpenDuration
        var serviceName = $"init-{Guid.NewGuid():N}";
        var cb = new CircuitBreakerState(serviceName, failureThreshold: 3, successThreshold: 2, openDuration: TimeSpan.FromSeconds(30));

        cb.GetState().Should().Be(CircuitState.Closed,
            "初始 _openedAt=null 表示从未打开过，状态应为 Closed 而非 HalfOpen");

        CleanupCircuitState(serviceName);
    }

    [Fact]
    public void Initial_State_Metrics_ShouldBe_Closed_Zero()
    {
        // T13+T14: 初始 metrics 应为 0 (Closed)
        var serviceName = $"init-metrics-{Guid.NewGuid():N}";
        var cb = new CircuitBreakerState(serviceName, failureThreshold: 3, successThreshold: 2, openDuration: TimeSpan.FromSeconds(30));

        // 触发一次 RecordSuccess 让 UpdateMetrics 写入 Closed 状态
        cb.RecordSuccess();

        GetCircuitStateValue(serviceName).Should().Be(0,
            "初始 Closed 状态的 metrics 值应为 0");

        CleanupCircuitState(serviceName);
    }

    [Fact]
    public void RecordFailure_AtThreshold_Metrics_ShouldBe_Open_One()
    {
        // T14: Open 状态 metrics 应为 1（保持向后兼容）
        var serviceName = $"open-{Guid.NewGuid():N}";
        var cb = new CircuitBreakerState(serviceName, failureThreshold: 2, successThreshold: 1, openDuration: TimeSpan.FromSeconds(30));

        cb.RecordFailure();
        cb.RecordFailure();

        cb.GetState().Should().Be(CircuitState.Open);
        GetCircuitStateValue(serviceName).Should().Be(1,
            "Open 状态 metrics 值应为 1（保持与既有测试断言兼容）");

        CleanupCircuitState(serviceName);
    }

    [Fact]
    public void HalfOpen_State_Metrics_ShouldBe_Two()
    {
        // T14: HalfOpen 状态 metrics 应为 2（新增，区分 Closed=0 与 HalfOpen）
        var serviceName = $"halfopen-{Guid.NewGuid():N}";
        var cb = new CircuitBreakerState(serviceName, failureThreshold: 1, successThreshold: 2, openDuration: TimeSpan.FromMilliseconds(100));

        cb.RecordFailure(); // 触发 Open
        cb.GetState().Should().Be(CircuitState.Open);
        GetCircuitStateValue(serviceName).Should().Be(1);

        Thread.Sleep(150); // 等待 Open 持续时间过期 → HalfOpen

        cb.GetState().Should().Be(CircuitState.HalfOpen);
        // 触发 UpdateMetrics 写入 HalfOpen 状态
        cb.RecordSuccess();

        GetCircuitStateValue(serviceName).Should().Be(2,
            "HalfOpen 状态 metrics 值应为 2，与 Closed(0) 区分");

        CleanupCircuitState(serviceName);
    }

    [Fact]
    public void HalfOpen_To_Closed_Metrics_Should_Transition_Two_To_Zero()
    {
        // T14: HalfOpen 累计成功阈值后切回 Closed，metrics 应从 2 变为 0
        var serviceName = $"halfopen-to-closed-{Guid.NewGuid():N}";
        var cb = new CircuitBreakerState(serviceName, failureThreshold: 1, successThreshold: 2, openDuration: TimeSpan.FromMilliseconds(100));

        cb.RecordFailure();
        Thread.Sleep(150);

        cb.RecordSuccess(); // HalfOpen, 1 次成功, metrics=2
        GetCircuitStateValue(serviceName).Should().Be(2);

        cb.RecordSuccess(); // HalfOpen, 2 次成功 → Closed, metrics=0
        cb.GetState().Should().Be(CircuitState.Closed);
        GetCircuitStateValue(serviceName).Should().Be(0,
            "HalfOpen 切回 Closed 后 metrics 应为 0");

        CleanupCircuitState(serviceName);
    }

    [Fact]
    public void Dispose_Should_Reset_Metrics_To_Closed_Zero()
    {
        // T14: Dispose 应将 metrics 重置为 0 (Closed)
        var serviceName = $"dispose-{Guid.NewGuid():N}";
        var cb = new CircuitBreakerState(serviceName, failureThreshold: 1, successThreshold: 1, openDuration: TimeSpan.FromSeconds(30));

        cb.RecordFailure();
        GetCircuitStateValue(serviceName).Should().Be(1);

        cb.Dispose();
        GetCircuitStateValue(serviceName).Should().Be(0,
            "Dispose 后 metrics 应重置为 0 (Closed)");

        CleanupCircuitState(serviceName);
    }

    [Fact]
    public void RecordSuccess_In_Closed_Should_Not_Change_State()
    {
        // T39: RecordSuccess 在 Closed 状态下重置失败计数，不影响状态
        var serviceName = $"rs-closed-{Guid.NewGuid():N}";
        var cb = new CircuitBreakerState(serviceName, failureThreshold: 3, successThreshold: 2, openDuration: TimeSpan.FromSeconds(30));

        cb.RecordFailure();
        cb.RecordFailure();
        cb.RecordSuccess(); // Closed 状态重置失败计数

        cb.GetState().Should().Be(CircuitState.Closed);
        GetCircuitStateValue(serviceName).Should().Be(0);

        CleanupCircuitState(serviceName);
    }
}
