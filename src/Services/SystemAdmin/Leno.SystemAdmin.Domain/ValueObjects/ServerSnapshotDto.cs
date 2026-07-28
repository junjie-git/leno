namespace Leno.SystemAdmin.Domain.ValueObjects;

/// <summary>服务器监控快照。</summary>
public sealed class ServerSnapshotDto
{
    public string Hostname { get; set; } = string.Empty;
    public string Os { get; set; } = string.Empty;
    public string KernelVersion { get; set; } = string.Empty;
    public string CpuModel { get; set; } = string.Empty;
    public int CpuCores { get; set; }
    public double CpuUsagePercent { get; set; }
    public long MemoryTotalBytes { get; set; }
    public long MemoryUsedBytes { get; set; }
    public long MemoryCachedBytes { get; set; }
    public long DiskTotalBytes { get; set; }
    public long DiskUsedBytes { get; set; }
    public long DiskReadBytesPerSec { get; set; }
    public long DiskWriteBytesPerSec { get; set; }
    public double LoadAvg1 { get; set; }
    public double LoadAvg5 { get; set; }
    public double LoadAvg15 { get; set; }
    public int ProcessCount { get; set; }
    public int UptimeSeconds { get; set; }
    public string BootTime { get; set; } = string.Empty;
    public string DotnetRuntimeVersion { get; set; } = string.Empty;
    public int GcTotalCollections { get; set; }
    public string SampledAt { get; set; } = string.Empty;
}
