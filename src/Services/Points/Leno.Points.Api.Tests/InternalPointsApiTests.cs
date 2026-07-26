using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Leno.Infrastructure.Auth;
using Leno.Points.Application;
using Leno.Points.Application.DTOs;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Leno.Points.Api.Tests;

/// <summary>
/// 积分域内部 API 集成测试。
/// 覆盖 InternalPointsController 4 个内部端点：trial-offset、freeze、release、confirm。
/// 验证 X-Internal-Key 中间件鉴权、单路径路由（internal/v1/points/*）、ApiResponse 包装。
/// </summary>
public class InternalPointsApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly Mock<IPointsAppService> _pointsAppServiceMock = new();
    private readonly Mock<ICheckInAppService> _checkInAppServiceMock = new();
    private readonly Mock<IExchangeCouponAppService> _exchangeCouponAppServiceMock = new();
    private readonly Mock<ITaskAppService> _taskAppServiceMock = new();
    private readonly Mock<IAwardAppService> _awardAppServiceMock = new();
    private readonly Mock<IPointsRuleAppService> _ruleAppServiceMock = new();
    private readonly Mock<IPointsInternalAppService> _internalAppServiceMock = new();
    private readonly Mock<ICurrentUserContext> _currentUserMock = new();

    private const string TestInternalKey = "test-internal-key-points-internal";

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid OrderId = Guid.NewGuid();

    public InternalPointsApiTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(builder =>
        {
            // 使用 Development 环境，跳过程序启动期的敏感配置校验（Program.cs 仅在非 Development 抛异常）
            builder.UseSetting("Environment", "Development");

            builder.ConfigureServices(services =>
            {
                // 先移除真实服务注册（Scoped），再添加 Mock 单例，避免 Remove 方法误删 Mock 注册
                RemoveMassTransitServices(services);
                RemoveElasticsearchServices(services);
                RemoveApplicationServiceRegistrations(services);
                RemoveEventBusServices(services);
                ReplaceDistributedLockProvider(services);

                services.AddSingleton(_pointsAppServiceMock.Object);
                services.AddSingleton(_checkInAppServiceMock.Object);
                services.AddSingleton(_exchangeCouponAppServiceMock.Object);
                services.AddSingleton(_taskAppServiceMock.Object);
                services.AddSingleton(_awardAppServiceMock.Object);
                services.AddSingleton(_ruleAppServiceMock.Object);
                services.AddSingleton(_internalAppServiceMock.Object);
                services.AddSingleton(_currentUserMock.Object);

                services.Configure<InternalApiKeyOptions>(o =>
                {
                    o.ApiKey = TestInternalKey;
                    o.RoutePrefix = "internal/";
                });

                services.AddAuthentication(defaultScheme: "Test")
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
            });
        }).CreateClient();

        // 内部端点不依赖 JWT，但仍需 Authorization 头通过 TestAuthHandler（避免 401 干扰）
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

    /// <summary>
    /// 移除 Program.cs 注册的真实应用服务（Scoped），避免 Development 环境的 ValidateOnBuild
    /// 校验因仓储未注册而失败。移除后由测试注入 Mock 单例替代。
    /// </summary>
    private static void RemoveApplicationServiceRegistrations(IServiceCollection services)
    {
        var appServiceInterfaces = new[]
        {
            typeof(IPointsAppService),
            typeof(ICheckInAppService),
            typeof(IExchangeCouponAppService),
            typeof(IAwardAppService),
            typeof(ITaskAppService),
            typeof(IPointsRuleAppService),
            typeof(IPointsInternalAppService)
        };

        var descriptors = services
            .Where(s => appServiceInterfaces.Contains(s.ServiceType))
            .ToList();
        foreach (var d in descriptors) services.Remove(d);
    }

    /// <summary>
    /// 移除 IEventBus 注册（RabbitMqEventBus 依赖 MassTransit.IPublishEndpoint，移除 MassTransit 后无法构造）。
    /// 注意：仅移除 Leno.Infrastructure.Abstractions.IEventBus，保留 IIntegrationEventMapper（UnitOfWork 依赖）。
    /// </summary>
    private static void RemoveEventBusServices(IServiceCollection services)
    {
        var descriptors = services
            .Where(s => s.ServiceType == typeof(Leno.Infrastructure.Abstractions.IEventBus)
                     || s.ImplementationType?.FullName?.Contains("RabbitMqEventBus") == true)
            .ToList();
        foreach (var d in descriptors) services.Remove(d);
    }

    /// <summary>
    /// 替换 IDistributedLockProvider 为 Mock，使 MigrateWithLockAsync 跳过迁移（TryAcquireLockAsync 返回 null）。
    /// 测试环境无 Redis，避免 RedisConnectionException 阻止宿主启动。
    /// </summary>
    private static void ReplaceDistributedLockProvider(IServiceCollection services)
    {
        var descriptors = services
            .Where(s => s.ServiceType == typeof(Medallion.Threading.IDistributedLockProvider))
            .ToList();
        foreach (var d in descriptors) services.Remove(d);

        var lockMock = new Mock<Medallion.Threading.IDistributedLock>();
        lockMock
            .Setup(l => l.TryAcquireAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Returns(() => default);

        var lockProviderMock = new Mock<Medallion.Threading.IDistributedLockProvider>();
        lockProviderMock
            .Setup(p => p.CreateLock(It.IsAny<string>()))
            .Returns(lockMock.Object);

        services.AddSingleton(lockProviderMock.Object);
    }

    /// <summary>构造带 X-Internal-Key 头的 POST 请求。</summary>
    private static HttpRequestMessage CreateInternalPostRequest(string path, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Add("X-Internal-Key", TestInternalKey);
        return request;
    }

    #region TrialOffset (POST internal/v1/points/trial-offset)

    [Fact]
    public async Task TrialOffset_WithValidInternalKey_ShouldReturnResult()
    {
        var resultDto = new TrialOffsetResultDto
        {
            OffsetAmount = 1.5m,
            UsedPoints = 150,
            Currency = "CNY"
        };
        _internalAppServiceMock.Setup(s => s.TrialOffsetAsync(UserId, 100m, It.IsAny<CancellationToken>()))
            .ReturnsAsync(resultDto);

        var body = new { userId = UserId, orderAmount = 100m };
        var request = CreateInternalPostRequest("/internal/v1/points/trial-offset", body);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<TrialOffsetResultDto>>();
        result!.Data!.OffsetAmount.Should().Be(1.5m);
        result.Data.UsedPoints.Should().Be(150);
        result.Data.Currency.Should().Be("CNY");
    }

    [Fact]
    public async Task TrialOffset_ShouldCallServiceWithDtoValues()
    {
        var resultDto = new TrialOffsetResultDto
        {
            OffsetAmount = 0.5m,
            UsedPoints = 50,
            Currency = "CNY"
        };
        _internalAppServiceMock.Setup(s => s.TrialOffsetAsync(UserId, 50m, It.IsAny<CancellationToken>()))
            .ReturnsAsync(resultDto);

        var body = new { userId = UserId, orderAmount = 50m };
        var request = CreateInternalPostRequest("/internal/v1/points/trial-offset", body);

        await _client.SendAsync(request);

        _internalAppServiceMock.Verify(
            s => s.TrialOffsetAsync(UserId, 50m, It.IsAny<CancellationToken>()),
            Times.Once,
            "UserId 与 OrderAmount 必须从请求体 DTO 传入");
    }

    [Fact]
    public async Task TrialOffset_WithoutInternalKey_ShouldReturnUnauthorized()
    {
        var body = new { userId = UserId, orderAmount = 100m };
        var request = new HttpRequestMessage(HttpMethod.Post, "/internal/v1/points/trial-offset")
        {
            Content = JsonContent.Create(body)
        };
        // 不带 X-Internal-Key 头

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task TrialOffset_WithWrongInternalKey_ShouldReturnUnauthorized()
    {
        var body = new { userId = UserId, orderAmount = 100m };
        var request = new HttpRequestMessage(HttpMethod.Post, "/internal/v1/points/trial-offset")
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Add("X-Internal-Key", "wrong-key");

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Freeze (POST internal/v1/points/freeze)

    [Fact]
    public async Task Freeze_WithValidInternalKey_ShouldReturnFreezeResult()
    {
        var resultDto = new FreezeResultDto
        {
            Success = true,
            Points = 100,
            OrderId = OrderId,
            AccountId = Guid.NewGuid(),
            AvailableBalanceAfter = 400,
            FrozenBalanceAfter = 100
        };
        _internalAppServiceMock.Setup(s => s.FreezeAsync(UserId, 100, OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(resultDto);

        var body = new { userId = UserId, points = 100, orderId = OrderId };
        var request = CreateInternalPostRequest("/internal/v1/points/freeze", body);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<FreezeResultDto>>();
        result!.Data!.Success.Should().BeTrue();
        result.Data.Points.Should().Be(100);
        result.Data.OrderId.Should().Be(OrderId);
        result.Data.AvailableBalanceAfter.Should().Be(400);
        result.Data.FrozenBalanceAfter.Should().Be(100);
    }

    [Fact]
    public async Task Freeze_ShouldCallServiceWithDtoValues()
    {
        var resultDto = new FreezeResultDto
        {
            Success = true,
            Points = 200,
            OrderId = OrderId,
            AccountId = Guid.NewGuid(),
            AvailableBalanceAfter = 300,
            FrozenBalanceAfter = 200
        };
        _internalAppServiceMock.Setup(s => s.FreezeAsync(UserId, 200, OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(resultDto);

        var body = new { userId = UserId, points = 200, orderId = OrderId };
        var request = CreateInternalPostRequest("/internal/v1/points/freeze", body);

        await _client.SendAsync(request);

        _internalAppServiceMock.Verify(
            s => s.FreezeAsync(UserId, 200, OrderId, It.IsAny<CancellationToken>()),
            Times.Once,
            "UserId、Points、OrderId 必须从请求体 DTO 传入");
    }

    [Fact]
    public async Task Freeze_WithoutInternalKey_ShouldReturnUnauthorized()
    {
        var body = new { userId = UserId, points = 100, orderId = OrderId };
        var request = new HttpRequestMessage(HttpMethod.Post, "/internal/v1/points/freeze")
        {
            Content = JsonContent.Create(body)
        };

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Release (POST internal/v1/points/release)

    [Fact]
    public async Task Release_WithValidInternalKey_ShouldReturnSuccess()
    {
        _internalAppServiceMock.Setup(s => s.ReleaseAsync(OrderId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var body = new { orderId = OrderId };
        var request = CreateInternalPostRequest("/internal/v1/points/release", body);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse>();
        result!.Code.Should().Be(200);
    }

    [Fact]
    public async Task Release_ShouldCallServiceWithOrderId()
    {
        _internalAppServiceMock.Setup(s => s.ReleaseAsync(OrderId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var body = new { orderId = OrderId };
        var request = CreateInternalPostRequest("/internal/v1/points/release", body);

        await _client.SendAsync(request);

        _internalAppServiceMock.Verify(
            s => s.ReleaseAsync(OrderId, It.IsAny<CancellationToken>()),
            Times.Once,
            "OrderId 必须从请求体 DTO 传入");
    }

    [Fact]
    public async Task Release_WithoutInternalKey_ShouldReturnUnauthorized()
    {
        var body = new { orderId = OrderId };
        var request = new HttpRequestMessage(HttpMethod.Post, "/internal/v1/points/release")
        {
            Content = JsonContent.Create(body)
        };

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Confirm (POST internal/v1/points/confirm)

    [Fact]
    public async Task Confirm_WithValidInternalKey_ShouldReturnSuccess()
    {
        _internalAppServiceMock.Setup(s => s.ConfirmAsync(OrderId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var body = new { orderId = OrderId };
        var request = CreateInternalPostRequest("/internal/v1/points/confirm", body);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse>();
        result!.Code.Should().Be(200);
    }

    [Fact]
    public async Task Confirm_ShouldCallServiceWithOrderId()
    {
        _internalAppServiceMock.Setup(s => s.ConfirmAsync(OrderId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var body = new { orderId = OrderId };
        var request = CreateInternalPostRequest("/internal/v1/points/confirm", body);

        await _client.SendAsync(request);

        _internalAppServiceMock.Verify(
            s => s.ConfirmAsync(OrderId, It.IsAny<CancellationToken>()),
            Times.Once,
            "OrderId 必须从请求体 DTO 传入");
    }

    [Fact]
    public async Task Confirm_WithoutInternalKey_ShouldReturnUnauthorized()
    {
        var body = new { orderId = OrderId };
        var request = new HttpRequestMessage(HttpMethod.Post, "/internal/v1/points/confirm")
        {
            Content = JsonContent.Create(body)
        };

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Internal Key 边界

    [Fact]
    public async Task InternalEndpoints_WithWrongInternalKey_ShouldAllReturnUnauthorized()
    {
        // 验证全部 4 个内部端点对错误 key 一致拒绝
        var endpoints = new[]
        {
            "/internal/v1/points/trial-offset",
            "/internal/v1/points/freeze",
            "/internal/v1/points/release",
            "/internal/v1/points/confirm"
        };

        foreach (var endpoint in endpoints)
        {
            var body = new { userId = UserId, points = 100, orderId = OrderId, orderAmount = 100m };
            var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = JsonContent.Create(body)
            };
            request.Headers.Add("X-Internal-Key", "wrong-key-" + Guid.NewGuid());

            var response = await _client.SendAsync(request);

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
                $"端点 {endpoint} 必须拒绝错误的 X-Internal-Key");
        }
    }

    #endregion
}
