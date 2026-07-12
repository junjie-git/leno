using Leno.SystemAdmin.Domain.Services;
using Leno.SystemAdmin.Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Leno.SystemAdmin.Infrastructure.Jobs;

/// <summary>
/// 统计数据对账后台作业，每日零点执行全量对账。
/// 比对 SystemAdmin 聚合统计与各域事件溯源统计，发现差异时记录日志并触发告警与修正。
/// 作为 BackgroundService 注册，在应用启动时开始运行。
/// </summary>
public sealed class StatisticsReconciliationJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<StatisticsReconciliationJob> _logger;
    private readonly TimeSpan _scheduleTime = new(0, 0, 0); // 每日零点

    public StatisticsReconciliationJob(
        IServiceScopeFactory scopeFactory,
        ILogger<StatisticsReconciliationJob> logger)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(logger);
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("统计数据对账后台作业已启动，计划每日零点执行");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var delay = CalculateDelayUntilMidnight();
                _logger.LogInformation("对账作业下次执行时间: {NextRun}", DateTime.UtcNow.Add(delay));

                await Task.Delay(delay, stoppingToken);

                _logger.LogInformation("开始执行每日对账作业");
                await ExecuteReconciliationAsync(stoppingToken);
                _logger.LogInformation("每日对账作业执行完成");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("对账作业被取消");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "对账作业执行异常");
                // 异常后等待 5 分钟再重试
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
    }

    private TimeSpan CalculateDelayUntilMidnight()
    {
        var now = DateTime.UtcNow;
        var midnight = now.Date.AddDays(1).Add(_scheduleTime);
        return midnight - now;
    }

    private async Task ExecuteReconciliationAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var reconciliationService = scope.ServiceProvider.GetRequiredService<IStatisticsReconciliationService>();

        // 对账昨日数据
        var yesterday = DateTime.UtcNow.Date.AddDays(-1);
        var period = new ReportPeriod(yesterday, yesterday.AddDays(1));

        try
        {
            var records = await reconciliationService.ReconcileAllAsync(period, ct);

            var consistentCount = records.Count(r => r.Status == ReconciliationStatus.Consistent);
            var discrepantCount = records.Count(r => r.Status == ReconciliationStatus.DiscrepancyFound);
            var errorCount = records.Count(r => r.Status == ReconciliationStatus.Error);

            _logger.LogInformation(
                "对账完成: 一致={Consistent} 差异={Discrepant} 错误={Error}",
                consistentCount, discrepantCount, errorCount);

            if (discrepantCount > 0)
            {
                _logger.LogWarning("发现 {Count} 个报表类型存在数据差异，请检查", discrepantCount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "对账执行失败");
        }
    }
}