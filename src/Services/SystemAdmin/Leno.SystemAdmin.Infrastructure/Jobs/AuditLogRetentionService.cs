using Leno.SystemAdmin.Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Leno.SystemAdmin.Infrastructure.Jobs;

/// <summary>
/// 审计日志保留策略后台服务，定期清理超过保留期的日志条目。
/// 默认保留 180 天，每小时执行一次。
/// </summary>
public sealed class AuditLogRetentionService : BackgroundService
{
    private const int RetentionDays = 180;
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AuditLogRetentionService> _logger;

    public AuditLogRetentionService(
        IServiceScopeFactory scopeFactory,
        ILogger<AuditLogRetentionService> logger)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(logger);
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("审计日志保留服务已启动，保留天数={RetentionDays}", RetentionDays);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(CleanupInterval, stoppingToken);
                await CleanupAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "审计日志清理异常");
            }
        }

        _logger.LogInformation("审计日志保留服务已停止");
    }

    private async Task CleanupAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAuditLogEntryRepository>();

        var cutoff = DateTime.UtcNow.AddDays(-RetentionDays);
        var deleted = await repository.DeleteOlderThanAsync(cutoff, ct);

        if (deleted > 0)
        {
            _logger.LogInformation("审计日志清理完成，删除 {Count} 条 {Cutoff} 之前的记录", deleted, cutoff.ToString("yyyy-MM-ddTHH:mm:ssZ"));
        }
    }
}