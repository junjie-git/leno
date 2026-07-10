namespace Leno.SystemAdmin.Application.Abstractions;

/// <summary>
/// 定时任务执行器抽象，封装底层调度器（如 Quartz）的注册、注销与立即触发能力。
/// 定义于应用层，由基础设施层实现，避免应用层直接依赖调度器实现。
/// </summary>
public interface IScheduledTaskExecutor
{
    /// <summary>立即触发指定任务执行。</summary>
    /// <param name="taskId">任务标识。</param>
    /// <param name="ct">取消令牌。</param>
    Task RunNowAsync(Guid taskId, CancellationToken ct = default);

    /// <summary>按 Cron 表达式注册任务调度。</summary>
    /// <param name="taskId">任务标识。</param>
    /// <param name="jobType">作业类型（程序集限定名）。</param>
    /// <param name="cronExpression">Cron 表达式。</param>
    /// <param name="parameters">参数 JSON，可空。</param>
    /// <param name="ct">取消令牌。</param>
    Task ScheduleAsync(Guid taskId, string jobType, string cronExpression, string? parameters, CancellationToken ct = default);

    /// <summary>注销任务调度。</summary>
    /// <param name="taskId">任务标识。</param>
    /// <param name="ct">取消令牌。</param>
    Task UnscheduleAsync(Guid taskId, CancellationToken ct = default);
}
