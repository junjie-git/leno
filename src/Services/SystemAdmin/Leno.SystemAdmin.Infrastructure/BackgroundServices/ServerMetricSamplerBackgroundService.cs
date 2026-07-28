using Leno.SystemAdmin.Domain.Services;
using Leno.SystemAdmin.Domain.ValueObjects;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Leno.SystemAdmin.Infrastructure.BackgroundServices;

/// <summary>
/// 服务器指标采样后台服务：1s 间隔调 IDotNetProcessMonitor 采样并写入 IMetricHistoryStore。
/// 单次采样失败仅记日志不退出，下次循环继续；进程重启后历史清空符合预期。
/// </summary>
public sealed class ServerMetricSamplerBackgroundService : BackgroundService
{
    private readonly IDotNetProcessMonitor _monitor;
    private readonly IMetricHistoryStore _historyStore;
    private readonly ILogger<ServerMetricSamplerBackgroundService> _logger;
    private readonly TimeSpan _sampleInterval = TimeSpan.FromSeconds(1);

    public ServerMetricSamplerBackgroundService(
        IDotNetProcessMonitor monitor,
        IMetricHistoryStore historyStore,
        ILogger<ServerMetricSamplerBackgroundService> logger)
    {
        _monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
        _historyStore = historyStore ?? throw new ArgumentNullException(nameof(historyStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("服务器指标采样后台服务已启动，采样间隔 {Interval} 秒", _sampleInterval.TotalSeconds);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var snapshot = await _monitor.GetSnapshotAsync(stoppingToken);
                await _historyStore.RecordAsync(MetricName.Cpu, snapshot.CpuUsagePercent, stoppingToken);
                var memUsagePercent = snapshot.MemoryTotalBytes > 0
                    ? snapshot.MemoryUsedBytes / (double)snapshot.MemoryTotalBytes * 100
                    : 0;
                await _historyStore.RecordAsync(MetricName.Memory, memUsagePercent, stoppingToken);
                await _historyStore.RecordAsync(MetricName.DiskIo, snapshot.DiskReadBytesPerSec + snapshot.DiskWriteBytesPerSec, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "服务器指标采样失败，下次循环继续");
            }
            try
            {
                await Task.Delay(_sampleInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
        }
        _logger.LogInformation("服务器指标采样后台服务已停止");
    }
}
