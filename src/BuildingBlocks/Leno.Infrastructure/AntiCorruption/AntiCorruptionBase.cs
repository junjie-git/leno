using System.Net.Http;
using Leno.SharedKernel.Exceptions;

namespace Leno.Infrastructure.AntiCorruption;

/// <summary>
/// 防腐层抽象基类（M4.1）。
/// 统一 <see cref="ExecuteAsync"/> 模板方法：异常捕获、指标埋点、HTTP 状态码映射。
/// 写操作与读操作均 <c>throwOnFailure=true</c>，不再返回 null（spec M4.1）。
/// 网络故障统一映射 HTTP 503 + ErrorCode <c>{SERVICE}_UNAVAILABLE</c>。
/// </summary>
public abstract class AntiCorruptionBase
{
    /// <summary>防腐层服务标识（如 <c>product</c>、<c>promotion</c>、<c>points</c>），用于指标埋点。</summary>
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
            // 用户主动取消，直接传播不埋点
            throw;
        }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
        {
            AntiCorruptionMetrics.RecordFailure(ServiceName, operation);
            throw new AntiCorruptionException(
                $"防腐层调用 {ServiceName}/{operation} 超时：{ex.Message}",
                $"{ServiceName.ToUpperInvariant()}_UNAVAILABLE");
        }
        catch (HttpRequestException ex)
        {
            AntiCorruptionMetrics.RecordFailure(ServiceName, operation);
            throw new AntiCorruptionException(
                $"防腐层调用 {ServiceName}/{operation} 网络故障：{ex.Message}",
                $"{ServiceName.ToUpperInvariant()}_UNAVAILABLE");
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
                $"防腐层调用 {ServiceName}/{operation} 失败：{ex.Message}",
                $"{ServiceName.ToUpperInvariant()}_REMOTE_FAILED");
        }
    }

    protected async Task ExecuteAsync(
        string operation,
        Func<CancellationToken, Task> execute,
        CancellationToken ct = default)
    {
        await ExecuteAsync<object?>(operation, async token =>
        {
            await execute(token).ConfigureAwait(false);
            return null;
        }, ct).ConfigureAwait(false);
    }

    protected void EnsureSuccessStatusCode(HttpResponseMessage response, string operation)
    {
        if (!response.IsSuccessStatusCode)
        {
            AntiCorruptionMetrics.RecordFailure(ServiceName, operation);
            throw new AntiCorruptionException(
                $"防腐层调用 {ServiceName}/{operation} 返回非成功状态码 {(int)response.StatusCode} ({response.StatusCode})",
                $"{ServiceName.ToUpperInvariant()}_REMOTE_FAILED");
        }
    }
}
