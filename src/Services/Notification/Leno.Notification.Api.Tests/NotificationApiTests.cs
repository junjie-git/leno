using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Leno.Infrastructure.Auth;
using Leno.Notification.Application;
using Leno.Notification.Application.DTOs;
using Leno.Notification.Domain.ValueObjects;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;

namespace Leno.Notification.Api.Tests;

public class NotificationApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly Mock<INotificationRecordAppService> _recordAppServiceMock = new();
    private readonly Mock<INotificationConfigAppService> _configAppServiceMock = new();
    private readonly Mock<ICurrentUserContext> _currentUserMock = new();

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid RecordId = Guid.NewGuid();

    public NotificationApiTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Environment", "Testing");
            TestWebHostHelper.UseSensitiveConfigPlaceholders(builder);

            builder.ConfigureServices(services =>
            {
                services.AddSingleton(_recordAppServiceMock.Object);
                services.AddSingleton(_configAppServiceMock.Object);
                services.AddSingleton(_currentUserMock.Object);

                RemoveMassTransitServices(services);
                RemoveElasticsearchServices(services);
                // 测试环境无 Redis：替换分布式锁使 MigrateWithLockAsync 跳过迁移，
                // 并替换 IConnectionMultiplexer 避免请求链路触发真实 Redis 连接
                TestWebHostHelper.ReplaceDistributedLockWithNullProvider(services);
                TestWebHostHelper.ReplaceRedisWithMock(services);

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

    private static void ReplaceRedisWithMock(IServiceCollection services)
    {
        var descriptors = services
            .Where(s => s.ServiceType == typeof(IConnectionMultiplexer))
            .ToList();
        foreach (var d in descriptors) services.Remove(d);

        var redisMock = new Mock<IConnectionMultiplexer>();
        services.AddSingleton(redisMock.Object);
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
        var response = await _client.GetAsync("/api/notifications/records");
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task QueryNotificationRecords_WithAdminRole_ShouldReturn200()
    {
        SetupAdminAuth();
        var listResult = new NotificationRecordListResultDto
        {
            Items = new List<NotificationRecordListItemDto>
            {
                new()
                {
                    RecordId = RecordId,
                    UserId = UserId,
                    TemplateCode = "ORDER_PAID",
                    Channel = NotificationChannel.InApp,
                    Title = "订单已支付",
                    Status = NotificationStatus.Succeeded,
                    SentAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow
                }
            },
            Total = 1,
            Page = 1,
            PageSize = 20
        };
        _recordAppServiceMock.Setup(s => s.QueryRecordsAsync(
                It.IsAny<Guid?>(),
                It.IsAny<NotificationChannel?>(),
                It.IsAny<NotificationStatus?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(listResult);

        var response = await _client.GetAsync("/api/notifications/records");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<NotificationRecordListResultDto>>();
        body!.Data!.Total.Should().Be(1);
        body.Data.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetNotificationConfig_WithAdminRole_ShouldReturn200()
    {
        SetupAdminAuth();
        var configDto = new NotificationConfigDto
        {
            Channel = NotificationChannel.Email,
            Enabled = true,
            SmtpHost = "smtp.test.com",
            SmtpPort = 587,
            SmtpUsername = "noreply@test.com",
            SmtpPassword = "******",
            FromAddress = "noreply@test.com",
            UseSsl = true
        };
        _configAppServiceMock.Setup(s => s.GetConfigAsync(NotificationChannel.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(configDto);

        var response = await _client.GetAsync("/api/admin/notification-config?channel=Email");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<NotificationConfigDto>>();
        body!.Data!.Channel.Should().Be(NotificationChannel.Email);
        body.Data.Enabled.Should().BeTrue();
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
