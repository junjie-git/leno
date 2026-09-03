using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Leno.Infrastructure.Auth;
using Leno.ReviewAfterSales.Application;
using Leno.ReviewAfterSales.Application.DTOs;
using Leno.ReviewAfterSales.Domain.ValueObjects;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Leno.ReviewAfterSales.Api.Tests;

public class ReviewAfterSalesApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly Mock<IReviewAppService> _reviewAppServiceMock = new();
    private readonly Mock<IAfterSalesAppService> _afterSalesAppServiceMock = new();
    private readonly Mock<ICurrentUserContext> _currentUserMock = new();

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid OrderId = Guid.NewGuid();
    private static readonly Guid OrderLineId = Guid.NewGuid();
    private static readonly Guid SpuId = Guid.NewGuid();
    private static readonly Guid SkuId = Guid.NewGuid();
    private static readonly Guid SellerId = Guid.NewGuid();
    private static readonly Guid ReviewId = Guid.NewGuid();
    private static readonly Guid AfterSalesId = Guid.NewGuid();

    public ReviewAfterSalesApiTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Environment", "Testing");
            TestWebHostHelper.UseSensitiveConfigPlaceholders(builder);

            builder.ConfigureServices(services =>
            {
                services.AddSingleton(_reviewAppServiceMock.Object);
                services.AddSingleton(_afterSalesAppServiceMock.Object);
                services.AddSingleton(_currentUserMock.Object);

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
        var response = await _client.GetAsync("/api/reviews/mine");
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SubmitReview_WithValidPayload_ShouldReturn201()
    {
        SetupBuyerAuth();
        _reviewAppServiceMock.Setup(s => s.SubmitReviewAsync(UserId, It.IsAny<SubmitReviewDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateReviewDto());

        var dto = new
        {
            OrderId = OrderId,
            OrderLineId = OrderLineId,
            SpuId = SpuId,
            SkuId = SkuId,
            Rating = 5,
            Content = "商品质量很好，物流也快！",
            Images = new List<string>()
        };
        var response = await _client.PostAsJsonAsync("/api/reviews", dto);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        _reviewAppServiceMock.Verify(
            s => s.SubmitReviewAsync(UserId, It.IsAny<SubmitReviewDto>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetProductReviews_WithoutAuth_ShouldReturn200()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var listResult = new ReviewListResultDto
        {
            Items = new List<ReviewDto> { CreateReviewDto() },
            Total = 1,
            Page = 1,
            PageSize = 20
        };
        _reviewAppServiceMock.Setup(s => s.GetReviewsBySpuAsync(SpuId, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(listResult);

        var response = await _client.GetAsync($"/api/products/{SpuId}/reviews");
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<ReviewListResultDto>>();
        body!.Data!.Total.Should().Be(1);
        body.Data.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task SubmitAfterSales_WithValidPayload_ShouldReturn201()
    {
        SetupBuyerAuth();
        _afterSalesAppServiceMock.Setup(s => s.SubmitAfterSalesAsync(UserId, It.IsAny<SubmitAfterSalesDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateAfterSalesDto());

        var dto = new
        {
            OrderId = OrderId,
            OrderLineId = OrderLineId,
            SellerId = SellerId,
            Type = AfterSalesType.ReturnRefund,
            ReasonCategory = "商品质量问题",
            Reason = "收到的商品有破损",
            Images = new List<string>(),
            RequestedAmount = 199.00m,
            Currency = "CNY"
        };
        var response = await _client.PostAsJsonAsync("/api/after-sales", dto);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        _afterSalesAppServiceMock.Verify(
            s => s.SubmitAfterSalesAsync(UserId, It.IsAny<SubmitAfterSalesDto>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private void SetupBuyerAuth()
    {
        _currentUserMock.SetupGet(c => c.IsAuthenticated).Returns(true);
        _currentUserMock.SetupGet(c => c.UserId).Returns(UserId);
        _currentUserMock.SetupGet(c => c.Role).Returns("Buyer");
    }

    private static ReviewDto CreateReviewDto()
    {
        return new ReviewDto
        {
            ReviewId = ReviewId,
            OrderId = OrderId,
            OrderLineId = OrderLineId,
            SpuId = SpuId,
            SkuId = SkuId,
            UserId = UserId,
            Rating = 5,
            Content = "商品质量很好，物流也快！",
            Images = new List<string>(),
            Status = ReviewStatus.Pending,
            SubmittedAt = DateTime.UtcNow
        };
    }

    private static AfterSalesDto CreateAfterSalesDto()
    {
        return new AfterSalesDto
        {
            AfterSalesId = AfterSalesId,
            OrderId = OrderId,
            OrderLineId = OrderLineId,
            UserId = UserId,
            SellerId = SellerId,
            Type = AfterSalesType.ReturnRefund,
            ReasonCategory = "商品质量问题",
            Reason = "收到的商品有破损",
            Images = new List<string>(),
            RequestedAmount = 199.00m,
            Currency = "CNY",
            Status = AfterSalesStatus.Pending,
            AppliedAt = DateTime.UtcNow
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
