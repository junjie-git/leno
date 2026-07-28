using Leno.SystemAdmin.Domain.ValueObjects;
using Leno.SystemAdmin.Infrastructure.Services;

namespace Leno.SystemAdmin.Infrastructure.Tests.Services;

public sealed class MemoryMetricHistoryStoreTests
{
    [Fact]
    public async Task RecordAsync_SingleMetric_PersistsPoint()
    {
        var store = new MemoryMetricHistoryStore(maxPointsPerMetric: 300);

        await store.RecordAsync(MetricName.Cpu, 50.5, default);

        var history = await store.GetHistoryAsync(MetricName.Cpu, TimeSpan.FromMinutes(5), default);
        history.Should().HaveCount(1);
        history[0].Value.Should().Be(50.5);
    }

    [Fact]
    public async Task GetHistoryAsync_FilterByRange_ReturnsRecentPoints()
    {
        var store = new MemoryMetricHistoryStore(maxPointsPerMetric: 300);
        await store.RecordAsync(MetricName.Memory, 30, default);
        await Task.Delay(50);
        await store.RecordAsync(MetricName.Memory, 40, default);

        var history = await store.GetHistoryAsync(MetricName.Memory, TimeSpan.FromMilliseconds(20), default);

        history.Should().NotBeEmpty();
        history.All(p => p.Timestamp >= DateTime.UtcNow - TimeSpan.FromMilliseconds(20)).Should().BeTrue();
    }

    [Fact]
    public async Task RecordAsync_OverMaxPoints_RollsWindow()
    {
        var store = new MemoryMetricHistoryStore(maxPointsPerMetric: 5);
        for (int i = 0; i < 10; i++)
        {
            await store.RecordAsync(MetricName.Cpu, i, default);
        }

        var history = await store.GetHistoryAsync(MetricName.Cpu, TimeSpan.FromHours(1), default);

        history.Should().HaveCount(5);
        history.Select(p => p.Value).Should().BeEquivalentTo(new[] { 5.0, 6, 7, 8, 9 });
    }

    [Fact]
    public async Task GetHistoryAsync_DifferentMetrics_Isolated()
    {
        var store = new MemoryMetricHistoryStore(maxPointsPerMetric: 300);
        await store.RecordAsync(MetricName.Cpu, 10, default);
        await store.RecordAsync(MetricName.Memory, 20, default);
        await store.RecordAsync(MetricName.DiskIo, 30, default);

        var cpuHistory = await store.GetHistoryAsync(MetricName.Cpu, TimeSpan.FromHours(1), default);
        var memHistory = await store.GetHistoryAsync(MetricName.Memory, TimeSpan.FromHours(1), default);
        var diskHistory = await store.GetHistoryAsync(MetricName.DiskIo, TimeSpan.FromHours(1), default);

        cpuHistory.Should().HaveCount(1);
        memHistory.Should().HaveCount(1);
        diskHistory.Should().HaveCount(1);
        cpuHistory[0].Value.Should().Be(10);
        memHistory[0].Value.Should().Be(20);
        diskHistory[0].Value.Should().Be(30);
    }
}
