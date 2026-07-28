using Leno.SystemAdmin.Application.Services;
using Leno.SystemAdmin.Domain.Exceptions;
using Leno.SystemAdmin.Domain.Services;
using Leno.SystemAdmin.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Leno.SystemAdmin.Application.Tests.Services;

public sealed class ServerMonitorAppServiceTests
{
    private readonly Mock<IDotNetProcessMonitor> _processMonitor = new();
    private readonly Mock<IMetricHistoryStore> _historyStore = new();
    private readonly ServerMonitorAppService _service;

    public ServerMonitorAppServiceTests()
    {
        _service = new ServerMonitorAppService(_processMonitor.Object, _historyStore.Object, NullLogger<ServerMonitorAppService>.Instance);
    }

    [Fact]
    public async Task GetSnapshotAsync_ReturnsAllFields()
    {
        _processMonitor.Setup(m => m.GetSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildSampleSnapshot());

        var snapshot = await _service.GetSnapshotAsync(default);

        snapshot.CpuUsagePercent.Should().BeGreaterThanOrEqualTo(0);
        snapshot.MemoryUsedBytes.Should().BeGreaterThanOrEqualTo(0);
        snapshot.Hostname.Should().NotBeNullOrEmpty();
        snapshot.Os.Should().NotBeNullOrEmpty();
        snapshot.DotnetRuntimeVersion.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetSnapshotAsync_CpuUsageCalculation()
    {
        _processMonitor.SetupSequence(m => m.GetSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildSampleSnapshot(cpuUsage: 10))
            .ReturnsAsync(BuildSampleSnapshot(cpuUsage: 30));

        var first = await _service.GetSnapshotAsync(default);
        var second = await _service.GetSnapshotAsync(default);

        first.CpuUsagePercent.Should().Be(10);
        second.CpuUsagePercent.Should().Be(30);
    }

    [Fact]
    public async Task GetHistoryAsync_CpuMetric_Returns300Points()
    {
        var points = Enumerable.Range(0, 300)
            .Select(i => new MetricPointDto { Timestamp = DateTime.UtcNow.AddSeconds(-300 + i), Value = i })
            .ToList();
        _historyStore.Setup(s => s.GetHistoryAsync(MetricName.Cpu, It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(points);

        var history = await _service.GetHistoryAsync("cpu", 300, default);

        history.Points.Should().HaveCount(300);
        history.Metric.Should().Be("cpu");
    }

    [Fact]
    public async Task GetHistoryAsync_RangeFilter_ReturnsLast5Min()
    {
        var now = DateTime.UtcNow;
        var points = new List<MetricPointDto>
        {
            new() { Timestamp = now.AddSeconds(-280), Value = 1 },
            new() { Timestamp = now.AddSeconds(-100), Value = 2 }
        };
        _historyStore.Setup(s => s.GetHistoryAsync(MetricName.Memory, TimeSpan.FromSeconds(300), It.IsAny<CancellationToken>()))
            .ReturnsAsync(points);

        var history = await _service.GetHistoryAsync("memory", 300, default);

        history.Points.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetHistoryAsync_InvalidMetric_ThrowsArgumentException()
    {
        var act = () => _service.GetHistoryAsync("invalid-metric", 300, default);

        await act.Should().ThrowAsync<SystemAdminDomainException>()
            .Where(e => e.ErrorCode == "SERVER_MONITOR_METRIC_INVALID");
    }

    private static ServerSnapshotDto BuildSampleSnapshot(double cpuUsage = 10) => new()
    {
        Hostname = Environment.MachineName,
        Os = Environment.OSVersion.ToString(),
        KernelVersion = "6.1.0",
        CpuModel = "x64",
        CpuCores = 8,
        CpuUsagePercent = cpuUsage,
        MemoryTotalBytes = 8_000_000_000L,
        MemoryUsedBytes = 4_000_000_000L,
        MemoryCachedBytes = 1_000_000_000L,
        DiskTotalBytes = 100_000_000_000L,
        DiskUsedBytes = 50_000_000_000L,
        DiskReadBytesPerSec = 1024,
        DiskWriteBytesPerSec = 2048,
        LoadAvg1 = 0.5,
        LoadAvg5 = 0.4,
        LoadAvg15 = 0.3,
        ProcessCount = 100,
        UptimeSeconds = 3600,
        BootTime = DateTime.UtcNow.AddHours(-1).ToString("o"),
        DotnetRuntimeVersion = Environment.Version.ToString(),
        GcTotalCollections = 1,
        SampledAt = DateTime.UtcNow.ToString("o")
    };
}
