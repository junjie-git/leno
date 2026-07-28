using System.Net;
using System.Net.Http.Json;
using Leno.Infrastructure.Auth;
using Leno.SharedContracts.Responses;
using Leno.SystemAdmin.Application;
using Leno.SystemAdmin.Application.DTOs;
using Leno.SystemAdmin.Domain.ValueObjects;
using Moq;

namespace Leno.SystemAdmin.Api.Tests;

/// <summary>
/// SystemAdmin API 基础集成测试：健康检查、鉴权 401/403、AuditLogs/FeatureFlags/Announcements 端点。
/// 使用 SystemAdminApiFactory 提供的 Mock 单例与应用服务替换，验证路由、RBAC、ApiResponse 包装。
/// </summary>
public class SystemAdminApiTests : IClassFixture<SystemAdminApiFactory>
{
    private readonly SystemAdminApiFactory _factory;
    private readonly HttpClient _client;

    private static readonly Guid OperatorId = Guid.NewGuid();

    public SystemAdminApiTests(SystemAdminApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAdminClient();
    }

    [Fact]
    public async Task HealthLive_ShouldReturnOk()
    {
        var response = await _client.GetAsync("/health/live");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UnauthorizedRequest_ShouldReturn401()
    {
        var anonymousClient = _factory.CreateAnonymousClient();
        var response = await anonymousClient.GetAsync("/api/admin/audit-logs");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task QueryAuditLogs_WithAdminRole_ShouldReturn200()
    {
        var listResult = new AuditLogListResultDto
        {
            Items = new List<AuditLogDto>
            {
                new()
                {
                    LogId = Guid.NewGuid(),
                    OperatorId = OperatorId,
                    Action = "Create",
                    ResourceType = "Shop",
                    ResourceId = "shop-001",
                    ResponseStatus = 200,
                    IpAddress = "127.0.0.1",
                    OccurredAt = DateTime.UtcNow
                }
            },
            Total = 1,
            Page = 1,
            PageSize = 20
        };
        _factory.AuditLogAppServiceMock.Setup(s => s.QueryAuditLogsAsync(
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(listResult);

        var response = await _client.GetAsync("/api/admin/audit-logs");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<AuditLogListResultDto>>();
        body!.Data!.Total.Should().Be(1);
        body.Data.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task QueryFeatureFlags_WithAdminRole_ShouldReturn200()
    {
        var listResult = new FeatureFlagListResultDto
        {
            Items = new List<FeatureFlagDto>
            {
                new()
                {
                    FlagId = Guid.NewGuid(),
                    Key = "new_checkout_flow",
                    Name = "新版结账流程",
                    Description = "灰度发布新版结账页面",
                    IsEnabled = true,
                    Strategy = FeatureFlagStrategy.Percentage,
                    Rules = "{\"percentage\":30}",
                    UpdatedAt = DateTime.UtcNow
                }
            },
            Total = 1,
            Page = 1,
            PageSize = 20
        };
        _factory.FeatureFlagAppServiceMock.Setup(s => s.QueryAsync(
                It.IsAny<string?>(),
                It.IsAny<FeatureFlagStatus?>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(listResult);

        var response = await _client.GetAsync("/api/admin/feature-flags");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<FeatureFlagListResultDto>>();
        body!.Data!.Total.Should().Be(1);
        body.Data.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task QueryAnnouncements_WithAdminRole_ShouldReturn200()
    {
        var listResult = new AnnouncementListResultDto
        {
            Items = new List<AnnouncementDto>
            {
                new()
                {
                    AnnouncementId = Guid.NewGuid(),
                    Title = "系统维护通知",
                    Content = "系统将于今晚 22:00-23:00 进行维护",
                    Type = AnnouncementType.Maintenance,
                    Status = AnnouncementStatus.Published,
                    PublishAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }
            },
            Total = 1,
            Page = 1,
            PageSize = 20
        };
        _factory.AnnouncementAppServiceMock.Setup(s => s.QueryAsync(
                It.IsAny<AnnouncementType?>(),
                It.IsAny<AnnouncementStatus?>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(listResult);

        var response = await _client.GetAsync("/api/admin/announcements");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<AnnouncementListResultDto>>();
        body!.Data!.Total.Should().Be(1);
        body.Data.Items.Should().HaveCount(1);
    }
}
