using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Leno.Infrastructure.Abstractions.Cqrs;
using Leno.Infrastructure.Auth;
using Leno.SharedContracts.Responses;
using Leno.SellerShop.Application;
using Leno.SellerShop.Application.DTOs;
using Leno.SellerShop.Application.Queries;
using Leno.SellerShop.Domain.ValueObjects;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Leno.SellerShop.Api.Tests;

public class SellerShopApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly Mock<IShopAppService> _shopAppServiceMock = new();
    private readonly Mock<ISellerDashboardAppService> _dashboardAppServiceMock = new();
    private readonly Mock<ICurrentUserContext> _currentUserMock = new();
    // SellerDashboardController 还依赖 ES 读模型查询处理器，测试中一并 Mock
    private readonly Mock<IQueryHandler<ShopDashboardQuery, ShopDashboardResult?>> _dashboardQueryHandlerMock = new();

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid ShopId = Guid.NewGuid();

    public SellerShopApiTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Environment", "Testing");
            TestWebHostHelper.UseSensitiveConfigPlaceholders(builder);

            builder.ConfigureServices(services =>
            {
                services.AddSingleton(_shopAppServiceMock.Object);
                services.AddSingleton(_dashboardAppServiceMock.Object);
                services.AddSingleton(_currentUserMock.Object);
                services.AddSingleton(_dashboardQueryHandlerMock.Object);

                RemoveMassTransitServices(services);
                RemoveElasticsearchServices(services);
                // 测试环境无 Redis：替换分布式锁使 MigrateWithLockAsync 跳过迁移
                TestWebHostHelper.ReplaceDistributedLockWithNullProvider(services);

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
        var response = await _client.GetAsync("/api/shops/me");
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetShopInfo_WithValidToken_ShouldReturn200()
    {
        SetupSellerAuth();
        _shopAppServiceMock.Setup(s => s.GetMyShopAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateShopDto());

        var response = await _client.GetAsync("/api/shops/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<ShopDto>>();
        body!.Data!.Id.Should().Be(ShopId);
        body.Data.SellerId.Should().Be(UserId);
    }

    [Fact]
    public async Task SubmitShopApplication_WithValidPayload_ShouldReturn200()
    {
        SetupSellerAuth();
        _shopAppServiceMock.Setup(s => s.SubmitShopApplicationAsync(UserId, It.IsAny<SubmitShopApplicationDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateShopDto());

        var dto = new
        {
            ShopName = "测试店铺",
            ContactPhone = "13800000000",
            ContactEmail = "shop@test.com",
            RealName = "张三",
            BusinessLicenseNo = "91110000123456789X",
            Description = "测试店铺描述"
        };
        var response = await _client.PostAsJsonAsync("/api/shops/application", dto);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _shopAppServiceMock.Verify(
            s => s.SubmitShopApplicationAsync(UserId, It.IsAny<SubmitShopApplicationDto>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetSellerDashboard_WithSellerRole_ShouldReturn200()
    {
        SetupSellerAuth();
        var dashboard = new SellerDashboardDto
        {
            ShopId = ShopId,
            ShopName = "测试店铺",
            Status = ShopStatus.Active,
            ProductCount = 10,
            TotalOrders = 50,
            PendingOrders = 5,
            CompletedOrders = 40,
            TotalRevenue = 9999.99m,
            TodayOrderCount = 3,
            TodaySalesAmount = 299.00m,
            TodaySalesCurrency = "CNY",
            TodayAvgRating = 4.5m,
            TodayRatingCount = 2,
            TodayRefundCount = 0
        };
        _dashboardAppServiceMock.Setup(s => s.GetDashboardAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dashboard);

        var response = await _client.GetAsync("/api/seller/dashboard");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<SellerDashboardDto>>();
        body!.Data!.ShopId.Should().Be(ShopId);
        body.Data.TotalOrders.Should().Be(50);
    }

    private void SetupSellerAuth()
    {
        _currentUserMock.SetupGet(c => c.IsAuthenticated).Returns(true);
        _currentUserMock.SetupGet(c => c.UserId).Returns(UserId);
        _currentUserMock.SetupGet(c => c.Role).Returns("Seller");
    }

    private static ShopDto CreateShopDto()
    {
        return new ShopDto
        {
            Id = ShopId,
            SellerId = UserId,
            ShopName = "测试店铺",
            ContactPhone = "13800000000",
            ContactEmail = "shop@test.com",
            BusinessLicenseNo = "91110000123456789X",
            Status = ShopStatus.PendingReview,
            ProductCount = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
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
