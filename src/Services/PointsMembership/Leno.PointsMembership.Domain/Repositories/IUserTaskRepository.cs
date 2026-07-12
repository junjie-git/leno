using Leno.PointsMembership.Domain.Aggregates;
using Leno.SharedKernel.Abstractions;

namespace Leno.PointsMembership.Domain.Repositories;

/// <summary>
/// 用户任务仓储接口，管理 <see cref="UserTask"/> 实体。
/// </summary>
public interface IUserTaskRepository : IRepository<UserTask>
{
    /// <summary>
    /// 按用户标识与任务标识查询用户任务记录。
    /// </summary>
    /// <param name="userId">用户标识。</param>
    /// <param name="taskId">任务标识。</param>
    Task<UserTask?> GetByUserIdAndTaskIdAsync(Guid userId, Guid taskId, CancellationToken ct = default);

    /// <summary>
    /// 查询用户所有任务记录（含已完成与未完成）。
    /// </summary>
    /// <param name="userId">用户标识。</param>
    Task<List<UserTask>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// 查询用户在指定日期已完成的任务记录。
    /// </summary>
    /// <param name="userId">用户标识。</param>
    /// <param name="date">北京时间日期。</param>
    Task<List<UserTask>> GetCompletedByUserIdAndDateAsync(Guid userId, DateOnly targetDate, CancellationToken ct = default);
}