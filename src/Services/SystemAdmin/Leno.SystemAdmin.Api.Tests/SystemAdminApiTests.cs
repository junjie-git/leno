using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Leno.Infrastructure.Auth;
using Leno.SharedContracts.Responses;
using Leno.SystemAdmin.Application;
using Leno.SystemAdmin.Application.DTOs;
using Leno.SystemAdmin.Domain.ValueObjects;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Leno.SystemAdmin.Api.Tests;

public class SystemAdminApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly Mock<IAuditLogAppService> _auditLogAppServiceMock = new();
    private readonly Mock<IFeatureFlagAppService> _featureFlagAppServiceMock = new();
    private readonly Mock<IAnnouncementAppService> _announcementAppServiceMock = new();
    private readonly Mock<ICurrentUserContext> _currentUserMock = new();

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid OperatorId = Guid.NewGuid();

    public SystemAdminApiTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Environment", "Testing");

            builder.ConfigureServices(services =>
            {
                services.AddSingleton(_auditLogAppServiceMock.Object);
                services.AddSingleton(_featureFlagAppServiceMock.Object);
                services.AddSingleton(_announcementAppServiceMock.Object);
                services.AddSingleton(_currentUserMock.Object);

                RemoveMassTransitServices(services);
                RemoveElasticsearchServices(services);

                services.AddAuthentication(defaultScheme: "Test")
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
            });
        }).CreateClient();

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test");
    }

    private static void RemoveMassTransitServices(IServiceCollection services)
    {
        var descriptors = services
            .Where(s => s.ServiceType.FullName?.Contains("MassTransit") == true
                     || s.ImplementationType?.FullName?.Contains("MassTransit") == true
                     || s.ServiceType == typeof(MassTransit.IBus)
                     || s.ServiceType == typeof(MassTransit.IBusControl)
                     || s.ServiceType.FullName?.StartsWith("MassTransit.", StringComparison.Ordinal) == true)
            .ToList();
        foreach (var d in descriptors) services.Remove(d);
    }

    private static void RemoveElasticsearchServices(IServiceCollection services)
    {
        var descriptors = services
            .Where(s => s.ServiceType.FullName?.Contains("Elasticsearch") == true
                     || s.ServiceType.FullName?.Contains("Elastic") == true
                     || s.ServiceType.FullName?.Contains("Nest") == true
                     || s.ImplementationType?.FullName?.Contains("Elastic") == true)
            .ToList();
        foreach (var d in descriptors) services.Remove(d);
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
        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.GetAsync("/api/admin/audit-logs");
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task QueryAuditLogs_WithAdminRole_ShouldReturn200()
    {
        SetupAdminAuth();
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
        _auditLogAppServiceMock.Setup(s => s.QueryAuditLogsAsync(
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
        SetupAdminAuth();
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
        _featureFlagAppServiceMock.Setup(s => s.QueryAsync(
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
        SetupAdminAuth();
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
        _announcementAppServiceMock.Setup(s => s.QueryAsync(
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

    private void SetupAdminAuth()
    {
        _currentUserMock.SetupGet(c => c.IsAuthenticated).Returns(true);
        _currentUserMock.SetupGet(c => c.UserId).Returns(UserId);
        _currentUserMock.SetupGet(c => c.Role).Returns("Admin");
    }
}

public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.ContainsKey("Authorization"))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "test"),
            new Claim(ClaimTypes.Role, "Buyer"),
            new Claim(ClaimTypes.Role, "Seller"),
            new Claim(ClaimTypes.Role, "Admin"),
            new Claim(ClaimTypes.Role, "Operator")
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "Test");

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
