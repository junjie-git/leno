using Grpc.Core;
using Leno.SharedKernel.Exceptions;

namespace Leno.Infrastructure.AntiCorruption;

/// <summary>
/// gRPC 防腐层客户端基类（M4.3）。
/// 统一 gRPC 调用的异常处理与埋点。
/// 错误处理策略与 <see cref="AntiCorruptionBase"/> 一致：网络故障映射 503 + <c>{SERVICE}_UNAVAILABLE</c>。
/// </summary>
public abstract class GrpcAntiCorruptionClientBase
{
    protected abstract string ServiceName { get; }

    protected async Task<T> ExecuteAsync<T>(
        string operation,
        Func<CancellationToken, Task<T>> execute,
        CancellationToken ct = default)
    {
        try
        {
            return await execute(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // 用户取消透传，不埋点
            throw;
        }
        catch (OperationCanceledException ex)
        {
            AntiCorruptionMetrics.RecordFailure(ServiceName, operation);
            throw new AntiCorruptionException(
                $"gRPC 调用 {ServiceName}/{operation} 超时：{ex.Message}",
                $"{ServiceName.ToUpperInvariant()}_UNAVAILABLE");
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable ||
                                       ex.StatusCode == StatusCode.DeadlineExceeded)
        {
            AntiCorruptionMetrics.RecordFailure(ServiceName, operation);
            throw new AntiCorruptionException(
                $"gRPC 调用 {ServiceName}/{operation} 不可用：{ex.Status.Detail}",
                $"{ServiceName.ToUpperInvariant()}_UNAVAILABLE");
        }
        catch (RpcException ex)
        {
            AntiCorruptionMetrics.RecordFailure(ServiceName, operation);
            throw new AntiCorruptionException(
                $"gRPC 调用 {ServiceName}/{operation} 失败：StatusCode={ex.StatusCode} Detail={ex.Status.Detail}",
                $"{ServiceName.ToUpperInvariant()}_REMOTE_FAILED");
        }
        catch (DomainException)
        {
            // 业务异常透传，不重复埋点
            throw;
        }
        catch (Exception ex)
        {
            AntiCorruptionMetrics.RecordFailure(ServiceName, operation);
            throw new AntiCorruptionException(
                $"gRPC 调用 {ServiceName}/{operation} 失败：{ex.Message}",
                $"{ServiceName.ToUpperInvariant()}_REMOTE_FAILED");
        }
    }
}
