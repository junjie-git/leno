namespace Leno.ApiGateway.Bff.Dag;

/// <summary>
/// DAG 编排引擎：对 <see cref="AggregateGraph"/> 执行拓扑分波并行调度，
/// 节点超时级联取消下游，整体超时兜底。
/// </summary>
public interface IDagOrchestrator
{
    /// <summary>
    /// 执行聚合图：按拓扑顺序分波并行调度就绪节点，最大化并行度。
    /// </summary>
    /// <param name="graph">已通过 <see cref="AggregateBuilder.Build"/> 校验的聚合图。</param>
    /// <param name="overallToken">调用方取消令牌（整体超时由引擎内部 CTS 控制）。</param>
    /// <returns>包含已完成节点结果与失败明细的 <see cref="AggregateResult"/>。</returns>
    Task<AggregateResult> ExecuteAsync(AggregateGraph graph, CancellationToken overallToken = default);
}
