using Leno.SystemAdmin.Domain.Services;
using Leno.SystemAdmin.Domain.ValueObjects;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Leno.SystemAdmin.Infrastructure.Jobs;

/// <summary>
/// 统计数据对账后台作业，每日零点执行全量对账。
/// 比对 SystemAdmin 聚合统计与各域事件溯源统计，发现差异时记录日志并触发告警与修正。
/// 作为 BackgroundService 注册，在应用启动时开始运行。
/// 时区通过配置 <c>Statistics:Reconciliation:TimeZone</c> 指定（默认 Asia/Shanghai），
/// 避免容器时区非 UTC 时午夜漂移。
/// </summary>
public sealed class StatisticsReconciliationJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<StatisticsReconciliationJob> _logger;
    private readonly TimeSpan _scheduleTime = new(0, 0, 0); // 每日零点
    private readonly TimeZoneInfo _timeZone;

    public StatisticsReconciliationJob(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<StatisticsReconciliationJob> logger)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(logger);
        _scopeFactory = scopeFactory;
        _logger = logger;

        // 读取配置化时区，默认 Asia/Shanghai；配置无效时回退 UTC 并告警
        var timeZoneId = configuration["Statistics:Reconciliation:TimeZone"];
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            timeZoneId = "Asia/Shanghai";
        }

        try
        {
            _timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            _logger.LogWarning("配置的时区 {TimeZoneId} 不存在，回退到 UTC", timeZoneId);
            _timeZone = TimeZoneInfo.Utc;
        }
        catch (InvalidTimeZoneException)
        {
            _logger.LogWarning("配置的时区 {TimeZoneId} 无效，回退到 UTC", timeZoneId);
            _timeZone = TimeZoneInfo.Utc;
        }
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("统计数据对账后台作业已启动，计划每日零点执行 时区={TimeZone}", _timeZone.Id);

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

    /// <summary>
    /// 计算距离配置化时区下次午夜的延迟。
    /// 使用 <see cref="TimeZoneInfo.ConvertTimeFromUtc"/> 将 UTC 转换为配置时区的本地时间，
    /// 再计算该时区的次日零点，避免容器时区非 UTC 时漂移 8 小时。
    /// </summary>
    internal TimeSpan CalculateDelayUntilMidnight()
    {
        var utcNow = DateTime.UtcNow;
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(utcNow, _timeZone);
        var localMidnight = localNow.Date.AddDays(1).Add(_scheduleTime);
        var delay = localMidnight - localNow;
        // 转换为 UTC 时间差，避免本地时间到 UTC 的偏移导致延迟计算错误
        return delay > TimeSpan.Zero ? delay : TimeSpan.Zero;
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