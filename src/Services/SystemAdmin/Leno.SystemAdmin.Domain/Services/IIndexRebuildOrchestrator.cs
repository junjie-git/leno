using Leno.SystemAdmin.Domain.Aggregates;

namespace Leno.SystemAdmin.Domain.Services;

/// <summary>
/// 索引重建编排器接口，协调索引重建任务的生命周期管理。
/// 实现位于基础设施层，编排任务创建、执行、进度查询与重试。
/// </summary>
public interface IIndexRebuildOrchestrator
{
    /// <summary>
    /// 触发索引重建，创建任务并立即开始执行。
    /// </summary>
    /// <param name="targetContext">目标上下文。</param>
    /// <param name="indexName">索引名称。</param>
    /// <param name="triggeredBy">触发操作者标识。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>创建的任务聚合根。</returns>
    Task<IndexRebuildTask> TriggerAsync(string targetContext, string indexName, string triggeredBy, CancellationToken ct);

    /// <summary>
    /// 查询任务进度。
    /// </summary>
    /// <param name="taskId">任务标识。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>任务聚合根。</returns>
    Task<IndexRebuildTask> GetProgressAsync(Guid taskId, CancellationToken ct);

    /// <summary>
    /// 重试失败的任务。
    /// </summary>
    /// <param name="taskId">任务标识。</param>
    /// <param name="triggeredBy">触发操作者标识。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>重试后的任务聚合根。</returns>
    Task<IndexRebuildTask> RetryAsync(Guid taskId, string triggeredBy, CancellationToken ct);
}