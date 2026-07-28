using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using Leno.SystemAdmin.Domain.Services;
using Leno.SystemAdmin.Domain.ValueObjects;

namespace Leno.SystemAdmin.Infrastructure.Services;

/// <summary>
/// .NET 进程内服务器监控实现：CPU/内存/磁盘/负载平均/进程数。
/// CPU 使用率 = 进程 TotalProcessorTime 增量 / (经过时间 * 核心数) * 100；
/// Linux 下读取 /proc/meminfo / /proc/loadavg / /proc/cpuinfo；
/// 非 Linux 平台降级返回 0。
/// </summary>
public sealed class DotNetProcessMonitorService : IDotNetProcessMonitor
{
    private readonly Process _currentProcess = Process.GetCurrentProcess();
    private DateTime _lastCpuSample = DateTime.UtcNow;
    private TimeSpan _lastTotalProcessorTime = TimeSpan.Zero;
    private readonly object _cpuLock = new();

    /// <inheritdoc />
    public Task<ServerSnapshotDto> GetSnapshotAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(BuildSnapshot());
    }

    private ServerSnapshotDto BuildSnapshot()
    {
        lock (_cpuLock)
        {
            var now = DateTime.UtcNow;
            var totalProcessorTime = _currentProcess.TotalProcessorTime;
            var cpuUsagePercent = CalculateCpuUsage(now, totalProcessorTime);
            _lastCpuSample = now;
            _lastTotalProcessorTime = totalProcessorTime;

            var memUsedBytes = (long)_currentProcess.WorkingSet64;
            var memoryTotalBytes = GetTotalPhysicalMemory();
            var memoryCachedBytes = GC.GetGCMemoryInfo().HeapSizeBytes;

            var drives = DriveInfo.GetDrives().Where(d => d.IsReady && d.TotalSize > 0).ToArray();
            var diskTotalBytes = drives.Sum(d => d.TotalSize);
            var diskUsedBytes = drives.Sum(d => d.TotalSize - d.AvailableFreeSpace);

            var loadAvg = GetLoadAverage();
            var processCount = Process.GetProcesses().Length;
            var startTime = _currentProcess.StartTime.ToUniversalTime();
            var uptimeSeconds = (int)(DateTime.UtcNow - startTime).TotalSeconds;

            return new ServerSnapshotDto
            {
                Hostname = Environment.MachineName,
                Os = RuntimeInformation.OSDescription,
                KernelVersion = Environment.OSVersion.Version.ToString(),
                CpuModel = GetCpuModel(),
                CpuCores = Environment.ProcessorCount,
                CpuUsagePercent = cpuUsagePercent,
                MemoryTotalBytes = memoryTotalBytes,
                MemoryUsedBytes = memUsedBytes,
                MemoryCachedBytes = memoryCachedBytes,
                DiskTotalBytes = diskTotalBytes,
                DiskUsedBytes = diskUsedBytes,
                DiskReadBytesPerSec = 0,
                DiskWriteBytesPerSec = 0,
                LoadAvg1 = loadAvg.avg1,
                LoadAvg5 = loadAvg.avg5,
                LoadAvg15 = loadAvg.avg15,
                ProcessCount = processCount,
                UptimeSeconds = uptimeSeconds,
                BootTime = startTime.ToString("O", CultureInfo.InvariantCulture),
                DotnetRuntimeVersion = RuntimeInformation.FrameworkDescription,
                GcTotalCollections = GC.CollectionCount(0) + GC.CollectionCount(1) + GC.CollectionCount(2),
                SampledAt = now.ToString("O", CultureInfo.InvariantCulture)
            };
        }
    }

    private double CalculateCpuUsage(DateTime now, TimeSpan totalProcessorTime)
    {
        var elapsed = now - _lastCpuSample;
        var cpuElapsed = totalProcessorTime - _lastTotalProcessorTime;
        if (elapsed.TotalSeconds <= 0) return 0;
        var cores = Environment.ProcessorCount > 0 ? Environment.ProcessorCount : 1;
        var usage = cpuElapsed.TotalSeconds / (elapsed.TotalSeconds * cores) * 100;
        return Math.Min(100, Math.Max(0, usage));
    }

    private static long GetTotalPhysicalMemory()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return 0;
        try
        {
            var lines = File.ReadAllLines("/proc/meminfo");
            var memTotalLine = lines.FirstOrDefault(l => l.StartsWith("MemTotal:", StringComparison.Ordinal));
            if (memTotalLine != null)
            {
                var parts = memTotalLine.Split(':', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    var valuePart = parts[1].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (valuePart.Length >= 1 && long.TryParse(valuePart[0], NumberStyles.Any, CultureInfo.InvariantCulture, out var kb))
                    {
                        return kb * 1024;
                    }
                }
            }
        }
        catch (Exception)
        {
            // 降级返回 0
        }
        return 0;
    }

    private static (double avg1, double avg5, double avg15) GetLoadAverage()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return (0, 0, 0);
        try
        {
            var lines = File.ReadAllLines("/proc/loadavg");
            if (lines.Length > 0)
            {
                var parts = lines[0].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3
                    && double.TryParse(parts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out var avg1)
                    && double.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out var avg5)
                    && double.TryParse(parts[2], NumberStyles.Any, CultureInfo.InvariantCulture, out var avg15))
                {
                    return (avg1, avg5, avg15);
                }
            }
        }
        catch (Exception)
        {
            // 降级返回 0
        }
        return (0, 0, 0);
    }

    private static string GetCpuModel()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            try
            {
                var lines = File.ReadAllLines("/proc/cpuinfo");
                var modelLine = lines.FirstOrDefault(l => l.StartsWith("model name", StringComparison.OrdinalIgnoreCase));
                if (modelLine != null)
                {
                    var idx = modelLine.IndexOf(':');
                    if (idx >= 0 && idx + 1 < modelLine.Length)
                    {
                        return modelLine[(idx + 1)..].Trim();
                    }
                }
            }
            catch (Exception)
            {
                // 降级返回架构信息
            }
        }
        return RuntimeInformation.OSArchitecture.ToString();
    }
}
