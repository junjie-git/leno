using Leno.SystemAdmin.Domain.Services;
using Leno.SystemAdmin.Domain.ValueObjects;
using Leno.SystemAdmin.Infrastructure.BackgroundServices;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Leno.SystemAdmin.Infrastructure.Tests.BackgroundServices;

public sealed class ServerMetricSamplerBackgroundServiceTests
{
    [Fact]
    public async Task ExecuteAsync_SamplesMetric_RecordedIntoStore()
    {
        var monitorMock = new Mock<IDotNetProcessMonitor>();
        monitorMock.Setup(m => m.GetSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServerSnapshotDto
            {
                CpuUsagePercent = 50.0,
                MemoryTotalBytes = 1024,
                MemoryUsedBytes = 512,
                DiskReadBytesPerSec = 100,
                DiskWriteBytesPerSec = 200
            });
        var storeMock = new Mock<IMetricHistoryStore>();
        var service = new ServerMetricSamplerBackgroundService(monitorMock.Object, storeMock.Object, NullLogger<ServerMetricSamplerBackgroundService>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        await InvokeExecuteAsync(service, cts.Token);

        storeMock.Verify(s => s.RecordAsync(MetricName.Cpu, 50.0, It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        storeMock.Verify(s => s.RecordAsync(MetricName.Memory, It.IsAny<double>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        storeMock.Verify(s => s.RecordAsync(MetricName.DiskIo, 300.0, It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_MonitorThrows_LogsErrorButContinues()
    {
        var monitorMock = new Mock<IDotNetProcessMonitor>();
        monitorMock.Setup(m => m.GetSnapshotAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("test error"));
        var storeMock = new Mock<IMetricHistoryStore>();
        var service = new ServerMetricSamplerBackgroundService(monitorMock.Object, storeMock.Object, NullLogger<ServerMetricSamplerBackgroundService>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        await InvokeExecuteAsync(service, cts.Token);

        storeMock.Verify(s => s.RecordAsync(It.IsAny<MetricName>(), It.IsAny<double>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static async Task InvokeExecuteAsync(ServerMetricSamplerBackgroundService service, CancellationToken ct)
    {
        var method = typeof(ServerMetricSamplerBackgroundService).GetMethod("ExecuteAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (method is null) throw new InvalidOperationException("ExecuteAsync not found");
        var task = (Task)method.Invoke(service, new object[] { ct })!;
        try
        {
            await task;
        }
        catch (OperationCanceledException)
        {
            // 正常取消
        }
    }
}
