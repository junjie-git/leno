using Leno.SystemAdmin.Infrastructure.Services;

namespace Leno.SystemAdmin.Infrastructure.Tests.Services;

public sealed class DotNetProcessMonitorServiceTests
{
    [Fact]
    public async Task GetSnapshotAsync_ReturnsAllFields()
    {
        var monitor = new DotNetProcessMonitorService();

        var snapshot = await monitor.GetSnapshotAsync(default);

        snapshot.Hostname.Should().NotBeEmpty();
        snapshot.Os.Should().NotBeEmpty();
        snapshot.CpuModel.Should().NotBeEmpty();
        snapshot.CpuCores.Should().BeGreaterThan(0);
        snapshot.CpuUsagePercent.Should().BeGreaterThanOrEqualTo(0);
        snapshot.MemoryTotalBytes.Should().BeGreaterThanOrEqualTo(0);
        snapshot.MemoryUsedBytes.Should().BeGreaterThan(0);
        snapshot.DiskTotalBytes.Should().BeGreaterThan(0);
        snapshot.LoadAvg1.Should().BeGreaterThanOrEqualTo(0);
        snapshot.LoadAvg5.Should().BeGreaterThanOrEqualTo(0);
        snapshot.LoadAvg15.Should().BeGreaterThanOrEqualTo(0);
        snapshot.ProcessCount.Should().BeGreaterThan(0);
        snapshot.UptimeSeconds.Should().BeGreaterThanOrEqualTo(0);
        snapshot.BootTime.Should().NotBeEmpty();
        snapshot.DotnetRuntimeVersion.Should().NotBeEmpty();
        snapshot.GcTotalCollections.Should().BeGreaterThanOrEqualTo(0);
        snapshot.SampledAt.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetSnapshotAsync_CpuUsageCalculation_InRange()
    {
        var monitor = new DotNetProcessMonitorService();

        var first = await monitor.GetSnapshotAsync(default);
        await Task.Delay(100);
        var second = await monitor.GetSnapshotAsync(default);

        second.CpuUsagePercent.Should().BeInRange(0, 100);
        second.SampledAt.Should().NotBe(first.SampledAt);
    }

    [Fact]
    public async Task GetSnapshotAsync_KernelVersion_NotEmpty()
    {
        var monitor = new DotNetProcessMonitorService();

        var snapshot = await monitor.GetSnapshotAsync(default);

        snapshot.KernelVersion.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetSnapshotAsync_MultipleCalls_MemoryUsedPositive()
    {
        var monitor = new DotNetProcessMonitorService();

        for (int i = 0; i < 3; i++)
        {
            var snapshot = await monitor.GetSnapshotAsync(default);
            snapshot.MemoryUsedBytes.Should().BeGreaterThan(0);
        }
    }
}
