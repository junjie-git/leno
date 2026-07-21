namespace Leno.SystemAdmin.Domain.Services;

/// <summary>
/// 索引重建触发器接口，封装底层搜索引擎（如 Elasticsearch）的 reindex 操作。
/// 实现位于基础设施层。
/// </summary>
public interface IIndexRebuildTrigger
{
    /// <summary>
    /// 启动索引重建操作。
    /// </summary>
    /// <param name="taskId">任务标识，用于关联 dest 索引名 <c>{sourceIndex}_reindex_{taskId:N}</c>。</param>
    /// <param name="targetContext">目标上下文。</param>
    /// <param name="indexName">索引名称。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>底层搜索引擎返回的任务标识（如 ES task 节点），可空（引擎未返回时为 null）。</returns>
    Task<string?> StartAsync(Guid taskId, string targetContext, string indexName, CancellationToken ct);

    /// <summary>
    /// 获取重建进度。实现方须通过 <paramref name="taskId"/> 关联匹配对应的底层重建任务，
    /// 避免返回其他不相关任务的进度。
    /// </summary>
    /// <param name="taskId">任务标识，用于匹配 dest 索引名 <c>{sourceIndex}_reindex_{taskId:N}</c>。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>进度值，0-100。当底层任务已完成（不再存在于任务列表）时返回 100。</returns>
    Task<int> GetProgressAsync(Guid taskId, CancellationToken ct);
}