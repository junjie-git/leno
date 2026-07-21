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

    /// <summary>
    /// 熔断器最近一次进入 Open 状态的时刻；null 表示从未打开过（初始为 Closed）。
    /// <para>
    /// T13 修复：原实现使用 <c>DateTime.MinValue</c>，导致 <see cref="GetStateUnsafe"/> 中
    /// <c>DateTime.UtcNow - _openedAt</c> 永远大于 <see cref="_openDuration"/>，
    /// 熔断器初始即被误判为 HalfOpen。改用 <see cref="DateTime"/>? 后，
    /// null 表示从未打开，初始状态正确返回 Closed。
    /// </para>
    /// </summary>
    private DateTime? _openedAt;

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
            return GetStateUnsafe();
        }
    }

    /// <summary>
    /// 在已持有 <see cref="_lock"/> 的上下文中获取当前熔断状态（不加锁）。
    /// <para>
    /// T39 修复：<see cref="RecordSuccess"/> 与 <see cref="RecordFailure"/> 已在 lock 块内，
    /// 原实现重入调用 <see cref="GetState"/>（再次获取同一锁）。虽然 C# lock 是可重入的不会死锁，
    /// 但增加了锁持有时间。提取此方法供已持锁上下文直接调用，避免重入。
    /// 公开的 <see cref="GetState"/> 委托给此方法并加锁。
    /// </para>
    /// </summary>
    private CircuitState GetStateUnsafe()
    {
        if (_consecutiveFailures < _failureThreshold)
        {
            return CircuitState.Closed;
        }

        // T13: _openedAt 为 null 表示从未打开过，状态为 Closed
        if (!_openedAt.HasValue)
        {
            return CircuitState.Closed;
        }

        if (DateTime.UtcNow - _openedAt.Value < _openDuration)
        {
            return CircuitState.Open;
        }

        return CircuitState.HalfOpen;
    }

    /// <summary>记录一次 gRPC 调用成功。HalfOpen 状态下累计 SuccessThreshold 次切 Closed。</summary>
    public void RecordSuccess()
    {
        lock (_lock)
        {
            // T39: 直接调用 GetStateUnsafe 避免重入 GetState
            var state = GetStateUnsafe();
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
        // T13: 重置为 null 表示从未打开（与初始值一致）
        _openedAt = null;
    }

    /// <summary>
    /// 同步当前熔断状态到 AntiCorruptionMetrics。
    /// <para>
    /// T14 修复：原实现仅传 <c>isOpen</c> 布尔值，HalfOpen 状态下指标显示为 Closed(0)，
    /// 运维无法区分"正常关闭"和"半开探测中"。改用三态指标值：
    /// <list type="bullet">
    /// <item>0 = Closed（正常关闭）</item>
    /// <item>1 = Open（熔断打开，保持与既有指标断言兼容）</item>
    /// <item>2 = HalfOpen（半开放探测中）</item>
    /// </list>
    /// </para>
    /// </summary>
    private void UpdateMetrics()
    {
        var state = GetStateUnsafe();
        var stateValue = state switch
        {
            CircuitState.Closed => 0,
            CircuitState.Open => 1,
            CircuitState.HalfOpen => 2,
            _ => 0
        };
        AntiCorruptionMetrics.UpdateCircuitState(_serviceName, stateValue);
    }

    public void Dispose()
    {
        // 清理指标回调：重置为 Closed(0)
        AntiCorruptionMetrics.UpdateCircuitState(_serviceName, 0);
    }
}
