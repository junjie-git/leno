using FluentAssertions;
using Leno.Infrastructure.AntiCorruption;
using Microsoft.Extensions.Options;
using Xunit;

namespace Leno.Infrastructure.Tests.AntiCorruption;

/// <summary>
/// CircuitBreakerState IOptionsMonitor 热更新测试（P1-13）。
/// 验证：构造时注入 IOptionsMonitor 后，运行时替换 CurrentValue（模拟 Consul KV 重载），
/// RecordFailure/RecordSuccess/GetState 行为按新阈值生效（原实现构造时冻结阈值，热更新不生效）。
/// </summary>
public class CircuitBreakerStateHotReloadTests
{
    /// <summary>
    /// 可变的 IOptionsMonitor 实现，允许测试运行时替换 CurrentValue 模拟热更新。
    /// 真实场景中 IOptionsMonitor.CurrentValue 由配置变更触发重载返回新对象。
    /// </summary>
    private sealed class MutableOptionsMonitor : IOptionsMonitor<AntiCorruptionOptions>
    {
        public AntiCorruptionOptions CurrentValue { get; set; }

        public MutableOptionsMonitor(AntiCorruptionOptions initial)
        {
            CurrentValue = initial;
        }

        public AntiCorruptionOptions Get(string? name) => CurrentValue;

        public IDisposable? OnChange(Action<AntiCorruptionOptions, string?> listener) => null;
    }

    private static AntiCorruptionOptions CreateOptions(
        int failureThreshold = 3,
        int successThreshold = 2,
        int openDurationSeconds = 30) =>
        new()
        {
            CircuitBreaker = new CircuitBreakerOptions
            {
                FailureThreshold = failureThreshold,
                SuccessThreshold = successThreshold,
                OpenDurationSeconds = openDurationSeconds
            }
        };

    [Fact]
    public void Constructor_WithOptionsMonitor_ShouldReadInitialThresholds()
    {
        // Arrange：初始阈值 FailureThreshold=5
        var monitor = new MutableOptionsMonitor(CreateOptions(failureThreshold: 5, successThreshold: 3, openDurationSeconds: 60));

        // Act
        var cb = new CircuitBreakerState("product", monitor);

        // Assert：构造后初始状态为 Closed，5 次失败才 Open（按初始阈值）
        cb.RecordFailure();
        cb.RecordFailure();
        cb.RecordFailure();
        cb.RecordFailure();
        cb.GetState().Should().Be(CircuitState.Closed); // 4 < 5，仍 Closed

        cb.RecordFailure();
        cb.GetState().Should().Be(CircuitState.Open); // 5 == 5，Open
    }

    [Fact]
    public void RecordFailure_HotUpdateLowersThreshold_ShouldOpenAtNewThreshold()
    {
        // Arrange：初始阈值 5，构造后热更新为 2
        var monitor = new MutableOptionsMonitor(CreateOptions(failureThreshold: 5));
        var cb = new CircuitBreakerState("product", monitor);

        // Act：热更新阈值（模拟 Consul KV 变更触发 IOptionsMonitor 重载，返回新 options 对象）
        monitor.CurrentValue = CreateOptions(failureThreshold: 2, successThreshold: 2, openDurationSeconds: 30);

        // Assert：仅 2 次失败即 Open（按新阈值），而非 5 次
        cb.RecordFailure();
        cb.GetState().Should().Be(CircuitState.Closed); // 1 < 2

        cb.RecordFailure();
        cb.GetState().Should().Be(CircuitState.Open); // 2 == 2，按热更新后阈值 Open
    }

    [Fact]
    public void RecordFailure_HotUpdateRaisesThreshold_ShouldRequireMoreFailuresToOpen()
    {
        // Arrange：初始阈值 2，构造后热更新为 5
        var monitor = new MutableOptionsMonitor(CreateOptions(failureThreshold: 2));
        var cb = new CircuitBreakerState("product", monitor);

        // Act：热更新阈值提高
        monitor.CurrentValue = CreateOptions(failureThreshold: 5, successThreshold: 2, openDurationSeconds: 30);

        // Assert：2 次失败不再 Open（按新阈值 5）
        cb.RecordFailure();
        cb.RecordFailure();
        cb.GetState().Should().Be(CircuitState.Closed); // 2 < 5

        cb.RecordFailure();
        cb.RecordFailure();
        cb.RecordFailure();
        cb.GetState().Should().Be(CircuitState.Open); // 5 == 5
    }

    [Fact]
    public void RecordSuccess_HotUpdateLowersSuccessThreshold_ShouldCloseAtNewThreshold()
    {
        // Arrange：初始 SuccessThreshold=3，先 Open 后 HalfOpen
        var monitor = new MutableOptionsMonitor(CreateOptions(
            failureThreshold: 1,
            successThreshold: 3,
            openDurationSeconds: 1));
        var cb = new CircuitBreakerState("product", monitor);

        // 触发 Open
        cb.RecordFailure();
        cb.GetState().Should().Be(CircuitState.Open);

        // 等待 Open 持续时间过期，进入 HalfOpen
        Thread.Sleep(1100);
        cb.GetState().Should().Be(CircuitState.HalfOpen);

        // Act：热更新 SuccessThreshold=1
        monitor.CurrentValue = CreateOptions(
            failureThreshold: 1,
            successThreshold: 1,
            openDurationSeconds: 1);

        // Assert：HalfOpen 状态下 1 次成功即 Closed（按新阈值），而非 3 次
        cb.RecordSuccess();
        cb.GetState().Should().Be(CircuitState.Closed);
    }

    [Fact]
    public async Task GetState_HotUpdateShortensOpenDuration_ShouldTransitionToHalfOpenFaster()
    {
        // Arrange：初始 OpenDuration=60s，先触发 Open
        var monitor = new MutableOptionsMonitor(CreateOptions(
            failureThreshold: 1,
            successThreshold: 2,
            openDurationSeconds: 60));
        var cb = new CircuitBreakerState("product", monitor);

        cb.RecordFailure();
        cb.GetState().Should().Be(CircuitState.Open);

        // Act：热更新 OpenDuration 为 1 秒
        monitor.CurrentValue = CreateOptions(
            failureThreshold: 1,
            successThreshold: 2,
            openDurationSeconds: 1);

        // 等待 1.1 秒后应转 HalfOpen（按新 OpenDuration）
        await Task.Delay(1100);

        // Assert
        cb.GetState().Should().Be(CircuitState.HalfOpen);
    }

    [Fact]
    public void Constructor_WithNullOptionsMonitor_ShouldThrowArgumentNullException()
    {
        var act = () => new CircuitBreakerState("product", (IOptionsMonitor<AntiCorruptionOptions>)null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithOptionsMonitorCircuitBreakerNull_ShouldFallbackToDefaults()
    {
        // Arrange：CircuitBreaker 为 null，应回退到 CircuitBreakerOptions 默认值 3/2/30
        var monitor = new MutableOptionsMonitor(new AntiCorruptionOptions { CircuitBreaker = null });

        // Act
        var cb = new CircuitBreakerState("product", monitor);

        // Assert：默认 FailureThreshold=3，3 次失败才 Open
        cb.RecordFailure();
        cb.RecordFailure();
        cb.GetState().Should().Be(CircuitState.Closed); // 2 < 3

        cb.RecordFailure();
        cb.GetState().Should().Be(CircuitState.Open); // 3 == 3
    }
}
