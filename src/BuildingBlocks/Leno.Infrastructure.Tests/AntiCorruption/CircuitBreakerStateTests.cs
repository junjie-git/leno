using FluentAssertions;
using Leno.Infrastructure.AntiCorruption;
using Xunit;

namespace Leno.Infrastructure.Tests.AntiCorruption;

public class CircuitBreakerStateTests
{
    private static CircuitBreakerState CreateState(int failureThreshold = 3, int successThreshold = 2, int openSeconds = 30)
        => new("test_service", failureThreshold, successThreshold, TimeSpan.FromSeconds(openSeconds));

    [Fact]
    public void Initial_State_Is_Closed()
    {
        var cb = CreateState();
        cb.GetState().Should().Be(CircuitState.Closed);
    }

    [Fact]
    public void RecordFailure_BelowThreshold_StaysClosed()
    {
        var cb = CreateState();
        cb.RecordFailure();
        cb.RecordFailure();
        cb.GetState().Should().Be(CircuitState.Closed);
    }

    [Fact]
    public void RecordFailure_AtThreshold_TransitionsToOpen()
    {
        var cb = CreateState();
        cb.RecordFailure();
        cb.RecordFailure();
        cb.RecordFailure();
        cb.GetState().Should().Be(CircuitState.Open);
    }

    [Fact]
    public void Open_AfterDuration_TransitionsToHalfOpen()
    {
        var cb = CreateState(openSeconds: 1);
        cb.RecordFailure();
        cb.RecordFailure();
        cb.RecordFailure();
        cb.GetState().Should().Be(CircuitState.Open);

        Thread.Sleep(1100);  // 等待 Open 持续时间过期
        cb.GetState().Should().Be(CircuitState.HalfOpen);
    }

    [Fact]
    public void HalfOpen_SuccessBelowThreshold_StaysHalfOpen()
    {
        var cb = CreateState(openSeconds: 1);
        cb.RecordFailure();
        cb.RecordFailure();
        cb.RecordFailure();
        Thread.Sleep(1100);

        cb.RecordSuccess();  // 1 次成功（阈值 2）
        cb.GetState().Should().Be(CircuitState.HalfOpen);
    }

    [Fact]
    public void HalfOpen_SuccessAtThreshold_TransitionsToClosed()
    {
        var cb = CreateState(openSeconds: 1);
        cb.RecordFailure();
        cb.RecordFailure();
        cb.RecordFailure();
        Thread.Sleep(1100);

        cb.RecordSuccess();
        cb.RecordSuccess();  // 2 次成功
        cb.GetState().Should().Be(CircuitState.Closed);
    }

    [Fact]
    public void HalfOpen_Failure_TransitionsToOpen()
    {
        var cb = CreateState(openSeconds: 1);
        cb.RecordFailure();
        cb.RecordFailure();
        cb.RecordFailure();
        Thread.Sleep(1100);

        cb.RecordFailure();  // HalfOpen 探测失败
        cb.GetState().Should().Be(CircuitState.Open);
    }

    [Fact]
    public void RecordSuccess_InClosed_ResetsFailureCount()
    {
        var cb = CreateState();
        cb.RecordFailure();
        cb.RecordFailure();  // 2 次失败
        cb.RecordSuccess();  // 重置
        cb.RecordFailure();  // 重新累计 1 次
        cb.RecordFailure();  // 2 次
        cb.GetState().Should().Be(CircuitState.Closed);  // 未到 3 次
    }
}
