using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;

namespace Leno.SystemAdmin.Domain.Repositories;

/// <summary>
/// 索引重建任务仓储接口，定义在领域层，由基础设施层实现。
/// 支持按索引查询运行中任务与分页查询，写操作由工作单元统一提交。
/// </summary>
public interface IIndexRebuildTaskRepository : IRepository<IndexRebuildTask>
{
    /// <summary>
    /// 获取指定索引的运行中任务（用于冲突检测）。
    /// </summary>
    /// <param name="targetContext">目标上下文。</param>
    /// <param name="indexName">索引名称。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>运行中的任务，不存在则返回 null。</returns>
    Task<IndexRebuildTask?> GetRunningByIndexAsync(string targetContext, string indexName, CancellationToken ct);

    /// <summary>
    /// 分页查询索引重建任务，支持目标上下文与状态过滤。
    /// </summary>
    /// <param name="targetContext">目标上下文过滤，可空表示不限。</param>
    /// <param name="status">状态过滤，可空表示不限。</param>
    /// <param name="page">页码，从 1 起。</param>
    /// <param name="pageSize">每页条数。</param>
    /// <param name="ct">取消令牌。</param>
    Task<List<IndexRebuildTask>> QueryAsync(string? targetContext, RebuildTaskStatus? status, int page, int pageSize, CancellationToken ct);

    /// <summary>
    /// 统计索引重建任务数量，支持目标上下文与状态过滤。
    /// </summary>
    /// <param name="targetContext">目标上下文过滤，可空表示不限。</param>
    /// <param name="status">状态过滤，可空表示不限。</param>
    /// <param name="ct">取消令牌。</param>
    Task<int> CountAsync(string? targetContext, RebuildTaskStatus? status, CancellationToken ct);
}