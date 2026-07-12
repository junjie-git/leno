using Leno.PointsMembership.Application.DTOs;

namespace Leno.PointsMembership.Application;

/// <summary>
/// 任务中心应用服务，编排任务列表查询与任务完成用例。
/// </summary>
public interface ITaskAppService
{
    /// <summary>
    /// 获取用户任务列表（含完成状态），每日任务自动重置。
    /// </summary>
    /// <param name="userId">用户标识。</param>
    Task<List<TaskDto>> GetTasksAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// 完成任务、发放积分。
    /// </summary>
    /// <param name="userId">用户标识。</param>
    /// <param name="taskId">任务标识。</param>
    Task<TaskCompleteResultDto> CompleteTaskAsync(Guid userId, Guid taskId, CancellationToken ct = default);
}