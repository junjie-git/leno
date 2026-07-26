using Leno.Points.Application.DTOs;

namespace Leno.Points.Application;

/// <summary>
/// 任务中心应用服务接口，编排任务列表查询与任务完成用例。
/// </summary>
public interface ITaskAppService
{
    /// <summary>
    /// 获取用户任务列表（含完成状态），每日任务自动重置。
    /// </summary>
    /// <param name="userId">用户标识。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>任务列表，含任务定义与当前用户完成状态。</returns>
    Task<List<TaskDto>> GetTasksAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// 完成任务、发放积分。
    /// 一次性任务重复完成抛出 <c>PointsDomainException</c>（错误码 TASK_ONETIME_ALREADY_DONE）。
    /// 每日任务当日重复完成抛出 <c>PointsDomainException</c>（错误码 TASK_DAILY_ALREADY_DONE）。
    /// </summary>
    /// <param name="userId">用户标识。</param>
    /// <param name="taskId">任务标识。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>任务完成结果，包含奖励积分与完成时间。</returns>
    Task<TaskCompleteResultDto> CompleteTaskAsync(Guid userId, Guid taskId, CancellationToken ct = default);
}
