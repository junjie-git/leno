using System.Diagnostics;
using Grpc.Core;
using Leno.SharedKernel.Exceptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Polly;

namespace Leno.Infrastructure.AntiCorruption;

/// <summary>
/// gRPC 防腐层客户端基类（M4.3 + M4 双轨方案 + P1-B.1 Polly 集成）。
/// 统一 gRPC 调用的异常处理与埋点。
/// 错误处理策略与 <see cref="AntiCorruptionBase"/> 一致：网络故障映射 503 + <c>{SERVICE}_UNAVAILABLE</c>。
/// M4 双轨方案：保留 <see cref="RpcException"/> 作为 <see cref="AntiCorruptionException.InnerException"/>，
/// 供 <c>AntiCorruptionDispatcher&lt;TService&gt;</c> 判断是否触发熔断降级。
/// <para>
/// P1-B.1 问题 9：在 <see cref="ExecuteAsync{T}"/> 内嵌 Polly retry，仅对临时性 gRPC 故障
/// （<see cref="StatusCode.Unavailable"/>/<see cref="StatusCode.DeadlineExceeded"/>/
/// <see cref="StatusCode.Aborted"/>/<see cref="StatusCode.ResourceExhausted"/>）重试 2 次，指数退避。
/// 业务错误（<see cref="StatusCode.InvalidArgument"/>/<see cref="StatusCode.NotFound"/> 等）与
/// <see cref="DomainException"/> 不重试。与既有 <see cref="CircuitBreakerState"/> 共存，互不干扰。
/// </para>
/// <para>
/// 派生类应通过构造函数注入 <see cref="IServiceProvider"/> 并传递给 base，
/// 以启用 Polly retry。未注入时（如单元测试）走原始无重试路径，保持兼容。
/// </para>
/// </summary>
public abstract class GrpcAntiCorruptionClientBase
{
    private readonly IServiceProvider? _serviceProvider;
    private readonly ILogger _logger;

    protected abstract string ServiceName { get; }

    /// <summary>
    /// 构造函数，注入 <see cref="IServiceProvider"/> 用于解析 Polly retry 策略。
    /// </summary>
    /// <param name="serviceProvider">
    /// 服务定位器，用于解析 keyed service <see cref="AntiCorruptionPollyExtensions.GrpcRetryPolicyKey"/>。
    /// 传 null（如单元测试）则跳过 Polly retry，保持向后兼容。
    /// </param>
    /// <param name="logger">日志记录器，用于记录重试事件。传 null 时使用 <see cref="NullLogger"/>。</param>
    protected GrpcAntiCorruptionClientBase(
        IServiceProvider? serviceProvider = null,
        ILogger? logger = null)
    {
        _serviceProvider = serviceProvider;
        _logger = logger ?? NullLogger.Instance;
    }

    protected async Task<T> ExecuteAsync<T>(
        string operation,
        Func<CancellationToken, Task<T>> execute,
        CancellationToken ct = default)
    {
        var retryPolicy = ResolveRetryPolicy();
        var sw = Stopwatch.StartNew();
        try
        {
            var result = retryPolicy is not null
                ? await retryPolicy.ExecuteAsync(ct => execute(ct), ct).ConfigureAwait(false)
                : await execute(ct).ConfigureAwait(false);
            sw.Stop();
            AntiCorruptionMetrics.RecordGrpcRequest(ServiceName, "OK", sw.Elapsed.TotalSeconds);
            return result;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // 用户取消透传，不埋点
            throw;
        }
        catch (OperationCanceledException ex)
        {
            sw.Stop();
            AntiCorruptionMetrics.RecordGrpcRequest(ServiceName, "DeadlineExceeded", sw.Elapsed.TotalSeconds);
            AntiCorruptionMetrics.RecordFailure(ServiceName, operation, "grpc");
            throw new AntiCorruptionException(
                $"gRPC 调用 {ServiceName}/{operation} 超时：{ex.Message}",
                ex,
                $"{ServiceName.ToUpperInvariant()}_UNAVAILABLE");
        }
        catch (RpcException ex) when (AntiCorruptionPollyExtensions.IsTransientGrpcStatus(ex.StatusCode))
        {
            // 临时性故障重试耗尽后包装为 AntiCorruptionException（保留 RpcException 供 Dispatcher 判断降级）
            sw.Stop();
            AntiCorruptionMetrics.RecordGrpcRequest(ServiceName, ex.StatusCode.ToString(), sw.Elapsed.TotalSeconds);
            AntiCorruptionMetrics.RecordFailure(ServiceName, operation, "grpc");
            _logger.LogWarning(ex,
                "gRPC 调用 {Service}/{Operation} 临时性故障重试耗尽 StatusCode={StatusCode} Detail={Detail}",
                ServiceName, operation, ex.StatusCode, ex.Status.Detail);
            throw new AntiCorruptionException(
                $"gRPC 调用 {ServiceName}/{operation} 不可用：{ex.Status.Detail}",
                ex,  // 保留 RpcException 作为 InnerException，供 Dispatcher 判断是否降级
                $"{ServiceName.ToUpperInvariant()}_UNAVAILABLE");
        }
        catch (RpcException ex)
        {
            // 非临时性 gRPC 故障（InvalidArgument/NotFound/PermissionDenied 等）：业务错误，不重试
            sw.Stop();
            AntiCorruptionMetrics.RecordGrpcRequest(ServiceName, ex.StatusCode.ToString(), sw.Elapsed.TotalSeconds);
            AntiCorruptionMetrics.RecordFailure(ServiceName, operation, "grpc");
            throw new AntiCorruptionException(
                $"gRPC 调用 {ServiceName}/{operation} 失败：StatusCode={ex.StatusCode} Detail={ex.Status.Detail}",
                ex,  // 业务异常也保留 RpcException，便于排查
                $"{ServiceName.ToUpperInvariant()}_REMOTE_FAILED");
        }
        catch (DomainException)
        {
            // 业务异常透传，不重复埋点
            throw;
        }
        catch (Exception ex)
        {
            sw.Stop();
            AntiCorruptionMetrics.RecordGrpcRequest(ServiceName, "Unknown", sw.Elapsed.TotalSeconds);
            AntiCorruptionMetrics.RecordFailure(ServiceName, operation, "grpc");
            throw new AntiCorruptionException(
                $"gRPC 调用 {ServiceName}/{operation} 失败：{ex.Message}",
                ex,
                $"{ServiceName.ToUpperInvariant()}_REMOTE_FAILED");
        }
    }

    /// <summary>
    /// 从 <see cref="_serviceProvider"/> 解析 gRPC retry 策略。
    /// 未注入 <see cref="IServiceProvider"/> 或策略未注册时返回 null，走原始无重试路径。
    /// </summary>
    /// <returns>Polly retry 策略，或 null。</returns>
    private IAsyncPolicy? ResolveRetryPolicy()
    {
        if (_serviceProvider is null)
        {
            return null;
        }

        // keyed service 可能未注册（如部分 BC 未调用 AddLenoGrpcAntiCorruptionPolly），
        // 此时返回 null 走原始路径，避免破坏既有功能。
        if (_serviceProvider.GetKeyedService<IAsyncPolicy>(AntiCorruptionPollyExtensions.GrpcRetryPolicyKey)
            is { } policy)
        {
            return policy;
        }

        return null;
    }
}
