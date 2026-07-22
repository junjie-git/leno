using System.Net;
using System.Net.Http.Json;
using Leno.Cart.Application;
using Leno.Cart.Application.DTOs;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Leno.Cart.Api.Tests;

/// <summary>
/// P1-7 + P2-6 匿名购物车控制器 X-Cart-Session 请求头行为测试。
/// 限流策略（10 次/分钟）在 AnonymousCartRateLimitTests 中独立验证，本类请求数控制在 10 次以内。
/// </summary>
public class AnonymousCartHeaderTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly Mock<IAnonymousCartAppService> _anonymousCartAppServiceMock = new();

    private const string SessionId = "test-session-abc-123";
    private static readonly Guid SkuId = Guid.NewGuid();

    public AnonymousCartHeaderTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Environment", "Testing");

            builder.ConfigureServices(services =>
            {
                services.AddSingleton(_anonymousCartAppServiceMock.Object);

                RemoveMassTransitServices(services);
                RemoveElasticsearchServices(services);

                // 与 CartApiTests 对齐：替换为 Test 认证方案，避免 JwtBearer 配置依赖
                services.AddAuthentication(defaultScheme: "Test")
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
            });
        }).CreateClient();

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test");
    }

    [Fact]
    public async Task CreateCart_ShouldReturnSessionId()
    {
        _anonymousCartAppServiceMock.Setup(s => s.CreateCartAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AnonymousCartResponseDto
            {
                SessionId = SessionId,
                Cart = new CartDto { Id = Guid.NewGuid(), UserId = Guid.NewGuid() }
            });

        var response = await _client.PostAsync("/api/cart/anonymous", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<AnonymousCartResponseDto>>();
        body!.Data!.SessionId.Should().Be(SessionId);
    }

    [Fact]
    public async Task GetCart_WithXCartSessionHeader_ShouldCallService()
    {
        _anonymousCartAppServiceMock.Setup(s => s.GetCartAsync(SessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CartDto { Id = Guid.NewGuid(), UserId = Guid.NewGuid() });

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/cart/anonymous");
        request.Headers.Add("X-Cart-Session", SessionId);
        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _anonymousCartAppServiceMock.Verify(s => s.GetCartAsync(SessionId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetCart_WithoutXCartSessionHeader_ShouldReturnErrorAndNotCallService()
    {
        var response = await _client.GetAsync("/api/cart/anonymous");

        response.StatusCode.Should().NotBe(HttpStatusCode.OK);
        _anonymousCartAppServiceMock.Verify(
            s => s.GetCartAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddItem_WithXCartSessionHeader_ShouldCallService()
    {
        _anonymousCartAppServiceMock.Setup(s => s.AddItemAsync(SessionId, It.IsAny<AddCartItemDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CartDto { Id = Guid.NewGuid(), UserId = Guid.NewGuid() });

        var dto = new { SkuId = SkuId, Quantity = 2, SellerId = Guid.NewGuid() };
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/cart/anonymous/items")
        {
            Content = JsonContent.Create(dto)
        };
        request.Headers.Add("X-Cart-Session", SessionId);
        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _anonymousCartAppServiceMock.Verify(
            s => s.AddItemAsync(SessionId, It.IsAny<AddCartItemDto>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PreviewCheckout_WithXCartSessionHeader_ShouldReturnPreview()
    {
        var preview = new CheckoutPreviewDto
        {
            Groups = Array.Empty<CheckoutGroupDto>(),
            TotalAmount = 99.9m,
            Currency = "CNY",
            TotalCount = 2
        };
        _anonymousCartAppServiceMock.Setup(s => s.PreviewCheckoutAsync(SessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(preview);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/cart/anonymous/preview");
        request.Headers.Add("X-Cart-Session", SessionId);
        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<CheckoutPreviewDto>>();
        body!.Data!.TotalAmount.Should().Be(99.9m);
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
}

/// <summary>
/// P1-7 限流策略验证：同 IP 连续 11 次请求匿名购物车接口，第 11 次返回 429 TooManyRequests。
/// 独立 IClassFixture 实例，确保限流计数器与其他测试类隔离。
/// </summary>
public class AnonymousCartRateLimitTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly Mock<IAnonymousCartAppService> _anonymousCartAppServiceMock = new();

    public AnonymousCartRateLimitTests(WebApplicationFactory<Program> factory)
    {
        _anonymousCartAppServiceMock.Setup(s => s.CreateCartAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AnonymousCartResponseDto
            {
                SessionId = "session-rate-limit",
                Cart = new CartDto()
            });

        _client = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Environment", "Testing");

            builder.ConfigureServices(services =>
            {
                services.AddSingleton(_anonymousCartAppServiceMock.Object);

                RemoveMassTransitServices(services);
                RemoveElasticsearchServices(services);

                services.AddAuthentication(defaultScheme: "Test")
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
            });
        }).CreateClient();

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test");
    }

    [Fact]
    public async Task ElevenRequestsFromSameIp_EleventhShouldReturn429()
    {
        // 前 10 次请求在限额内，均应成功
        for (var i = 0; i < 10; i++)
        {
            var response = await _client.PostAsync("/api/cart/anonymous", null);
            response.StatusCode.Should().Be(HttpStatusCode.OK, $"第 {i + 1} 次请求应在限额内成功");
        }

        // 第 11 次请求超出限额，应返回 429
        var rejected = await _client.PostAsync("/api/cart/anonymous", null);
        rejected.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
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
}
