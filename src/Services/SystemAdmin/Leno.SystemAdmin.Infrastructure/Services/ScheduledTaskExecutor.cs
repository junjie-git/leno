using Leno.SystemAdmin.Application.Abstractions;
using Leno.SystemAdmin.Domain.Repositories;
using Leno.SystemAdmin.Infrastructure.Jobs;
using Microsoft.Extensions.Logging;

namespace Leno.SystemAdmin.Infrastructure.Services;

/// <summary>
/// 定时任务执行器实现，作为应用层 <see cref="IScheduledTaskExecutor"/> 的反腐败层，
/// 封装 <see cref="QuartzJobScheduler"/> 的注册、注销与立即触发能力。
/// </summary>
public sealed class ScheduledTaskExecutor : IScheduledTaskExecutor
{
    private readonly QuartzJobScheduler _scheduler;
    private readonly IScheduledTaskRepository _taskRepository;
    private readonly ILogger<ScheduledTaskExecutor> _logger;

    public ScheduledTaskExecutor(
        QuartzJobScheduler scheduler,
        IScheduledTaskRepository taskRepository,
        ILogger<ScheduledTaskExecutor> logger)
    {
        ArgumentNullException.ThrowIfNull(scheduler);
        ArgumentNullException.ThrowIfNull(taskRepository);
        ArgumentNullException.ThrowIfNull(logger);
        _scheduler = scheduler;
        _taskRepository = taskRepository;
        _logger = logger;
    }

    /// <summary>
    /// 立即触发指定任务执行。
    /// 当前为占位实现：仅校验任务存在并记录日志，Quartz 立即触发能力待后续接入。
    /// 聚合状态变更已由应用服务持久化，本方法不再重复变更。
    /// </summary>
    /// <param name="taskId">任务标识。</param>
    /// <param name="ct">取消令牌。</param>
    public async Task RunNowAsync(Guid taskId, CancellationToken ct = default)
    {
        var task = await _taskRepository.GetByIdAsync(taskId, ct);
        if (task is null)
        {
            _logger.LogWarning("手动触发定时任务失败：任务不存在 TaskId={TaskId}", taskId);
            return;
        }

        _logger.LogInformation("手动触发定时任务执行 TaskId={TaskId}", taskId);
    }

    /// <summary>
    /// 按 Cron 表达式注册任务调度，委托给 <see cref="QuartzJobScheduler"/>。
    /// </summary>
    /// <param name="taskId">任务标识。</param>
    /// <param name="jobType">作业类型（程序集限定名）。</param>
    /// <param name="cronExpression">Cron 表达式。</param>
    /// <param name="parameters">参数 JSON，可空。</param>
    /// <param name="ct">取消令牌。</param>
    public Task ScheduleAsync(Guid taskId, string jobType, string cronExpression, string? parameters, CancellationToken ct = default)
        => _scheduler.ScheduleTaskAsync(taskId, jobType, cronExpression, parameters, ct);

    /// <summary>
    /// 注销任务调度，委托给 <see cref="QuartzJobScheduler"/>。
    /// </summary>
    /// <param name="taskId">任务标识。</param>
    /// <param name="ct">取消令牌。</param>
    public Task UnscheduleAsync(Guid taskId, CancellationToken ct = default)
        => _scheduler.UnscheduleTaskAsync(taskId, ct);
}
