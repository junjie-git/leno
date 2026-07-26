using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Leno.Infrastructure.Abstractions;
using Leno.Infrastructure.Auth;
using Leno.Notification.Application;
using Leno.Notification.Application.DTOs;
using Leno.Notification.Domain.ValueObjects;
using Leno.SharedContracts.Responses;
using Medallion.Threading;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Leno.Notification.Api.Tests;

/// <summary>
/// 通知偏好内部端点集成测试（Task D1）。
/// 验证 <see cref="InternalNotificationPreferencesController"/> 2 个 internal/v1/users/{userId}/notification-preferences 端点：
/// - GET 成功返回 ApiResponse
/// - PUT 成功更新
/// - 缺失/错误 X-Internal-Key 头返回 401
/// - 原 api/users/me/notification-preferences 路由已不存在（404）
/// 内部端点鉴权由 InternalApiKeyMiddleware（X-Internal-Key 头）保护，不走 JWT 认证。
/// </summary>
public class NotificationPreferencesApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string TestInternalKey = "test-internal-key-notification-preferences";

    private static readonly Guid UserId = Guid.NewGuid();

    private readonly HttpClient _client;
    private readonly Mock<INotificationPreferenceAppService> _preferenceAppServiceMock = new();
    private readonly Mock<ICurrentUserContext> _currentUserMock = new();

    public NotificationPreferencesApiTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(builder =>
        {
            // 使用 Development 环境：跳过启动期敏感配置校验 + InternalAuth:ApiKey 启动校验
            builder.UseSetting("Environment", "Development");

            builder.ConfigureServices(services =>
            {
                // Mock 待测应用服务 + 当前用户上下文（内部端点不依赖 JWT/CurrentUser，但其他控制器解析时需要）
                services.AddSingleton(_preferenceAppServiceMock.Object);
                services.AddSingleton(_currentUserMock.Object);

                RemoveMassTransitServices(services);
                RemoveElasticsearchServices(services);
                RemoveRedisServices(services);

                // 显式配置 InternalApiKeyOptions：测试用固定密钥
                services.Configure<InternalApiKeyOptions>(o =>
                {
                    o.ApiKey = TestInternalKey;
                    o.RoutePrefix = "internal/";
                });

                // 注册 Test 鉴权方案（复用 NotificationApiTests.cs 中已定义的 TestAuthHandler）
                // 内部端点不依赖 JWT，但仍需 Authorization 头通过 TestAuthHandler，避免其他需鉴权控制器解析时 401 干扰
                services.AddAuthentication(defaultScheme: "Test")
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
            });
        }).CreateClient();

        // 内部端点不走 JWT，但默认带 Authorization 头避免其他中间件干扰
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test");
    }

    /// <summary>
    /// 移除 MassTransit 服务（含 IBus/IBusControl/IPublishEndpoint）与依赖它的 IEventBus，
    /// 注册无操作 IEventBus 占位，避免测试启动连接 RabbitMQ。
    /// </summary>
    private static void RemoveMassTransitServices(IServiceCollection services)
    {
        var descriptors = services
            .Where(s => s.ServiceType.FullName?.Contains("MassTransit") == true
                     || s.ImplementationType?.FullName?.Contains("MassTransit") == true
                     || s.ServiceType == typeof(MassTransit.IBus)
                     || s.ServiceType == typeof(MassTransit.IBusControl)
                     || s.ServiceType == typeof(MassTransit.IPublishEndpoint)
                     || s.ServiceType == typeof(MassTransit.ISendEndpointProvider)
                     || s.ServiceType.FullName?.StartsWith("MassTransit.", StringComparison.Ordinal) == true)
            .ToList();
        foreach (var d in descriptors) services.Remove(d);

        var eventBusDescriptors = services
            .Where(s => s.ServiceType == typeof(IEventBus))
            .ToList();
        foreach (var d in eventBusDescriptors) services.Remove(d);
        services.AddSingleton<IEventBus, NoopEventBus>();

        var consumerDescriptors = services
            .Where(s => s.ImplementationType?.FullName?.Contains("Consumer") == true
                     || s.ImplementationType?.Namespace?.Contains("MassTransit") == true)
            .ToList();
        foreach (var d in consumerDescriptors) services.Remove(d);
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
    /// 移除 Redis 相关服务并替换为无操作实现，避免测试启动时连接 Redis 实例。
    /// 同时将 IDistributedLockProvider 替换为返回 null 的 mock，使 MigrateWithLockAsync 跳过数据库迁移。
    /// 注意：Development 环境启用 ValidateOnBuild，InAppChannel 依赖 IConnectionMultiplexer，
    /// 因此必须保留一个 mock IConnectionMultiplexer，否则 DI 容器构建期校验失败。
    /// </summary>
    private static void RemoveRedisServices(IServiceCollection services)
    {
        // 移除真实 IConnectionMultiplexer 注册，替换为 mock（满足 InAppChannel 构造期依赖）
        var multiplexerDescriptors = services
            .Where(s => s.ServiceType == typeof(StackExchange.Redis.IConnectionMultiplexer))
            .ToList();
        foreach (var d in multiplexerDescriptors) services.Remove(d);
        var redisMock = new Mock<StackExchange.Redis.IConnectionMultiplexer>();
        services.AddSingleton(redisMock.Object);

        var idempotencyDescriptors = services
            .Where(s => s.ServiceType == typeof(IIdempotencyStore))
            .ToList();
        foreach (var d in idempotencyDescriptors) services.Remove(d);
        services.AddSingleton<IIdempotencyStore, NoopIdempotencyStore>();

        var lockProviderDescriptors = services
            .Where(s => s.ServiceType == typeof(IDistributedLockProvider))
            .ToList();
        foreach (var d in lockProviderDescriptors) services.Remove(d);

        var lockMock = new Mock<IDistributedLock>();
        lockMock
            .Setup(l => l.TryAcquireAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Returns(() => default);

        var lockProviderMock = new Mock<IDistributedLockProvider>();
        lockProviderMock
            .Setup(p => p.CreateLock(It.IsAny<string>()))
            .Returns(lockMock.Object);

        services.AddSingleton(lockProviderMock.Object);
    }

    /// <summary>构造带 X-Internal-Key 头的 GET 请求。</summary>
    private static HttpRequestMessage CreateInternalGetRequest(string path)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("X-Internal-Key", TestInternalKey);
        return request;
    }

    /// <summary>构造带 X-Internal-Key 头的 PUT 请求。</summary>
    private static HttpRequestMessage CreateInternalPutRequest(string path, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, path)
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Add("X-Internal-Key", TestInternalKey);
        return request;
    }

    #region GET internal/v1/users/{userId}/notification-preferences

    [Fact]
    public async Task GetPreferences_WithValidInternalKey_ShouldReturnApiResponse()
    {
        // Arrange
        var preferenceDto = new NotificationPreferenceDto
        {
            PreferenceId = Guid.NewGuid(),
            UserId = UserId,
            EventChannels = new Dictionary<string, List<NotificationChannel>>
            {
                ["OrderPaid"] = new() { NotificationChannel.InApp, NotificationChannel.Sms }
            },
            Status = PreferenceStatus.Active
        };
        _preferenceAppServiceMock
            .Setup(s => s.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(preferenceDto);

        // Act
        var request = CreateInternalGetRequest(
            $"/internal/v1/users/{UserId}/notification-preferences");
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<NotificationPreferenceDto>>();
        body!.Should().NotBeNull();
        body!.Code.Should().Be(200);
        body!.Data!.UserId.Should().Be(UserId);
        body!.Data!.PreferenceId.Should().Be(preferenceDto.PreferenceId);
        body!.Data!.Status.Should().Be(PreferenceStatus.Active);
        body!.Data!.EventChannels.Should().ContainKey("OrderPaid");
    }

    [Fact]
    public async Task GetPreferences_ShouldCallServiceWithRouteUserId()
    {
        // Arrange
        var preferenceDto = new NotificationPreferenceDto
        {
            PreferenceId = Guid.NewGuid(),
            UserId = UserId,
            EventChannels = [],
            Status = PreferenceStatus.Active
        };
        _preferenceAppServiceMock
            .Setup(s => s.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(preferenceDto);

        // Act
        var request = CreateInternalGetRequest(
            $"/internal/v1/users/{UserId}/notification-preferences");
        await _client.SendAsync(request);

        // Assert — userId 必须从路由参数传入，而非 ICurrentUserContext
        _preferenceAppServiceMock.Verify(
            s => s.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()),
            Times.Once,
            "userId 必须从路由参数 {userId:guid} 传入应用服务");
    }

    [Fact]
    public async Task GetPreferences_WithoutInternalKey_ShouldReturnUnauthorized()
    {
        // Arrange — 不带 X-Internal-Key 头
        var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/internal/v1/users/{UserId}/notification-preferences");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetPreferences_WithWrongInternalKey_ShouldReturnUnauthorized()
    {
        // Arrange
        var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/internal/v1/users/{UserId}/notification-preferences");
        request.Headers.Add("X-Internal-Key", "wrong-key-" + Guid.NewGuid());

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region PUT internal/v1/users/{userId}/notification-preferences

    [Fact]
    public async Task SetChannelPreference_WithValidInternalKey_ShouldReturnSuccess()
    {
        // Arrange
        _preferenceAppServiceMock
            .Setup(s => s.SetChannelPreferenceAsync(
                UserId,
                It.IsAny<SetChannelPreferenceDto>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var body = new
        {
            eventType = "OrderPaid",
            channels = new[] { (int)NotificationChannel.InApp, (int)NotificationChannel.Sms }
        };
        var request = CreateInternalPutRequest(
            $"/internal/v1/users/{UserId}/notification-preferences",
            body);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse>();
        result!.Should().NotBeNull();
        result!.Code.Should().Be(200);
    }

    [Fact]
    public async Task SetChannelPreference_ShouldCallServiceWithRouteUserIdAndDto()
    {
        // Arrange
        _preferenceAppServiceMock
            .Setup(s => s.SetChannelPreferenceAsync(
                UserId,
                It.IsAny<SetChannelPreferenceDto>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var body = new
        {
            eventType = "OrderShipped",
            channels = new[] { (int)NotificationChannel.Email }
        };
        var request = CreateInternalPutRequest(
            $"/internal/v1/users/{UserId}/notification-preferences",
            body);

        // Act
        await _client.SendAsync(request);

        // Assert — userId 必须从路由参数传入；eventType/channels 必须从请求体 DTO 传入
        _preferenceAppServiceMock.Verify(
            s => s.SetChannelPreferenceAsync(
                UserId,
                It.Is<SetChannelPreferenceDto>(dto =>
                    dto.EventType == "OrderShipped"
                    && dto.Channels.Count == 1
                    && dto.Channels.Contains(NotificationChannel.Email)),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "userId 必须从路由参数传入，eventType/channels 必须从请求体 DTO 传入");
    }

    [Fact]
    public async Task SetChannelPreference_WithoutInternalKey_ShouldReturnUnauthorized()
    {
        // Arrange — 不带 X-Internal-Key 头
        var body = new
        {
            eventType = "OrderPaid",
            channels = new[] { (int)NotificationChannel.InApp }
        };
        var request = new HttpRequestMessage(
            HttpMethod.Put,
            $"/internal/v1/users/{UserId}/notification-preferences")
        {
            Content = JsonContent.Create(body)
        };

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SetChannelPreference_WithWrongInternalKey_ShouldReturnUnauthorized()
    {
        // Arrange
        var body = new
        {
            eventType = "OrderPaid",
            channels = new[] { (int)NotificationChannel.InApp }
        };
        var request = new HttpRequestMessage(
            HttpMethod.Put,
            $"/internal/v1/users/{UserId}/notification-preferences")
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Add("X-Internal-Key", "wrong-key-" + Guid.NewGuid());

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region 旧对外路由已下线

    [Fact]
    public async Task LegacyPublicRoute_GetApiUsersMePreferences_ShouldReturn404()
    {
        // Arrange — 旧对外 HTTP 路由已迁移至 UserCenter 域，本域不再提供
        var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/users/me/notification-preferences");
        request.Headers.Add("X-Internal-Key", TestInternalKey);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "对外 HTTP 端点已归 UserCenter 域，Notification 域不再提供 api/users/me/notification-preferences 路由");
    }

    [Fact]
    public async Task LegacyPublicRoute_PutApiUsersMePreferences_ShouldReturn404()
    {
        // Arrange
        var body = new
        {
            eventType = "OrderPaid",
            channels = new[] { (int)NotificationChannel.InApp }
        };
        var request = new HttpRequestMessage(
            HttpMethod.Put,
            "/api/users/me/notification-preferences")
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Add("X-Internal-Key", TestInternalKey);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "对外 HTTP 端点已归 UserCenter 域，Notification 域不再提供 api/users/me/notification-preferences 路由");
    }

    #endregion
}

/// <summary>
/// 无操作幂等去重存储，用于测试环境替换 Redis 实现。
/// 所有方法返回默认值（未处理 / 标记成功），不实际持久化任何状态。
/// </summary>
internal sealed class NoopIdempotencyStore : IIdempotencyStore
{
    public bool SupportsAtomicProcessing => false;

    public Task<bool> IsProcessedAsync(Guid eventId, CancellationToken ct = default)
        => Task.FromResult(false);

    public Task MarkAsProcessedAsync(Guid eventId, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task<bool> TryMarkAsProcessingAsync(Guid eventId, CancellationToken ct = default)
        => Task.FromResult(true);

    public Task ReleaseProcessingLockAsync(Guid eventId, CancellationToken ct = default)
        => Task.CompletedTask;
}

/// <summary>
/// 无操作事件总线，用于测试环境替换基于 MassTransit 的 RabbitMqEventBus 实现。
/// 所有发布操作直接返回已完成任务，不实际投递消息到消息队列。
/// </summary>
internal sealed class NoopEventBus : IEventBus
{
    public Task PublishAsync<T>(T integrationEvent, CancellationToken ct = default) where T : notnull
        => Task.CompletedTask;

    public Task PublishAsync<T>(T integrationEvent, IReadOnlyDictionary<string, string?>? headers, CancellationToken ct = default) where T : notnull
        => Task.CompletedTask;
}
