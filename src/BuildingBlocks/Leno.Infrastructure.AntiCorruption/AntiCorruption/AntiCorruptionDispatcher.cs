using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace Leno.Infrastructure.AntiCorruption;

/// <summary>
/// 防腐层调度器（双轨下线单轨版，2026-10-30 B2）。
/// 持有唯一 gRPC 实现，调用经熔断器保护：
/// 1. 熔断 Open 时 fail-fast 抛 <see cref="AntiCorruptionException"/>（不静默降级 HTTP）
/// 2. 任何 <see cref="AntiCorruptionException"/> 计入熔断失败后原样上抛
/// 3. 熔断器为 Keyed Singleton（每个防腐层一个实例），跨请求累积失败计数
/// </summary>
public sealed class AntiCorruptionDispatcher<TService> : IDisposable
    where TService : class
{
    private readonly TService _implementation;
    private readonly ILogger<AntiCorruptionDispatcher<TService>> _logger;
    private readonly CircuitBreakerState _circuitBreaker;
    private readonly string _serviceName;

    public AntiCorruptionDispatcher(
        TService implementation,
        ILogger<AntiCorruptionDispatcher<TService>> logger,
        string serviceName,
        CircuitBreakerState circuitBreaker)
    {
        ArgumentNullException.ThrowIfNull(implementation);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(circuitBreaker);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

        _implementation = implementation;
        _logger = logger;
        _serviceName = serviceName;
        _circuitBreaker = circuitBreaker;
    }

    public async Task<TResult> ExecuteAsync<TResult>(
        Func<TService, Task<TResult>> operation,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        var state = _circuitBreaker.GetState();
        if (state == CircuitState.Open)
        {
            _logger.LogWarning("AntiCorruption {Service} gRPC circuit open, failing fast", _serviceName);
            AntiCorruptionMetrics.RecordFallback(_serviceName, "circuit_open");
            throw new AntiCorruptionException(
                $"AntiCorruption {_serviceName} gRPC 熔断 Open，调用被拒绝（fail-fast，无 HTTP 降级路径）");
        }

        try
        {
            var result = await operation(_implementation).ConfigureAwait(false);
            _circuitBreaker.RecordSuccess();
            return result;
        }
        catch (AntiCorruptionException ex) when (IsInfrastructureFailure(ex))
        {
            // 仅传输类故障（Unavailable/DeadlineExceeded/Internal/ResourceExhausted）计入熔断；
            // 业务类失败（如 not_found）不是传输故障，直接上抛、不计失败，避免误触发熔断。
            _circuitBreaker.RecordFailure();
            _logger.LogWarning(ex, "AntiCorruption {Service} gRPC infrastructure failure", _serviceName);
            AntiCorruptionMetrics.RecordFallback(_serviceName, ExtractReason(ex));
            throw;
        }
        catch (AntiCorruptionException)
        {
            // 业务类失败：不计熔断、不记 fallback 指标，原样上抛
            throw;
        }
    }

    /// <summary>判断 AntiCorruptionException 是否由 gRPC 传输层故障引起（用于决定是否计入熔断失败）。</summary>
    private static bool IsInfrastructureFailure(AntiCorruptionException ex)
    {
        if (ex.InnerException is not RpcException rpc) return false;
        return rpc.StatusCode is StatusCode.Unavailable or StatusCode.DeadlineExceeded
            or StatusCode.Internal or StatusCode.ResourceExhausted;
    }

    private static string ExtractReason(AntiCorruptionException ex)
        => ex.InnerException is RpcException rpc ? $"grpc_{rpc.StatusCode}" : ex.ErrorCode;

    public void Dispose()
    {
        // 不 Dispose _circuitBreaker —— KeyedSingleton，由 DI 容器管理生命周期。
        GC.SuppressFinalize(this);
    }
}
