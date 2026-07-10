using Leno.SystemAdmin.Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Leno.SystemAdmin.Infrastructure.Jobs;

/// <summary>
/// 定时任务调度分发器，宿主启动时加载全部启用任务并注册到 Quartz。
/// 启动失败仅记录日志不中断宿主，避免影响其他服务启动。
/// </summary>
public sealed class ScheduledTaskDispatcher : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ScheduledTaskDispatcher> _logger;

    public ScheduledTaskDispatcher(IServiceProvider serviceProvider, ILogger<ScheduledTaskDispatcher> logger)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(logger);
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _serviceProvider.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<IScheduledTaskRepository>();
            var scheduler = scope.ServiceProvider.GetRequiredService<QuartzJobScheduler>();

            await scheduler.StartAsync(cancellationToken);

            var tasks = await repository.GetEnabledAsync(cancellationToken);
            foreach (var task in tasks)
            {
                await scheduler.ScheduleTaskAsync(task.Id, task.JobType, task.CronExpression, task.Parameters, cancellationToken);
            }

            _logger.LogInformation("已注册 {Count} 个定时任务调度", tasks.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "启动定时任务调度分发器失败");
        }
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("定时任务调度分发器停止");
        return Task.CompletedTask;
    }
}
