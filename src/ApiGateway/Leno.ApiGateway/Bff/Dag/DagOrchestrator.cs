using System.Collections.Concurrent;
using Leno.ApiGateway.Bff.Models;
using Microsoft.Extensions.Logging;

namespace Leno.ApiGateway.Bff.Dag;

/// <summary>
/// DAG 编排引擎：对 <see cref="AggregateGraph"/> 执行拓扑分波并行调度。
/// <para>
/// 执行模型：
/// <list type="bullet">
///   <item>按拓扑顺序分波：每波找出依赖全部完成的"就绪"节点</item>
///   <item>波内并行：<see cref="Parallel.ForEachAsync"/> 并行执行就绪节点，MaxDegreeOfParallelism=就绪数</item>
///   <item>节点超时：每个节点独立 <see cref="CancellationTokenSource"/> + CancelAfter，超时后级联取消下游</item>
///   <item>整体超时：linked CTS + CancelAfter 兜底，超时后未完成节点标记 504</item>
///   <item>级联取消：依赖已超时/失败节点的下游节点跳过执行并标记 503</item>
/// </list>
/// </para>
/// </summary>
public sealed class DagOrchestrator : IDagOrchestrator
{
    /// <summary>默认整体超时（10 秒），与 <see cref="BffForwarderService"/> 一致。</summary>
    public static readonly TimeSpan DefaultOverallTimeout = TimeSpan.FromSeconds(10);

    private readonly CascadeTimeoutPolicy _cascadePolicy;
    private readonly ILogger<DagOrchestrator> _logger;
    private readonly TimeSpan _overallTimeout;

