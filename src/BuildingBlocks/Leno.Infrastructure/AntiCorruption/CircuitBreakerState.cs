namespace Leno.Infrastructure.AntiCorruption;

/// <summary>
/// 熔断器状态机（M4 双轨方案）。
/// 三状态：Closed（正常）→ Open（熔断）→ HalfOpen（半开放探测）→ Closed 或 Open。
/// 每个 AntiCorruptionDispatcher 持有一个独立实例（Keyed Singleton），跨请求累积失败计数。
/// </summary>
public sealed class CircuitBreakerState : IDisposable
{
    private readonly int _failureThreshold;
    private readonly int _successThreshold;
    private readonly TimeSpan _openDuration;
    private readonly string _serviceName;
    private int _consecutiveFailures;
    private int _halfOpenSuccesses;
    private DateTime _openedAt = DateTime.MinValue;
    private readonly object _lock = new();

    public CircuitBreakerState(string serviceName, int failureThreshold, int successThreshold, TimeSpan openDuration)
    {
        if (string.IsNullOrWhiteSpace(serviceName))
            throw new ArgumentException("serviceName 不能为空", nameof(serviceName));
        if (failureThreshold <= 0)
            throw new ArgumentOutOfRangeException(nameof(failureThreshold), "必须 > 0");
        if (successThreshold <= 0)
            throw new ArgumentOutOfRangeException(nameof(successThreshold), "必须 > 0");

        _serviceName = serviceName;
        _failureThreshold = failureThreshold;
        _successThreshold = successThreshold;
        _openDuration = openDuration;
    }

    /// <summary>获取当前熔断状态（线程安全）。</summary>
    public CircuitState GetState()
    {
        lock (_lock)
        {
            if (_consecutiveFailures < _failureThreshold)
                return CircuitState.Closed;

            if (DateTime.UtcNow - _openedAt < _openDuration)
                return CircuitState.Open;

            return CircuitState.HalfOpen;
        }
    }

    /// <summary>记录一次 gRPC 调用成功。HalfOpen 状态下累计 SuccessThreshold 次切 Closed。</summary>
    public void RecordSuccess()
    {
        lock (_lock)
        {
            var state = GetState();
            if (state == CircuitState.HalfOpen)
            {
                _halfOpenSuccesses++;
                if (_halfOpenSuccesses >= _successThreshold)
                {
                    ResetToClosed();
                }
            }
            else
            {
                // Closed 状态：重置失败计数
                _consecutiveFailures = 0;
            }

            UpdateMetrics();
        }
    }

    /// <summary>记录一次 gRPC 调用失败。Closed 状态累计 FailureThreshold 次切 Open；HalfOpen 任一失败切 Open。</summary>
    public void RecordFailure()
    {
        lock (_lock)
        {
            _consecutiveFailures++;
            _halfOpenSuccesses = 0;
            if (_consecutiveFailures >= _failureThreshold)
            {
                _openedAt = DateTime.UtcNow;
            }

            UpdateMetrics();
        }
    }

    private void ResetToClosed()
    {
        _consecutiveFailures = 0;
        _halfOpenSuccesses = 0;
        _openedAt = DateTime.MinValue;
    }

    private void UpdateMetrics()
    {
        var state = GetState();
        AntiCorruptionMetrics.UpdateCircuitOpenState(_serviceName, state == CircuitState.Open);
    }

    public void Dispose()
    {
        // 清理指标回调
        AntiCorruptionMetrics.UpdateCircuitOpenState(_serviceName, false);
    }
}
