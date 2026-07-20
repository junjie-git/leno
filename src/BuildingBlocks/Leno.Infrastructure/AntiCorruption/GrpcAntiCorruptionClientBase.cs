using System.Diagnostics;
using Grpc.Core;
using Leno.SharedKernel.Exceptions;
using Microsoft.Extensions.DependencyInjection;
using Polly;

namespace Leno.Infrastructure.AntiCorruption;

/// <summary>
/// gRPC 防腐层客户端基类（M4.3 + M4 双轨方案 + P1-B.1 Polly 集成）。
/// 统一 gRPC 调用的异常处理与埋点。
/// 错误处理策略与 <see cref="AntiCorruptionBase"/> 一致：网络故障映射 503 + <c>{SERVICE}_UNAVAILABLE</c>。
/// M4 双轨方案：保留 <see cref="RpcException"/> 作为 <see cref="AntiCorruptionException.InnerException"/>，
/// 供 <c>AntiCorruptionDispatcher&lt;TService&gt;</c> 判断是否触发熔断降级。
/// P1-B.1：在 <see cref="ExecuteAsync{T}"/> 内嵌 Polly retry，仅对 gRPC 临时性故障
/// （Unavailable/DeadlineExceeded/Aborted/ResourceExhausted）重试 2 次，业务错误不重试。
/// </summary>
public abstract class GrpcAntiCorruptionClientBase
{
    private readonly IServiceProvider _serviceProvider;

    protected abstract string ServiceName { get; }

    protected GrpcAntiCorruptionClientBase(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    protected async Task<T> ExecuteAsync<T>(
        string operation,
        Func<CancellationToken, Task<T>> execute,
        CancellationToken ct = default)
    {
        // P1-B.1：从 DI 容器解析 gRPC retry 策略，仅对临时性故障重试 2 次
        var retryPolicy = _serviceProvider.GetRequiredKeyedService<IAsyncPolicy>(
            AntiCorruptionPollyExtensions.GrpcRetryPolicyKey);

        var sw = Stopwatch.StartNew();
        try
        {
            // Polly 仅处理 RpcException 中的临时性故障（Unavailable/DeadlineExceeded/Aborted/ResourceExhausted）
            // 业务错误（InvalidArgument/NotFound 等）、OperationCanceledException、DomainException 均不触发重试
            var result = await retryPolicy
                .ExecuteAsync(() => execute(ct))
                .ConfigureAwait(false);
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
        catch (RpcException ex) when (IsUnavailable(ex.StatusCode))
        {
            // 重试耗尽后包装为 AntiCorruptionException，保留 RpcException 作为 InnerException 供 Dispatcher 判断降级
            sw.Stop();
            AntiCorruptionMetrics.RecordGrpcRequest(ServiceName, ex.StatusCode.ToString(), sw.Elapsed.TotalSeconds);
            AntiCorruptionMetrics.RecordFailure(ServiceName, operation, "grpc");
            throw new AntiCorruptionException(
                $"gRPC 调用 {ServiceName}/{operation} 不可用：{ex.Status.Detail}",
                ex,
                $"{ServiceName.ToUpperInvariant()}_UNAVAILABLE");
        }
        catch (RpcException ex)
        {
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

    /// <summary>判断 gRPC StatusCode 是否属于"不可用"分类（触发熔断降级）。</summary>
    private static bool IsUnavailable(StatusCode code)
        => code is StatusCode.Unavailable or StatusCode.DeadlineExceeded
            or StatusCode.Internal or StatusCode.ResourceExhausted;
}
