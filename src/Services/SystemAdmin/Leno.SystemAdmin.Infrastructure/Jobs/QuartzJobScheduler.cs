using Microsoft.Extensions.Logging;
using Quartz;

namespace Leno.SystemAdmin.Infrastructure.Jobs;

/// <summary>
/// Quartz 调度器封装，负责任务的注册、取消与调度器启停。
/// 作为单例注册，由分发器与应用层共享同一调度器实例。
/// </summary>
public sealed class QuartzJobScheduler
{
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly ILogger<QuartzJobScheduler> _logger;
    private IScheduler? _scheduler;

    public QuartzJobScheduler(ISchedulerFactory schedulerFactory, ILogger<QuartzJobScheduler> logger)
    {
        ArgumentNullException.ThrowIfNull(schedulerFactory);
        ArgumentNullException.ThrowIfNull(logger);
        _schedulerFactory = schedulerFactory;
        _logger = logger;
    }

    /// <summary>启动调度器，若已启动则跳过。</summary>
    public async Task StartAsync(CancellationToken ct)
    {
        var scheduler = await GetSchedulerAsync(ct);
        if (!scheduler.IsStarted)
        {
            await scheduler.Start(ct);
            _logger.LogInformation("Quartz 调度器已启动");
        }
    }

    /// <summary>
    /// 按 Cron 表达式调度任务，作业数据携带任务标识与参数。
    /// 调度失败记录错误日志并重新抛出，由调用方处理。
    /// </summary>
    /// <param name="taskId">任务标识。</param>
    /// <param name="jobType">作业类型（程序集限定名，用于日志）。</param>
    /// <param name="cronExpression">Cron 表达式。</param>
    /// <param name="parameters">参数 JSON，可空。</param>
    /// <param name="ct">取消令牌。</param>
    public async Task ScheduleTaskAsync(Guid taskId, string jobType, string cronExpression, string? parameters, CancellationToken ct)
    {
        try
        {
            var scheduler = await GetSchedulerAsync(ct);
            var jobKey = JobKey.Create(taskId.ToString());
            var jobDetail = JobBuilder.Create<ScheduledTaskJob>()
                .WithIdentity(jobKey)
                .UsingJobData("taskId", taskId.ToString())
                .UsingJobData("parameters", parameters ?? string.Empty)
                .Build();

            var trigger = TriggerBuilder.Create()
                .WithIdentity($"trigger-{taskId}", "systemadmin")
                .StartNow()
                .WithCronSchedule(cronExpression)
                .Build();

            await scheduler.ScheduleJob(jobDetail, trigger, ct);
            _logger.LogInformation("已调度定时任务 TaskId={TaskId} JobType={JobType} Cron={CronExpression}", taskId, jobType, cronExpression);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "调度定时任务失败 TaskId={TaskId} JobType={JobType}", taskId, jobType);
            throw;
        }
    }

    /// <summary>按任务标识取消调度，任务不存在时忽略。</summary>
    /// <param name="taskId">任务标识。</param>
    /// <param name="ct">取消令牌。</param>
    public async Task UnscheduleTaskAsync(Guid taskId, CancellationToken ct)
    {
        try
        {
            var scheduler = await GetSchedulerAsync(ct);
            var jobKey = JobKey.Create(taskId.ToString());
            await scheduler.DeleteJob(jobKey, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取消调度定时任务失败 TaskId={TaskId}", taskId);
        }
    }

    private async Task<IScheduler> GetSchedulerAsync(CancellationToken ct)
        => _scheduler ??= await _schedulerFactory.GetScheduler(ct);
}
