using Grpc.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leno.Infrastructure.AntiCorruption;

/// <summary>
/// 双轨调度器（M4 双轨方案）。
/// 接收同一接口 <typeparamref name="TService"/> 的 HttpClient 实现（必填）与 gRPC 实现（可选），
/// 每次 <see cref="ExecuteAsync{TResult}"/> 根据 <c>UseGrpc</c> 开关与熔断状态选择实现。
/// 设计要点：
/// 1. 通过 <see cref="IOptionsMonitor{AntiCorruptionOptions}"/> 每次请求读取最新配置，支持 ConsulConfigWatcher 热更新
/// 2. 熔断器为 Keyed Singleton（每个防腐层一个实例），跨请求累积失败计数
/// 3. 仅 gRPC 不可用异常（Unavailable/DeadlineExceeded/Internal/ResourceExhausted）触发降级，业务异常直接抛
/// 4. 熔断 Open 期间所有 gRPC 调用直接降级到 HttpClient，不调 gRPC
/// </summary>
public sealed class AntiCorruptionDispatcher<TService> : IDisposable
    where TService : class
{
    private readonly TService _httpImplementation;
    private readonly TService? _grpcImplementation;
    private readonly IOptionsMonitor<AntiCorruptionOptions> _optionsMonitor;
    private readonly ILogger<AntiCorruptionDispatcher<TService>> _logger;
    private readonly CircuitBreakerState _circuitBreaker;
    private readonly string _serviceName;

    public AntiCorruptionDispatcher(
        TService httpImplementation,
        TService? grpcImplementation,
        IOptionsMonitor<AntiCorruptionOptions> optionsMonitor,
        ILogger<AntiCorruptionDispatcher<TService>> logger,
        string serviceName,
        CircuitBreakerState circuitBreaker)
    {
        ArgumentNullException.ThrowIfNull(httpImplementation);
        ArgumentNullException.ThrowIfNull(optionsMonitor);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(circuitBreaker);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

        _httpImplementation = httpImplementation;
        _grpcImplementation = grpcImplementation;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
        _serviceName = serviceName;
        _circuitBreaker = circuitBreaker;
    }

    public async Task<TResult> ExecuteAsync<TResult>(
        Func<TService, Task<TResult>> operation,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        // 每次请求读取最新配置（支持 ConsulConfigWatcher 热更新）
        var currentOptions = _optionsMonitor.CurrentValue;

        if (!currentOptions.UseGrpc || _grpcImplementation is null)
        {
            return await operation(_httpImplementation).ConfigureAwait(false);
        }

        var state = _circuitBreaker.GetState();
        if (state == CircuitState.Open)
        {
            _logger.LogWarning("AntiCorruption {Service} gRPC circuit open, falling back to HTTP", _serviceName);
            AntiCorruptionMetrics.RecordFallback(_serviceName, "circuit_open");
            return await operation(_httpImplementation).ConfigureAwait(false);
        }

        try
        {
            var result = await operation(_grpcImplementation).ConfigureAwait(false);
            _circuitBreaker.RecordSuccess();
            return result;
        }
        catch (AntiCorruptionException ex) when (IsGrpcUnavailable(ex))
        {
            _circuitBreaker.RecordFailure();
            _logger.LogWarning(ex, "AntiCorruption {Service} gRPC unavailable, falling back to HTTP", _serviceName);
            AntiCorruptionMetrics.RecordFallback(_serviceName, ExtractReason(ex));

            // 熔断因本次失败触发 → 本次直接抛（下次走 HTTP）
            if (_circuitBreaker.GetState() == CircuitState.Open)
            {
                throw;
            }

            // 熔断未触发 → 本次降级到 HttpClient
            return await operation(_httpImplementation).ConfigureAwait(false);
        }
    }

    /// <summary>判断 AntiCorruptionException 是否由 gRPC 不可用引起（用于决定是否降级）。</summary>
    private static bool IsGrpcUnavailable(AntiCorruptionException ex)
    {
        if (ex.InnerException is not RpcException rpc) return false;
        return rpc.StatusCode is StatusCode.Unavailable or StatusCode.DeadlineExceeded
            or StatusCode.Internal or StatusCode.ResourceExhausted;
    }

    private static string ExtractReason(AntiCorruptionException ex)
        => ex.InnerException is RpcException rpc ? $"grpc_{rpc.StatusCode}" : "grpc_unknown";

    /// <summary>
    /// 释放 Dispatcher 资源。
    /// <para>
    /// 注意：<see cref="_circuitBreaker"/> 是通过 DI 容器解析的 KeyedSingleton，
    /// 生命周期由 DI 容器统一管理。本 Dispatcher 注册为 Scoped，不应在 Dispose 时
    /// 销毁共享的 KeyedSingleton，否则同进程中其他 Scope 的 Dispatcher 熔断器指标状态丢失。
    /// </para>
    /// </summary>
    public void Dispose()
    {
        // 不 Dispose _circuitBreaker — 它是 KeyedSingleton，由 DI 容器管理生命周期。
        // 仅清理 Dispatcher 自身拥有的非共享资源（当前无）。
        GC.SuppressFinalize(this);
    }
}
