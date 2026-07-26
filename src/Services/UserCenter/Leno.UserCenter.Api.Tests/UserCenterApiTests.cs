using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Leno.Infrastructure.Abstractions;
using Leno.Infrastructure.Auth;
using Leno.SharedContracts.Responses;
using Leno.UserCenter.Application;
using Leno.UserCenter.Application.DTOs;
using Leno.UserCenter.Application.Exceptions;
using Leno.UserCenter.Domain.Exceptions;
using Leno.UserCenter.Domain.ValueObjects;
using Medallion.Threading;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Leno.UserCenter.Api.Tests;

/// <summary>
/// 用户中心域 API 集成测试（UserCenter BC 独立维护）。
/// 覆盖 4 个 Controller 共 17 个端点的成功场景、鉴权场景（401/403）与失败场景（400/404）。
/// 通过 mock 4 个 AppService 与 ICurrentUserContext 解耦业务逻辑，聚焦 Controller 路由/鉴权/响应包装。
/// </summary>
public class UserCenterApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private HttpClient _client;
    private readonly Mock<IAddressAppService> _addressAppServiceMock = new();
    private readonly Mock<IFavoritesAppService> _favoritesAppServiceMock = new();
    private readonly Mock<IBrowseHistoryAppService> _browseHistoryAppServiceMock = new();
    private readonly Mock<INotificationPreferencesAppService> _notificationPreferencesAppServiceMock = new();
    private readonly Mock<ICurrentUserContext> _currentUserMock = new();
    private readonly WebApplicationFactory<Program> _factory;

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid AddressId = Guid.NewGuid();
    private static readonly Guid SpuId = Guid.NewGuid();
    private static readonly Guid SkuId = Guid.NewGuid();
    private static readonly Guid FavoriteId = Guid.NewGuid();
    private static readonly Guid HistoryId = Guid.NewGuid();

    public UserCenterApiTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = CreateClient(role: "Buyer,Seller,Operator,Admin");
    }

    /// <summary>
    /// 创建测试 HttpClient，通过 X-Test-Role 头指定当前用户角色。
    /// 默认赋予全部 4 个角色，覆盖所有鉴权端点的成功场景。
    /// 使用 Development 环境以绕过生产级敏感配置与 InternalAuth:ApiKey 启动校验（测试通过 mock 解耦业务依赖）。
    /// </summary>
    private HttpClient CreateClient(string role)
    {
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Environment", "Development");

            builder.ConfigureServices(services =>
            {
                // 用 mock 替换 4 个 AppService 与当前用户上下文，避免触发真实仓储 / 远程调用
                services.AddSingleton(_addressAppServiceMock.Object);
                services.AddSingleton(_favoritesAppServiceMock.Object);
                services.AddSingleton(_browseHistoryAppServiceMock.Object);
                services.AddSingleton(_notificationPreferencesAppServiceMock.Object);
                services.AddSingleton(_currentUserMock.Object);

                RemoveMassTransitServices(services);
                RemoveElasticsearchServices(services);
                RemoveRedisServices(services);

                services.AddAuthentication(defaultScheme: "Test")
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
            });
        }).CreateClient();

        _client = client;
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test");
        _client.DefaultRequestHeaders.Add("X-Test-Role", role);

        // 默认 currentUserMock 设置为已认证（成功场景）
        _currentUserMock.SetupGet(c => c.IsAuthenticated).Returns(true);
        _currentUserMock.SetupGet(c => c.UserId).Returns(UserId);

        return _client;
    }

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

        // 移除依赖 MassTransit 的 IEventBus（RabbitMqEventBus 需要 IPublishEndpoint）
        var eventBusDescriptors = services
            .Where(s => s.ServiceType == typeof(Leno.Infrastructure.Abstractions.IEventBus))
            .ToList();
        foreach (var d in eventBusDescriptors) services.Remove(d);

        // 注册无操作 IEventBus 占位，避免解析失败
        services.AddSingleton<Leno.Infrastructure.Abstractions.IEventBus, NoopEventBus>();

        // 移除可能残留的 MassTransit 消费者
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

        // 移除依赖 ElasticsearchClient 的 IEsReadModelRepository<> 开放泛型注册
        var esRepoDescriptors = services
            .Where(s => s.ServiceType.IsGenericType
                     && s.ServiceType.GetGenericTypeDefinition().FullName?.Contains("IEsReadModelRepository") == true)
            .ToList();
        foreach (var d in esRepoDescriptors) services.Remove(d);

        // 移除依赖 ElasticsearchClient 的 HostedService（IndexInitializer）
        var hostedServiceDescriptors = services
            .Where(s => s.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService)
                     && s.ImplementationType?.FullName?.Contains("IndexInitializer") == true)
            .ToList();
        foreach (var d in hostedServiceDescriptors) services.Remove(d);
    }

    /// <summary>
    /// 移除 Redis 相关服务并替换为无操作实现，避免测试启动时连接 Redis 实例。
    /// 同时将 IDistributedLockProvider 替换为返回 null 的 mock，使 MigrateWithLockAsync 跳过数据库迁移。
    /// </summary>
    private static void RemoveRedisServices(IServiceCollection services)
    {
        // 移除 IConnectionMultiplexer（StackExchange.Redis 连接复用器）
        var multiplexerDescriptors = services
            .Where(s => s.ServiceType == typeof(StackExchange.Redis.IConnectionMultiplexer))
            .ToList();
        foreach (var d in multiplexerDescriptors) services.Remove(d);

        // 移除 IIdempotencyStore（Redis 幂等去重存储）
        var idempotencyDescriptors = services
            .Where(s => s.ServiceType == typeof(IIdempotencyStore))
            .ToList();
        foreach (var d in idempotencyDescriptors) services.Remove(d);
        // 注册无操作 IIdempotencyStore 占位，避免其他服务解析失败
        services.AddSingleton<IIdempotencyStore, NoopIdempotencyStore>();

        // 移除 IDistributedLockProvider（基于 Redis 的分布式锁提供者）
        var lockProviderDescriptors = services
            .Where(s => s.ServiceType == typeof(IDistributedLockProvider))
            .ToList();
        foreach (var d in lockProviderDescriptors) services.Remove(d);

        // 注册返回 null 的 mock：CreateLock → TryAcquireAsync 返回 null → MigrateWithLockAsync 跳过迁移
        var lockMock = new Mock<Medallion.Threading.IDistributedLock>();
        lockMock
            .Setup(l => l.TryAcquireAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Returns(() => default);

        var lockProviderMock = new Mock<IDistributedLockProvider>();
        lockProviderMock
            .Setup(p => p.CreateLock(It.IsAny<string>()))
            .Returns(lockMock.Object);

        services.AddSingleton(lockProviderMock.Object);
    }

    /// <summary>切换当前用户角色，重新创建 HttpClient。</summary>
    private void SwitchRole(string role)
    {
        _client = CreateClient(role);
    }

    /// <summary>切换为未认证用户（无 Authorization 头）。</summary>
    private void SwitchToUnauthenticated()
    {
        _client = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Environment", "Development");
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(_addressAppServiceMock.Object);
                services.AddSingleton(_favoritesAppServiceMock.Object);
                services.AddSingleton(_browseHistoryAppServiceMock.Object);
                services.AddSingleton(_notificationPreferencesAppServiceMock.Object);
                services.AddSingleton(_currentUserMock.Object);
                RemoveMassTransitServices(services);
                RemoveElasticsearchServices(services);
                RemoveRedisServices(services);
                services.AddAuthentication(defaultScheme: "Test")
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
            });
        }).CreateClient();
        // 不设置 Authorization 头，模拟未认证
    }

    [Fact]
    public async Task HealthLive_ShouldReturnOk()
    {
        var response = await _client.GetAsync("/health/live");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #region AddressesController（5端点，[Authorize]，路由 api/users/me/addresses）

    [Fact]
    public async Task ListAddresses_AsAuthenticated_ShouldReturn200()
    {
        _addressAppServiceMock.Setup(s => s.ListAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AddressDto> { CreateAddressDto() });

        var response = await _client.GetAsync("/api/users/me/addresses");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<IReadOnlyList<AddressDto>>>();
        body!.Data.Should().HaveCount(1);
        body.Data![0].Id.Should().Be(AddressId);
    }

    [Fact]
    public async Task CreateAddress_AsAuthenticated_ShouldReturn200()
    {
        _addressAppServiceMock.Setup(s => s.CreateAsync(UserId, It.IsAny<SaveAddressDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateAddressDto());

        var dto = new
        {
            RecipientName = "张三",
            RecipientPhone = "+8613800138000",
            Province = "广东省",
            City = "深圳市",
            District = "南山区",
            Detail = "科技园南区T3栋501室",
            Tag = "公司",
            IsDefault = true
        };
        var response = await _client.PostAsJsonAsync("/api/users/me/addresses", dto);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<AddressDto>>();
        body!.Data!.Id.Should().Be(AddressId);
        _addressAppServiceMock.Verify(
            s => s.CreateAsync(UserId, It.IsAny<SaveAddressDto>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateAddress_AsAuthenticated_ShouldReturn200()
    {
        _addressAppServiceMock.Setup(s => s.UpdateAsync(UserId, AddressId, It.IsAny<SaveAddressDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateAddressDto());

        var dto = new
        {
            RecipientName = "李四",
            RecipientPhone = "+8613900139000",
            Province = "北京市",
            City = "北京市",
            District = "海淀区",
            Detail = "中关村大街1号院2号楼3单元401室",
            Tag = "家",
            IsDefault = false
        };
        var response = await _client.PutAsJsonAsync($"/api/users/me/addresses/{AddressId}", dto);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<AddressDto>>();
        body!.Data!.Id.Should().Be(AddressId);
        _addressAppServiceMock.Verify(
            s => s.UpdateAsync(UserId, AddressId, It.IsAny<SaveAddressDto>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DeleteAddress_AsAuthenticated_ShouldReturn200()
    {
        _addressAppServiceMock.Setup(s => s.DeleteAsync(UserId, AddressId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await _client.DeleteAsync($"/api/users/me/addresses/{AddressId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse>();
        body!.Code.Should().Be(200);
        _addressAppServiceMock.Verify(
            s => s.DeleteAsync(UserId, AddressId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SetDefaultAddress_AsAuthenticated_ShouldReturn200()
    {
        _addressAppServiceMock.Setup(s => s.SetDefaultAsync(UserId, AddressId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateAddressDto());

        var response = await _client.PostAsync($"/api/users/me/addresses/{AddressId}/default", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<AddressDto>>();
        body!.Data!.Id.Should().Be(AddressId);
        _addressAppServiceMock.Verify(
            s => s.SetDefaultAsync(UserId, AddressId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region FavoritesController（5端点，[Authorize(Roles = "Buyer")]，路由 api/users/me/favorites）

    [Fact]
    public async Task ListFavorites_AsBuyer_ShouldReturn200()
    {
        var paged = PagedResult.Create(new List<FavoriteDto> { CreateFavoriteDto() }, total: 1, page: 1, pageSize: 20);
        _favoritesAppServiceMock.Setup(s => s.ListAsync(UserId, It.IsAny<FavoriteQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(paged);

        var response = await _client.GetAsync("/api/users/me/favorites?page=1&pageSize=20&sort=created&order=desc");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<FavoriteDto>>>();
        body!.Data!.Total.Should().Be(1);
        body.Data.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task AddFavorite_AsBuyer_ShouldReturn200()
    {
        _favoritesAppServiceMock.Setup(s => s.AddAsync(UserId, It.IsAny<AddFavoriteDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateFavoriteDto());

        var dto = new { SpuId = SpuId };
        var response = await _client.PostAsJsonAsync("/api/users/me/favorites", dto);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<FavoriteDto>>();
        body!.Data!.FavoriteId.Should().Be(FavoriteId);
        _favoritesAppServiceMock.Verify(
            s => s.AddAsync(UserId, It.IsAny<AddFavoriteDto>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RemoveFavorite_AsBuyer_ShouldReturn200()
    {
        _favoritesAppServiceMock.Setup(s => s.RemoveAsync(UserId, SpuId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await _client.DeleteAsync($"/api/users/me/favorites/{SpuId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse>();
        body!.Code.Should().Be(200);
        _favoritesAppServiceMock.Verify(
            s => s.RemoveAsync(UserId, SpuId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task BatchDeleteFavorites_AsBuyer_ShouldReturn200()
    {
        _favoritesAppServiceMock.Setup(s => s.BatchDeleteAsync(UserId, It.IsAny<BatchDeleteFavoritesDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        var dto = new { SpuIds = new List<Guid> { SpuId, Guid.NewGuid() } };
        var response = await _client.PostAsJsonAsync("/api/users/me/favorites/batch-delete", dto);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<int>>();
        body!.Data.Should().Be(2);
        _favoritesAppServiceMock.Verify(
            s => s.BatchDeleteAsync(UserId, It.IsAny<BatchDeleteFavoritesDto>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CountFavorites_AsBuyer_ShouldReturn200()
    {
        _favoritesAppServiceMock.Setup(s => s.CountAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FavoriteCountDto { Count = 5 });

        var response = await _client.GetAsync("/api/users/me/favorites/count");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<FavoriteCountDto>>();
        body!.Data!.Count.Should().Be(5);
    }

    #endregion

    #region BrowseHistoryController（5端点，[Authorize(Roles = "Buyer")]，路由 api/users/me/browse-history）

    [Fact]
    public async Task ListBrowseHistory_AsBuyer_ShouldReturn200()
    {
        var paged = PagedResult.Create(new List<BrowseHistoryDto> { CreateBrowseHistoryDto() }, total: 1, page: 1, pageSize: 20);
        _browseHistoryAppServiceMock.Setup(s => s.ListAsync(UserId, It.IsAny<BrowseHistoryQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(paged);

        var response = await _client.GetAsync("/api/users/me/browse-history?page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<BrowseHistoryDto>>>();
        body!.Data!.Total.Should().Be(1);
        body.Data.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task AddBrowseHistory_AsBuyer_ShouldReturn200()
    {
        _browseHistoryAppServiceMock.Setup(s => s.AddAsync(UserId, It.IsAny<AddBrowseHistoryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateBrowseHistoryDto());

        var dto = new { SpuId = SpuId, SkuId = (Guid?)SkuId };
        var response = await _client.PostAsJsonAsync("/api/users/me/browse-history", dto);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<BrowseHistoryDto>>();
        body!.Data!.HistoryId.Should().Be(HistoryId);
        _browseHistoryAppServiceMock.Verify(
            s => s.AddAsync(UserId, It.IsAny<AddBrowseHistoryDto>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RemoveBrowseHistory_AsBuyer_ShouldReturn200()
    {
        _browseHistoryAppServiceMock.Setup(s => s.RemoveAsync(UserId, HistoryId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await _client.DeleteAsync($"/api/users/me/browse-history/{HistoryId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse>();
        body!.Code.Should().Be(200);
        _browseHistoryAppServiceMock.Verify(
            s => s.RemoveAsync(UserId, HistoryId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task BatchDeleteBrowseHistory_AsBuyer_ShouldReturn200()
    {
        _browseHistoryAppServiceMock.Setup(s => s.BatchDeleteAsync(UserId, It.IsAny<BatchDeleteBrowseHistoryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        var dto = new { Ids = new List<Guid> { HistoryId, Guid.NewGuid(), Guid.NewGuid() } };
        var response = await _client.PostAsJsonAsync("/api/users/me/browse-history/batch-delete", dto);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<int>>();
        body!.Data.Should().Be(3);
        _browseHistoryAppServiceMock.Verify(
            s => s.BatchDeleteAsync(UserId, It.IsAny<BatchDeleteBrowseHistoryDto>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ClearAllBrowseHistory_AsBuyer_ShouldReturn200()
    {
        _browseHistoryAppServiceMock.Setup(s => s.ClearAllAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(10);

        var response = await _client.DeleteAsync("/api/users/me/browse-history");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<int>>();
        body!.Data.Should().Be(10);
        _browseHistoryAppServiceMock.Verify(
            s => s.ClearAllAsync(UserId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region NotificationPreferencesController（2端点，[Authorize(Roles = "Buyer")]，路由 api/users/me/notification-preferences）

    [Fact]
    public async Task GetNotificationPreferences_AsBuyer_ShouldReturn200()
    {
        _notificationPreferencesAppServiceMock.Setup(s => s.GetAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateNotificationPreferencesDto());

        var response = await _client.GetAsync("/api/users/me/notification-preferences");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<NotificationPreferencesDto>>();
        body!.Data!.UserId.Should().Be(UserId);
        body.Data.Preferences.Should().HaveCount(1);
    }

    [Fact]
    public async Task UpdateNotificationPreferences_AsBuyer_ShouldReturn200()
    {
        _notificationPreferencesAppServiceMock.Setup(s => s.UpdateAsync(UserId, It.IsAny<UpdateNotificationPreferencesRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateNotificationPreferencesDto());

        var dto = new
        {
            EventType = NotificationEventType.OrderStatus,
            Channel = NotificationChannel.Sms,
            Enabled = true,
            DndEnabled = true,
            DndStart = "22:00",
            DndEnd = "07:00"
        };
        var response = await _client.PutAsJsonAsync("/api/users/me/notification-preferences", dto);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<NotificationPreferencesDto>>();
        body!.Data!.UserId.Should().Be(UserId);
        _notificationPreferencesAppServiceMock.Verify(
            s => s.UpdateAsync(UserId, It.IsAny<UpdateNotificationPreferencesRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region 鉴权场景（401/403）

    [Fact]
    public async Task UnauthorizedRequest_ShouldReturn401()
    {
        SwitchToUnauthenticated();
        var response = await _client.GetAsync("/api/users/me/addresses");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SellerAccessFavoritesEndpoint_ShouldReturn403()
    {
        // FavoritesController 标注 [Authorize(Roles = "Buyer")]，Seller 角色无权访问
        SwitchRole("Seller");
        var response = await _client.GetAsync("/api/users/me/favorites");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SellerAccessBrowseHistoryEndpoint_ShouldReturn403()
    {
        // BrowseHistoryController 标注 [Authorize(Roles = "Buyer")]，Seller 角色无权访问
        SwitchRole("Seller");
        var response = await _client.GetAsync("/api/users/me/browse-history");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SellerAccessNotificationPreferencesEndpoint_ShouldReturn403()
    {
        // NotificationPreferencesController 标注 [Authorize(Roles = "Buyer")]，Seller 角色无权访问
        SwitchRole("Seller");
        var response = await _client.GetAsync("/api/users/me/notification-preferences");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    #endregion

    #region 失败场景（400/404）

    [Fact]
    public async Task CreateAddress_WithEmptyBody_ShouldReturn400()
    {
        // [ApiController] 自动模型绑定校验：[FromBody] 收到空 JSON 体时返回 400
        using var content = new StringContent(string.Empty, Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/api/users/me/addresses", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateAddress_WithEmptyBody_ShouldReturn400()
    {
        // [ApiController] 自动模型绑定校验：[FromBody] 收到空 JSON 体时返回 400
        using var content = new StringContent(string.Empty, Encoding.UTF8, "application/json");
        var response = await _client.PutAsync($"/api/users/me/addresses/{AddressId}", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateAddress_WhenAppServiceThrowsValidationException_ShouldReturn400()
    {
        // 应用层校验失败（如手机号非 E.164 格式）抛 UserCenterValidationException
        // 全局异常中间件按 ErrorCode 默认映射为 400
        _addressAppServiceMock.Setup(s => s.CreateAsync(UserId, It.IsAny<SaveAddressDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UserCenterValidationException("收件人手机号须为 E.164 格式"));

        var dto = new
        {
            RecipientName = "张三",
            RecipientPhone = "13800138000", // 缺少 + 前缀，违反 E.164
            Province = "广东省",
            City = "深圳市",
            District = "南山区",
            Detail = "科技园南区T3栋501室",
            Tag = "公司",
            IsDefault = false
        };
        var response = await _client.PostAsJsonAsync("/api/users/me/addresses", dto);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse>();
        body!.Code.Should().Be(400);
    }

    [Fact]
    public async Task UpdateAddress_WhenNotFound_ShouldReturn404()
    {
        // 应用层找不到地址时抛 UserCenterDomainException，ErrorCode 后缀 _NOT_FOUND 触发 404 映射
        _addressAppServiceMock.Setup(s => s.UpdateAsync(UserId, AddressId, It.IsAny<SaveAddressDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UserCenterDomainException("地址不存在", "ADDRESS_NOT_FOUND"));

        var dto = new
        {
            RecipientName = "李四",
            RecipientPhone = "+8613900139000",
            Province = "北京市",
            City = "北京市",
            District = "海淀区",
            Detail = "中关村大街1号院2号楼3单元401室",
            Tag = "家",
            IsDefault = false
        };
        var response = await _client.PutAsJsonAsync($"/api/users/me/addresses/{AddressId}", dto);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse>();
        body!.Code.Should().Be(404);
    }

    [Fact]
    public async Task AddFavorite_WithEmptyBody_ShouldReturn400()
    {
        // [ApiController] 自动模型绑定校验：[FromBody] 收到空 JSON 体时返回 400
        using var content = new StringContent(string.Empty, Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/api/users/me/favorites", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AddBrowseHistory_WithEmptyBody_ShouldReturn400()
    {
        // [ApiController] 自动模型绑定校验：[FromBody] 收到空 JSON 体时返回 400
        using var content = new StringContent(string.Empty, Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/api/users/me/browse-history", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateNotificationPreferences_WithEmptyBody_ShouldReturn400()
    {
        // [ApiController] 自动模型绑定校验：[FromBody] 收到空 JSON 体时返回 400
        using var content = new StringContent(string.Empty, Encoding.UTF8, "application/json");
        var response = await _client.PutAsync("/api/users/me/notification-preferences", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    private static AddressDto CreateAddressDto()
    {
        return new AddressDto
        {
            Id = AddressId,
            UserId = UserId,
            RecipientName = "张三",
            RecipientPhone = "+8613800138000",
            Province = "广东省",
            City = "深圳市",
            District = "南山区",
            Detail = "科技园南区T3栋501室",
            Tag = "公司",
            IsDefault = true,
            Status = AddressStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private static FavoriteDto CreateFavoriteDto()
    {
        return new FavoriteDto
        {
            FavoriteId = FavoriteId,
            SpuId = SpuId,
            SpuTitle = "测试商品",
            MainImageUrl = "https://cdn.example.com/images/test.jpg",
            Price = 199.00m,
            OriginalPrice = 299.00m,
            ShopId = Guid.NewGuid(),
            ShopName = "测试店铺",
            SalesCount = 1000,
            StockStatus = "有货",
            FavoritedAt = DateTime.UtcNow
        };
    }

    private static BrowseHistoryDto CreateBrowseHistoryDto()
    {
        return new BrowseHistoryDto
        {
            HistoryId = HistoryId,
            SpuId = SpuId,
            SkuId = SkuId,
            SpuTitle = "测试商品",
            MainImageUrl = "https://cdn.example.com/images/test.jpg",
            Price = 199.00m,
            ShopId = Guid.NewGuid(),
            ShopName = "测试店铺",
            ViewedAt = DateTime.UtcNow
        };
    }

    private static NotificationPreferencesDto CreateNotificationPreferencesDto()
    {
        return new NotificationPreferencesDto
        {
            UserId = UserId,
            Preferences = new List<NotificationPreferenceItemDto>
            {
                new()
                {
                    EventType = NotificationEventType.OrderStatus,
                    Group = "订单通知",
                    DisplayName = "订单状态变更",
                    Channels = new NotificationChannelsDto
                    {
                        InApp = true,
                        Sms = true,
                        Email = false
                    }
                }
            },
            DndEnabled = true,
            DndStart = "22:00",
            DndEnd = "07:00",
            UpdatedAt = DateTime.UtcNow
        };
    }
}

/// <summary>
/// 测试鉴权处理器，通过 X-Test-Role 头指定当前用户角色。
/// 默认（无 X-Test-Role 头）赋予全部 4 个角色，覆盖所有鉴权端点的成功场景。
/// </summary>
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

        var roleHeader = Request.Headers["X-Test-Role"].FirstOrDefault() ?? "Buyer,Seller,Operator,Admin";
        var roles = roleHeader.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var claims = new List<Claim> { new(ClaimTypes.Name, "test") };
        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "Test");

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
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
internal sealed class NoopEventBus : Leno.Infrastructure.Abstractions.IEventBus
{
    public Task PublishAsync<T>(T integrationEvent, CancellationToken ct = default) where T : notnull
        => Task.CompletedTask;

    public Task PublishAsync<T>(T integrationEvent, IReadOnlyDictionary<string, string?>? headers, CancellationToken ct = default) where T : notnull
        => Task.CompletedTask;
}