    /// <summary>构造 DAG 编排引擎。</summary>
    /// <param name="cascadePolicy">级联超时策略（记录超时节点供下游判断）。</param>
    /// <param name="logger">日志器。</param>
    /// <param name="overallTimeout">整体超时阈值，默认 10 秒。</param>
    public DagOrchestrator(
        CascadeTimeoutPolicy cascadePolicy,
        ILogger<DagOrchestrator> logger,
        TimeSpan? overallTimeout = null)
    {
        _cascadePolicy = cascadePolicy ?? throw new ArgumentNullException(nameof(cascadePolicy));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        var timeout = overallTimeout ?? DefaultOverallTimeout;
        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(overallTimeout), "整体超时必须为正值");
        }
        _overallTimeout = timeout;
    }

    /// <inheritdoc />
    public async Task<AggregateResult> ExecuteAsync(AggregateGraph graph, CancellationToken overallToken = default)
    {
        ArgumentNullException.ThrowIfNull(graph);

        // 每次执行前重置级联策略状态（CascadeTimeoutPolicy 若为 Scoped 则天然隔离，
        // 若为 Singleton 复用则需 Reset；统一调用确保两种生命周期均正确）
        _cascadePolicy.Reset();

        var results = new ConcurrentDictionary<string, object?>(StringComparer.Ordinal);
        var completed = new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);
        var errors = new ConcurrentDictionary<string, BffError>(StringComparer.Ordinal);
        var pending = new List<AggregateNode>(graph.SortedNodes);

        using var overallCts = CancellationTokenSource.CreateLinkedTokenSource(overallToken);
        overallCts.CancelAfter(_overallTimeout);

        while (pending.Count > 0)
        {
            // 1. 级联取消：将依赖已超时/失败节点的 pending 节点标记为失败
            CascadeFailedDependencies(pending, completed, errors);

            // 2. 找出就绪节点：依赖全部完成且未失败
            var ready = pending
                .Where(n => !completed.ContainsKey(n.Name) && !errors.ContainsKey(n.Name))
                .Where(n => n.Dependencies.All(d => completed.ContainsKey(d)))
                .ToList();

            // 3. 没有就绪节点：剩余 pending 全部因级联取消已标记，退出循环
            if (ready.Count == 0)
            {
                break;
            }

            // 4. 并行执行就绪节点（波内并行，最大化并行度）
            var batchToken = overallCts.Token;
            try
            {
                await Parallel.ForEachAsync(
                    ready,
                    new ParallelOptions
                    {
                        CancellationToken = batchToken,
                        MaxDegreeOfParallelism = ready.Count
                    },
                    async (node, token) =>
                    {
                        await ExecuteNodeAsync(node, overallCts, results, completed, errors, token);
                    }).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (overallCts.IsCancellationRequested && !overallToken.IsCancellationRequested)
            {
                // 整体超时：将尚未完成/未失败的 pending 节点标记为 504
                foreach (var node in pending)
                {
                    if (completed.ContainsKey(node.Name) || errors.ContainsKey(node.Name))
                    {
                        continue;
                    }
                    errors.TryAdd(node.Name, new BffError
                    {
                        Source = node.Name,
                        StatusCode = 504,
                        Message = $"整体超时（{_overallTimeout.TotalSeconds:F0}s）"
                    });
                }
                break;
            }
            catch (OperationCanceledException) when (overallToken.IsCancellationRequested)
            {
                // 调用方主动取消：将尚未完成/未失败的 pending 节点标记为 499
                foreach (var node in pending)
                {
                    if (completed.ContainsKey(node.Name) || errors.ContainsKey(node.Name))
                    {
                        continue;
                    }
                    errors.TryAdd(node.Name, new BffError
                    {
                        Source = node.Name,
                        StatusCode = 499,
                        Message = "调用方取消"
                    });
                }
                break;
            }

            // 5. 移除已完成或失败的节点
            pending.RemoveAll(n => completed.ContainsKey(n.Name) || errors.ContainsKey(n.Name));
        }

        return new AggregateResult
        {
            Success = errors.IsEmpty,
            Partial = !errors.IsEmpty && !results.IsEmpty,
            Results = results,
            Errors = errors.Values.ToArray()
        };
    }

    private void CascadeFailedDependencies(
        List<AggregateNode> pending,
        ConcurrentDictionary<string, byte> completed,
        ConcurrentDictionary<string, BffError> errors)
    {
        foreach (var node in pending)
        {
            if (completed.ContainsKey(node.Name) || errors.ContainsKey(node.Name))
            {
                continue;
            }
            // 检查是否有依赖已失败或超时
            var failedDep = node.Dependencies.FirstOrDefault(d =>
                errors.ContainsKey(d) || _cascadePolicy.IsCancelled(d));
            if (failedDep is not null)
            {
                errors.TryAdd(node.Name, new BffError
                {
                    Source = node.Name,
                    StatusCode = 503,
                    Message = $"级联取消：上游节点 '{failedDep}' 失败或超时"
                });
            }
        }
    }

    private async Task ExecuteNodeAsync(
        AggregateNode node,
        CancellationTokenSource overallCts,
        ConcurrentDictionary<string, object?> results,
        ConcurrentDictionary<string, byte> completed,
        ConcurrentDictionary<string, BffError> errors,
        CancellationToken batchToken)
    {
        using var nodeCts = CancellationTokenSource.CreateLinkedTokenSource(batchToken);
        nodeCts.CancelAfter(node.Timeout);

        try
        {
            // 快照已完成节点结果供节点读取上游数据
            var input = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var kv in results)
            {
                input[kv.Key] = kv.Value;
            }

            var result = await node.Executor(input, nodeCts.Token).ConfigureAwait(false);
            results[node.Name] = result;
            completed.TryAdd(node.Name, 0);
        }
        catch (OperationCanceledException) when (nodeCts.IsCancellationRequested && !batchToken.IsCancellationRequested)
        {
            // 节点超时（nodeCts 触发，但整体 token 仍有效）
            _logger.LogWarning(
                "DAG 节点 {Node} 超时（{Timeout}s），级联取消下游",
                node.Name, node.Timeout.TotalSeconds);
            _cascadePolicy.OnNodeTimeout(node.Name);
            errors.TryAdd(node.Name, new BffError
            {
                Source = node.Name,
                StatusCode = 504,
                Message = $"节点超时（{node.Timeout.TotalSeconds:F0}s）"
            });
        }
        catch (OperationCanceledException) when (overallCts.IsCancellationRequested)
        {
            // 整体超时：不在此处记录（由外层统一处理），避免重复
        }
        catch (OperationCanceledException) when (batchToken.IsCancellationRequested)
        {
            // 调用方取消：不在此处记录（由外层统一处理）
        }
        catch (DagNodeException ex)
        {
            // 节点抛出携带状态码的异常（如下游非 2xx）
            _logger.LogWarning(
                "DAG 节点 {Node} 执行失败：{StatusCode} {Message}",
                node.Name, ex.StatusCode, ex.Message);
            errors.TryAdd(node.Name, new BffError
            {
                Source = node.Name,
                StatusCode = ex.StatusCode,
                Message = ex.Message
            });
        }
        catch (Exception ex)
        {
            // 其他异常统一标记 500
            _logger.LogWarning(
                ex, "DAG 节点 {Node} 执行异常：{Message}", node.Name, ex.Message);
            errors.TryAdd(node.Name, new BffError
            {
                Source = node.Name,
                StatusCode = 500,
                Message = ex.Message
            });
        }
    }
}
