using Leno.PointsMembership.Domain.Aggregates;
using Leno.PointsMembership.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;

namespace Leno.PointsMembership.Domain.Repositories;

/// <summary>
/// 任务定义仓储接口，管理 <see cref="TaskDefinition"/> 聚合。
/// </summary>
public interface ITaskRepository : IRepository<TaskDefinition>
{
    /// <summary>
    /// 按任务类型查询任务定义。
    /// </summary>
    /// <param name="type">任务类型。</param>
    Task<TaskDefinition?> GetByTypeAsync(TaskType type, CancellationToken ct = default);

    /// <summary>
    /// 获取所有启用的任务定义列表。
    /// </summary>
    Task<List<TaskDefinition>> GetAllEnabledAsync(CancellationToken ct = default);
}