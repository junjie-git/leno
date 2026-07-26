using Leno.Points.Domain.Aggregates.TaskDefinition;
using Leno.Points.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using TaskDefinitionAggregate = Leno.Points.Domain.Aggregates.TaskDefinition.TaskDefinition;

namespace Leno.Points.Domain.Repositories;

/// <summary>
/// 任务定义仓储接口，管理 <see cref="TaskDefinition"/> 聚合。
/// </summary>
public interface ITaskRepository : IRepository<TaskDefinitionAggregate>
{
    /// <summary>
    /// 按任务类型查询任务定义。
    /// </summary>
    /// <param name="type">任务类型。</param>
    Task<TaskDefinitionAggregate?> GetByTypeAsync(TaskType type, CancellationToken ct = default);

    /// <summary>
    /// 获取所有启用的任务定义列表。
    /// </summary>
    Task<List<TaskDefinitionAggregate>> GetAllEnabledAsync(CancellationToken ct = default);
}
