using Leno.SharedKernel.Abstractions;
using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Repositories;
using Leno.SystemAdmin.Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Leno.SystemAdmin.Infrastructure.Jobs;

/// <summary>
/// 定时任务执行外壳，由 Quartz 触发。
/// 通过 <see cref="IServiceProvider"/> 创建作用域解析仓储与工作单元，
/// 读取任务后调用 <see cref="ScheduledTask.RunNow"/> 并记录执行结果。
/// </summary>
public sealed class ScheduledTaskJob : IJob
{
    private readonly IServiceProvider _serviceProvider;

    public ScheduledTaskJob(IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc />
    public async Task Execute(IJobExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var ct = context.CancellationToken;
        var taskIdValue = context.MergedJobDataMap.GetString("taskId");
        if (!Guid.TryParse(taskIdValue, out var taskId) || taskId == Guid.Empty)
        {
            return;
        }

        await using var scope = _serviceProvider.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IScheduledTaskRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<ScheduledTaskJob>>();

        var task = await repository.GetByIdAsync(taskId, ct);
        if (task is null)
        {
            logger.LogWarning("定时任务不存在 TaskId={TaskId}", taskId);
            return;
        }

        try
        {
            // 原子地完成"开始运行 + 记录成功"两步状态变更，单次 SaveEntitiesAsync 提交事务
            // 避免原先 RunNow + SaveEntitiesAsync + RecordExecution + SaveEntitiesAsync 中途失败导致任务卡在 Running 状态
            task.RunAndRecord(TaskRunStatus.Success, DateTime.UtcNow, null);
            await repository.UpdateAsync(task, ct);
            await unitOfWork.SaveEntitiesAsync(ct);

            logger.LogInformation("定时任务执行成功 TaskId={TaskId}", taskId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "定时任务执行失败 TaskId={TaskId}", taskId);
            try
            {
                // 失败分支同样使用 RunAndRecord 单次事务，记录 Failed 状态
                task.RunAndRecord(TaskRunStatus.Failed, DateTime.UtcNow, ex.Message);
                await repository.UpdateAsync(task, ct);
                await unitOfWork.SaveEntitiesAsync(ct);
            }
            catch (Exception inner)
            {
                logger.LogError(inner, "记录定时任务失败状态异常 TaskId={TaskId}", taskId);
            }
        }
    }
}
