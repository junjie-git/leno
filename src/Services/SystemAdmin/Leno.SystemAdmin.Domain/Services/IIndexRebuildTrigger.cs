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
    /// <param name="taskId">任务标识。</param>
    /// <param name="targetContext">目标上下文。</param>
    /// <param name="indexName">索引名称。</param>
    /// <param name="ct">取消令牌。</param>
    Task StartAsync(Guid taskId, string targetContext, string indexName, CancellationToken ct);

    /// <summary>
    /// 获取重建进度。
    /// </summary>
    /// <param name="taskId">任务标识。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>进度值，0-100。</returns>
    Task<int> GetProgressAsync(Guid taskId, CancellationToken ct);
}