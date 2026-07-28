using System.Net;
using System.Net.Http.Json;
using Leno.SharedContracts.Responses;
using Leno.SystemAdmin.Application;
using Leno.SystemAdmin.Application.DTOs;
using Leno.SystemAdmin.Domain.Exceptions;
using Leno.SystemAdmin.Domain.ValueObjects;
using Moq;

namespace Leno.SystemAdmin.Api.Tests.Controllers;

/// <summary>
/// ServerMonitorController 集成测试（Task 7.16，7 用例）。
/// 覆盖服务器快照、历史指标折线 2 个端点，
/// 验证 200/400/401/403 状态码、metric 与 rangeSeconds 参数校验、ApiResponse 包装。
/// 不依赖 Redis，应用服务以 Mock 替换，断言基于 ApiResponse&lt;T&gt; 字段。
/// </summary>
public class ServerMonitorControllerTests : IClassFixture<SystemAdminApiFactory>
{
    private readonly SystemAdminApiFactory _factory;
    private readonly HttpClient _client;

    public ServerMonitorControllerTests(SystemAdminApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAdminClient();
    }

    #region GET /api/admin/server-monitor/snapshot

    [Fact]
    public async Task GetSnapshot_WithAdminRole_ShouldReturn200()
    {
        var snapshot = new ServerSnapshotDto
        {
            Hostname = "leno-prod-01",
            Os = "Linux 5.15.0-91-generic",
            KernelVersion = "#101-Ubuntu SMP x86_64",
            CpuModel = "Intel Xeon E5-2680 v4 @ 2.40GHz",
            CpuCores = 8,
            CpuUsagePercent = 23.5,
            MemoryTotalBytes = 16L * 1024 * 1024 * 1024,
            MemoryUsedBytes = 6L * 1024 * 1024 * 1024,
            MemoryCachedBytes = 2L * 1024 * 1024 * 1024,
            DiskTotalBytes = 500L * 1024 * 1024 * 1024,
            DiskUsedBytes = 180L * 1024 * 1024 * 1024,
            DiskReadBytesPerSec = 1024 * 1024,
            DiskWriteBytesPerSec = 2 * 1024 * 1024,
            LoadAvg1 = 0.42,
            LoadAvg5 = 0.55,
            LoadAvg15 = 0.61,
            ProcessCount = 132,
            UptimeSeconds = 86400,
            BootTime = "2026-07-27T10:00:00Z",
            DotnetRuntimeVersion = "10.0.0",
            GcTotalCollections = 1024,
            SampledAt = "2026-07-28T10:00:00Z"
        };
        _factory.ServerMonitorAppServiceMock
            .Setup(s => s.GetSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        var response = await _client.GetAsync("/api/admin/server-monitor/snapshot");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<ServerSnapshotDto>>();
        body!.Code.Should().Be(200);
        body.Data!.Hostname.Should().Be("leno-prod-01");
        body.Data.CpuCores.Should().Be(8);
        body.Data.CpuUsagePercent.Should().Be(23.5);
        body.Data.UptimeSeconds.Should().Be(86400);
    }

    [Fact]
    public async Task GetSnapshot_WithoutAuth_ShouldReturn401()
    {
        var anonymousClient = _factory.CreateAnonymousClient();
        var response = await anonymousClient.GetAsync("/api/admin/server-monitor/snapshot");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetSnapshot_WithOperatorRole_ShouldReturn403()
    {
        var operatorClient = _factory.CreateClientWithRole("Operator");
        var response = await operatorClient.GetAsync("/api/admin/server-monitor/snapshot");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    #endregion

    #region GET /api/admin/server-monitor/history

    [Fact]
    public async Task GetHistory_WithValidCpuMetric_ShouldReturn200()
    {
        var history = new MetricHistoryDto
        {
            Metric = "cpu",
            RangeSeconds = 300,
            Points = new List<MetricPointDto>
            {
                new() { Timestamp = DateTime.UtcNow.AddSeconds(-300), Value = 12.3 },
                new() { Timestamp = DateTime.UtcNow.AddSeconds(-240), Value = 18.5 },
                new() { Timestamp = DateTime.UtcNow.AddSeconds(-180), Value = 22.1 },
                new() { Timestamp = DateTime.UtcNow.AddSeconds(-120), Value = 19.7 },
                new() { Timestamp = DateTime.UtcNow.AddSeconds(-60), Value = 15.2 }
            }
        };
        _factory.ServerMonitorAppServiceMock
            .Setup(s => s.GetHistoryAsync("cpu", 300, It.IsAny<CancellationToken>()))
            .ReturnsAsync(history);

        var response = await _client.GetAsync("/api/admin/server-monitor/history?metric=cpu&rangeSeconds=300");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<MetricHistoryDto>>();
        body!.Code.Should().Be(200);
        body.Data!.Metric.Should().Be("cpu");
        body.Data.RangeSeconds.Should().Be(300);
        body.Data.Points.Should().HaveCount(5);
    }

    [Fact]
    public async Task GetHistory_WithoutAuth_ShouldReturn401()
    {
        var anonymousClient = _factory.CreateAnonymousClient();
        var response = await anonymousClient.GetAsync("/api/admin/server-monitor/history?metric=cpu");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetHistory_WithInvalidMetric_ShouldReturn400()
    {
        _factory.ServerMonitorAppServiceMock
            .Setup(s => s.GetHistoryAsync("invalid", It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SystemAdminDomainException(
                "metric 参数非法：invalid（仅支持 cpu/memory/disk-io）",
                "SERVER_MONITOR_METRIC_INVALID"));

        var response = await _client.GetAsync("/api/admin/server-monitor/history?metric=invalid&rangeSeconds=300");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse>();
        body!.Code.Should().Be(400);
    }

    [Fact]
    public async Task GetHistory_WithOutOfRangeSeconds_ShouldReturn400()
    {
        _factory.ServerMonitorAppServiceMock
            .Setup(s => s.GetHistoryAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SystemAdminDomainException(
                "rangeSeconds 必须在 1-3600 范围",
                "SERVER_MONITOR_RANGE_INVALID"));

        // rangeSeconds=0 低于下限 1，应用层应抛 SERVER_MONITOR_RANGE_INVALID
        var response = await _client.GetAsync("/api/admin/server-monitor/history?metric=cpu&rangeSeconds=0");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse>();
        body!.Code.Should().Be(400);
    }

    #endregion
}
