using Leno.SystemAdmin.Application.DTOs;
using Leno.SystemAdmin.Domain.Exceptions;
using Leno.SystemAdmin.Domain.Services;
using Leno.SystemAdmin.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Leno.SystemAdmin.Application.Services;

/// <summary>
/// 服务器监控应用服务实现。
/// 委托 IDotNetProcessMonitor 获取实时快照，IMetricHistoryStore 获取历史折线。
/// metric 参数校验与 rangeSeconds 边界在本层完成。
/// </summary>
public sealed class ServerMonitorAppService : IServerMonitorAppService
{
    private const int MinRangeSeconds = 1;
    private const int MaxRangeSeconds = 3600;

    private readonly IDotNetProcessMonitor _processMonitor;
    private readonly IMetricHistoryStore _metricHistoryStore;
    private readonly ILogger<ServerMonitorAppService> _logger;

    public ServerMonitorAppService(
        IDotNetProcessMonitor processMonitor,
        IMetricHistoryStore metricHistoryStore,
        ILogger<ServerMonitorAppService> logger)
    {
        ArgumentNullException.ThrowIfNull(processMonitor);
        ArgumentNullException.ThrowIfNull(metricHistoryStore);
        ArgumentNullException.ThrowIfNull(logger);
        _processMonitor = processMonitor;
        _metricHistoryStore = metricHistoryStore;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ServerSnapshotDto> GetSnapshotAsync(CancellationToken ct = default)
    {
        var snapshot = await _processMonitor.GetSnapshotAsync(ct);
        _logger.LogDebug("服务器快照已采集 Hostname={Hostname} CpuUsage={CpuUsage}%",
            snapshot.Hostname, snapshot.CpuUsagePercent);
        return snapshot;
    }

    /// <inheritdoc />
    public async Task<MetricHistoryDto> GetHistoryAsync(string metric, int rangeSeconds, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(metric))
        {
            throw new SystemAdminDomainException("metric 不可为空", "SERVER_MONITOR_METRIC_EMPTY");
        }

        var metricName = metric.ToLowerInvariant() switch
        {
            "cpu" => MetricName.Cpu,
            "memory" => MetricName.Memory,
            "disk-io" => MetricName.DiskIo,
            _ => throw new SystemAdminDomainException($"metric 参数非法：{metric}（仅支持 cpu/memory/disk-io）", "SERVER_MONITOR_METRIC_INVALID")
        };

        if (rangeSeconds < MinRangeSeconds || rangeSeconds > MaxRangeSeconds)
        {
            throw new SystemAdminDomainException(
                $"rangeSeconds 必须在 {MinRangeSeconds}-{MaxRangeSeconds} 范围", "SERVER_MONITOR_RANGE_INVALID");
        }

        var points = await _metricHistoryStore.GetHistoryAsync(metricName, TimeSpan.FromSeconds(rangeSeconds), ct);
        return new MetricHistoryDto
        {
            Metric = metric,
            RangeSeconds = rangeSeconds,
            Points = points
        };
    }
}
