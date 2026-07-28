using Leno.SystemAdmin.Application.DTOs;
using Leno.SystemAdmin.Domain.ValueObjects;

namespace Leno.SystemAdmin.Application;

/// <summary>
/// 服务器监控应用服务接口。
/// 不依赖 Redis，永远可用；数据来自 .NET 进程内 API 与内存历史窗口。
/// </summary>
public interface IServerMonitorAppService
{
    /// <summary>获取服务器快照（6 卡片 + 系统信息）。</summary>
    Task<ServerSnapshotDto> GetSnapshotAsync(CancellationToken ct = default);

    /// <summary>获取历史指标折线数据。metric: cpu/memory/disk-io；rangeSeconds: 1-3600。</summary>
    Task<MetricHistoryDto> GetHistoryAsync(string metric, int rangeSeconds, CancellationToken ct = default);
}
