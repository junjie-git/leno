using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Leno.Infrastructure.Auth;
using Leno.Membership.Application;
using Leno.Membership.Application.DTOs;
using Leno.Membership.Domain.Exceptions;
using Leno.Membership.Domain.ValueObjects;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Leno.Membership.Api.Tests;

/// <summary>
/// 会员域 API 集成测试。
/// 覆盖 MembershipPackagesController（买家列表 + 订阅 2 端点）、AdminMembershipPackagesController（运营 CRUD + 启停 4 端点）、
/// MembersController（买家 /me 1 端点）、AdminMemberLevelsController（运营 levels CRUD + 启停 5 端点）共 12 端点。
/// 使用 WebApplicationFactory + Mock 应用服务，验证路由、RBAC 鉴权、ApiResponse 包装、UserId 从 JWT 注入。
/// </summary>
public class MembershipApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly Mock<IMembershipPackageAppService> _packageAppServiceMock = new();
    private readonly Mock<IMemberAppService> _memberAppServiceMock = new();
    private readonly Mock<ICurrentUserContext> _currentUserMock = new();

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid AnotherUserId = Guid.NewGuid();
    private static readonly Guid PackageId = Guid.NewGuid();
    private static readonly Guid LevelId = Guid.NewGuid();

    public MembershipApiTests(WebApplicationFactory<Program> factory)
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

                services.AddSingleton(_packageAppServiceMock.Object);
                services.AddSingleton(_memberAppServiceMock.Object);
                services.AddSingleton(_currentUserMock.Object);

                services.Configure<InternalApiKeyOptions>(o =>
                {
                    o.ApiKey = "test-internal-key-membership";
                    o.RoutePrefix = "internal/";
                });

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

    /// <summary>
    /// 移除 Program.cs 注册的真实应用服务（Scoped），避免 Development 环境的 ValidateOnBuild
    /// 校验因仓储未注册而失败。移除后由测试注入 Mock 单例替代。
    /// </summary>
    private static void RemoveApplicationServiceRegistrations(IServiceCollection services)
    {
        var appServiceInterfaces = new[]
        {
            typeof(IMembershipPackageAppService),
            typeof(IMemberAppService)
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

    #region MembershipPackagesController - List (GET /api/membership-packages)

    [Fact]
    public async Task ListPackages_WithBuyerAuth_ReturnsOk()
    {
        SetupBuyerAuth();
        var packages = new List<MembershipPackageDto>
        {
            new()
            {
                Id = PackageId,
                Name = "月度会员",
                Level = 1,
                Price = 30m,
                DurationDays = 30,
                Benefits = "{\"discount\":0.9}",
                Status = PackageStatus.Enabled
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "年度会员",
                Level = 2,
                Price = 299m,
                DurationDays = 365,
                Benefits = "{\"discount\":0.8}",
                Status = PackageStatus.Enabled
            }
        };
        _packageAppServiceMock.Setup(s => s.GetPackagesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(packages);

        var response = await _client.GetAsync("/api/membership-packages");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<MembershipPackageDto>>>();
        result!.Code.Should().Be(200);
        result.Data.Should().HaveCount(2);
        result.Data![0].Name.Should().Be("月度会员");
        result.Data[1].Name.Should().Be("年度会员");
    }

    [Fact]
    public async Task ListPackages_WithoutAuth_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.GetAsync("/api/membership-packages");
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region MembershipPackagesController - Subscribe (POST /api/membership-packages/{id}/subscribe)

    [Fact]
    public async Task Subscribe_WithBuyerAuth_ReturnsOk()
    {
        SetupBuyerAuth();
        var resultDto = new SubscriptionResultDto
        {
            SubscriptionId = Guid.NewGuid(),
            UserId = UserId,
            PackageId = PackageId,
            PackageName = "月度会员",
            Level = 1,
            Price = 30m,
            DurationDays = 30,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };
        _packageAppServiceMock.Setup(s => s.SubscribeAsync(UserId, PackageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(resultDto);

        var response = await _client.PostAsync($"/api/membership-packages/{PackageId}/subscribe", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<SubscriptionResultDto>>();
        result!.Code.Should().Be(200);
        result.Data!.UserId.Should().Be(UserId, "UserId 必须从 JWT 注入");
        result.Data.PackageId.Should().Be(PackageId);
        result.Data.Status.Should().Be("Pending");
    }

    [Fact]
    public async Task Subscribe_ShouldCallServiceWithJwtUserIdAndRoutePackageId()
    {
        SetupBuyerAuth();
        var resultDto = new SubscriptionResultDto
        {
            SubscriptionId = Guid.NewGuid(),
            UserId = UserId,
            PackageId = PackageId,
            PackageName = "月度会员",
            Level = 1,
            Price = 30m,
            DurationDays = 30,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };
        _packageAppServiceMock.Setup(s => s.SubscribeAsync(UserId, PackageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(resultDto);

        await _client.PostAsync($"/api/membership-packages/{PackageId}/subscribe", null);

        _packageAppServiceMock.Verify(
            s => s.SubscribeAsync(UserId, PackageId, It.IsAny<CancellationToken>()),
            Times.Once,
            "UserId 从 JWT 注入，PackageId 从路由参数取");
    }

    [Fact]
    public async Task Subscribe_WithoutAuth_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.PostAsync($"/api/membership-packages/{PackageId}/subscribe", null);
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region AdminMembershipPackagesController - CreatePackage (POST /api/admin/membership-packages)

    [Fact]
    public async Task CreatePackage_WithOperatorAuth_ReturnsOk()
    {
        SetupOperatorAuth();
        var dto = new MembershipPackageDto
        {
            Id = PackageId,
            Name = "月度会员",
            Level = 1,
            Price = 30m,
            DurationDays = 30,
            Benefits = "{\"discount\":0.9}",
            Status = PackageStatus.Enabled
        };
        _packageAppServiceMock.Setup(s => s.CreatePackageAsync(It.IsAny<CreateMembershipPackageDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var body = new
        {
            name = "月度会员",
            level = 1,
            price = 30m,
            durationDays = 30,
            benefits = "{\"discount\":0.9}"
        };
        var response = await _client.PostAsJsonAsync("/api/admin/membership-packages", body);

        response.StatusCode.Should().Be(HttpStatusCode.OK, "返工后不用 201 CreatedAtAction");
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<MembershipPackageDto>>();
        result!.Code.Should().Be(200);
        result.Data!.Name.Should().Be("月度会员");
        result.Data.Price.Should().Be(30m);
    }

    [Fact]
    public async Task CreatePackage_WithBuyerAuth_Returns403()
    {
        SetupBuyerAuth();
        _packageAppServiceMock.Setup(s => s.CreatePackageAsync(It.IsAny<CreateMembershipPackageDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MembershipPackageDto { Id = PackageId, Name = "x", Level = 1, Price = 1m, DurationDays = 1, Benefits = "{}", Status = PackageStatus.Enabled });

        var body = new
        {
            name = "月度会员",
            level = 1,
            price = 30m,
            durationDays = 30,
            benefits = "{\"discount\":0.9}"
        };
        var response = await _client.PostAsJsonAsync("/api/admin/membership-packages", body);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden, "Buyer 角色不可访问运营端 api/admin/*");
        _packageAppServiceMock.Verify(
            s => s.CreatePackageAsync(It.IsAny<CreateMembershipPackageDto>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "403 时不应调用应用服务");
    }

    [Fact]
    public async Task CreatePackage_WithoutAuth_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var body = new
        {
            name = "月度会员",
            level = 1,
            price = 30m,
            durationDays = 30,
            benefits = "{\"discount\":0.9}"
        };
        var response = await _client.PostAsJsonAsync("/api/admin/membership-packages", body);
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region AdminMembershipPackagesController - UpdatePackage (PUT /api/admin/membership-packages/{id})

    [Fact]
    public async Task UpdatePackage_WithOperatorAuth_ReturnsOk()
    {
        SetupOperatorAuth();
        var dto = new MembershipPackageDto
        {
            Id = PackageId,
            Name = "月度会员（更新）",
            Level = 1,
            Price = 39m,
            DurationDays = 30,
            Benefits = "{\"discount\":0.85}",
            Status = PackageStatus.Enabled
        };
        _packageAppServiceMock.Setup(s => s.UpdatePackageAsync(PackageId, It.IsAny<UpdateMembershipPackageDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var body = new
        {
            name = "月度会员（更新）",
            price = 39m,
            durationDays = 30,
            benefits = "{\"discount\":0.85}"
        };
        var response = await _client.PutAsJsonAsync($"/api/admin/membership-packages/{PackageId}", body);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<MembershipPackageDto>>();
        result!.Data!.Name.Should().Be("月度会员（更新）");
        result.Data.Price.Should().Be(39m);
    }

    [Fact]
    public async Task UpdatePackage_ShouldCallServiceWithRoutePackageId()
    {
        SetupAdminAuth();
        _packageAppServiceMock.Setup(s => s.UpdatePackageAsync(PackageId, It.IsAny<UpdateMembershipPackageDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MembershipPackageDto { Id = PackageId, Name = "x", Level = 1, Price = 1m, DurationDays = 1, Benefits = "{}", Status = PackageStatus.Enabled });

        var body = new { name = "x", price = 1m, durationDays = 1, benefits = "{}" };
        await _client.PutAsJsonAsync($"/api/admin/membership-packages/{PackageId}", body);

        _packageAppServiceMock.Verify(
            s => s.UpdatePackageAsync(PackageId, It.IsAny<UpdateMembershipPackageDto>(), It.IsAny<CancellationToken>()),
            Times.Once,
            "packageId 必须从路由参数取");
    }

    #endregion

    #region AdminMembershipPackagesController - EnablePackage (POST /api/admin/membership-packages/{id}/enable)

    [Fact]
    public async Task EnablePackage_WithOperatorAuth_ReturnsOk()
    {
        SetupOperatorAuth();
        _packageAppServiceMock.Setup(s => s.EnablePackageAsync(PackageId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await _client.PostAsync($"/api/admin/membership-packages/{PackageId}/enable", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK, "返工后不用 204 NoContent");
        var result = await response.Content.ReadFromJsonAsync<ApiResponse>();
        result!.Code.Should().Be(200);
    }

    [Fact]
    public async Task EnablePackage_AlreadyEnabled_ShouldReturnConflict()
    {
        SetupAdminAuth();
        _packageAppServiceMock.Setup(s => s.EnablePackageAsync(PackageId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new MembershipDomainException("套餐已启用", "PACKAGE_ALREADY_ENABLED"));

        var response = await _client.PostAsync($"/api/admin/membership-packages/{PackageId}/enable", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse>();
        result!.Code.Should().Be(409);
    }

    #endregion

    #region AdminMembershipPackagesController - DisablePackage (POST /api/admin/membership-packages/{id}/disable)

    [Fact]
    public async Task DisablePackage_WithOperatorAuth_ReturnsOk()
    {
        SetupOperatorAuth();
        _packageAppServiceMock.Setup(s => s.DisablePackageAsync(PackageId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await _client.PostAsync($"/api/admin/membership-packages/{PackageId}/disable", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK, "返工后不用 204 NoContent");
        var result = await response.Content.ReadFromJsonAsync<ApiResponse>();
        result!.Code.Should().Be(200);
    }

    [Fact]
    public async Task DisablePackage_NotExist_ShouldReturnNotFound()
    {
        SetupAdminAuth();
        _packageAppServiceMock.Setup(s => s.DisablePackageAsync(PackageId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new MembershipDomainException($"会员套餐 {PackageId} 不存在", "PACKAGE_NOT_FOUND"));

        var response = await _client.PostAsync($"/api/admin/membership-packages/{PackageId}/disable", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse>();
        result!.Code.Should().Be(404);
    }

    #endregion

    #region MembersController - GetMyMember (GET /api/members/me)

    [Fact]
    public async Task GetMyMember_WithBuyerAuth_ReturnsOk()
    {
        SetupBuyerAuth();
        var dto = new MemberDto
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            CurrentLevel = 2,
            TotalConsumption = 1500m,
            JoinedAt = DateTime.UtcNow.AddDays(-100),
            LevelUpgradedAt = DateTime.UtcNow.AddDays(-10),
            Status = MemberStatus.Active,
            GrowthValue = 500,
            CurrentGrowthLevel = 2
        };
        _memberAppServiceMock.Setup(s => s.GetMemberInfoAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var response = await _client.GetAsync("/api/members/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<MemberDto>>();
        result!.Code.Should().Be(200);
        result.Data!.UserId.Should().Be(UserId);
        result.Data.CurrentLevel.Should().Be(2);
        result.Data.GrowthValue.Should().Be(500);
    }

    [Fact]
    public async Task GetMyMember_AsAnotherUser_ReturnsOwnInfo()
    {
        // 模拟另一用户登录：ICurrentUserContext.UserId 返回 AnotherUserId
        // 验证 /me 端点不传 userId 参数，服务端从 JWT 取 userId，不会越权查到其他用户
        _currentUserMock.SetupGet(c => c.IsAuthenticated).Returns(true);
        _currentUserMock.SetupGet(c => c.UserId).Returns(AnotherUserId);
        _currentUserMock.SetupGet(c => c.Role).Returns("Buyer");

        var dto = new MemberDto
        {
            Id = Guid.NewGuid(),
            UserId = AnotherUserId,
            CurrentLevel = 1,
            TotalConsumption = 0m,
            JoinedAt = DateTime.UtcNow,
            LevelUpgradedAt = DateTime.UtcNow,
            Status = MemberStatus.Active,
            GrowthValue = 0,
            CurrentGrowthLevel = 0
        };
        _memberAppServiceMock.Setup(s => s.GetMemberInfoAsync(AnotherUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var response = await _client.GetAsync("/api/members/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<MemberDto>>();
        result!.Data!.UserId.Should().Be(AnotherUserId, "应返回当前登录用户自己的信息，不传 userId 参数");

        _memberAppServiceMock.Verify(
            s => s.GetMemberInfoAsync(AnotherUserId, It.IsAny<CancellationToken>()),
            Times.Once,
            "/me 端点从 ICurrentUserContext.UserId 取，禁止客户端传 userId");
    }

    [Fact]
    public async Task GetMyMember_WithoutAuth_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.GetAsync("/api/members/me");
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region AdminMemberLevelsController - ListLevels (GET /api/admin/members/levels)

    [Fact]
    public async Task ListLevels_WithOperatorAuth_ReturnsOk()
    {
        SetupOperatorAuth();
        var levels = new List<MemberLevelDefinitionDto>
        {
            new()
            {
                Id = LevelId,
                Level = 0,
                Name = "V0",
                MinGrowthValue = 0,
                MaxGrowthValue = 100,
                Description = "新手",
                LevelUpBonusPoints = 0,
                Status = LevelDefinitionStatus.Enabled
            },
            new()
            {
                Id = Guid.NewGuid(),
                Level = 1,
                Name = "V1",
                MinGrowthValue = 100,
                MaxGrowthValue = 500,
                Description = "普通",
                LevelUpBonusPoints = 50,
                Status = LevelDefinitionStatus.Enabled
            }
        };
        _memberAppServiceMock.Setup(s => s.GetLevelsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(levels);

        var response = await _client.GetAsync("/api/admin/members/levels");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<MemberLevelDefinitionDto>>>();
        result!.Data.Should().HaveCount(2);
        result.Data![0].Name.Should().Be("V0");
        result.Data[1].LevelUpBonusPoints.Should().Be(50);
    }

    [Fact]
    public async Task ListLevels_WithBuyerAuth_Returns403()
    {
        SetupBuyerAuth();
        _memberAppServiceMock.Setup(s => s.GetLevelsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var response = await _client.GetAsync("/api/admin/members/levels");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden, "返工后 levels 端点加 Operator/Admin 鉴权，Buyer 不可访问");
        _memberAppServiceMock.Verify(
            s => s.GetLevelsAsync(It.IsAny<CancellationToken>()),
            Times.Never,
            "403 时不应调用应用服务");
    }

    [Fact]
    public async Task ListLevels_WithoutAuth_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.GetAsync("/api/admin/members/levels");
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region AdminMemberLevelsController - CreateLevel (POST /api/admin/members/levels)

    [Fact]
    public async Task CreateLevel_WithOperatorAuth_ReturnsOk()
    {
        SetupOperatorAuth();
        var dto = new MemberLevelDefinitionDto
        {
            Id = LevelId,
            Level = 1,
            Name = "V1",
            MinGrowthValue = 100,
            MaxGrowthValue = 500,
            Description = "普通会员",
            LevelUpBonusPoints = 50,
            Status = LevelDefinitionStatus.Enabled
        };
        _memberAppServiceMock.Setup(s => s.CreateLevelAsync(It.IsAny<CreateMemberLevelDefinitionDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var body = new
        {
            level = 1,
            name = "V1",
            minGrowthValue = 100,
            maxGrowthValue = 500,
            description = "普通会员",
            levelUpBonusPoints = 50
        };
        var response = await _client.PostAsJsonAsync("/api/admin/members/levels", body);

        response.StatusCode.Should().Be(HttpStatusCode.OK, "返工后不用 201 CreatedAtAction");
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<MemberLevelDefinitionDto>>();
        result!.Code.Should().Be(200);
        result.Data!.Name.Should().Be("V1");
        result.Data.LevelUpBonusPoints.Should().Be(50);
    }

    #endregion

    #region AdminMemberLevelsController - UpdateLevel (PUT /api/admin/members/levels/{id})

    [Fact]
    public async Task UpdateLevel_WithOperatorAuth_ReturnsOk()
    {
        SetupOperatorAuth();
        var dto = new MemberLevelDefinitionDto
        {
            Id = LevelId,
            Level = 1,
            Name = "V1（更新）",
            MinGrowthValue = 100,
            MaxGrowthValue = 600,
            Description = "普通会员（更新）",
            LevelUpBonusPoints = 80,
            Status = LevelDefinitionStatus.Enabled
        };
        _memberAppServiceMock.Setup(s => s.UpdateLevelAsync(LevelId, It.IsAny<UpdateMemberLevelDefinitionDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var body = new
        {
            name = "V1（更新）",
            minGrowthValue = 100,
            maxGrowthValue = 600,
            description = "普通会员（更新）",
            levelUpBonusPoints = 80
        };
        var response = await _client.PutAsJsonAsync($"/api/admin/members/levels/{LevelId}", body);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<MemberLevelDefinitionDto>>();
        result!.Data!.Name.Should().Be("V1（更新）");
        result.Data.MaxGrowthValue.Should().Be(600);
    }

    [Fact]
    public async Task UpdateLevel_NotExist_ShouldReturnNotFound()
    {
        SetupAdminAuth();
        _memberAppServiceMock.Setup(s => s.UpdateLevelAsync(LevelId, It.IsAny<UpdateMemberLevelDefinitionDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new MembershipDomainException($"会员等级定义 {LevelId} 不存在", "MEMBER_LEVEL_NOT_FOUND"));

        var body = new
        {
            name = "不存在",
            minGrowthValue = 0,
            maxGrowthValue = 100,
            description = "",
            levelUpBonusPoints = 0
        };
        var response = await _client.PutAsJsonAsync($"/api/admin/members/levels/{LevelId}", body);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse>();
        result!.Code.Should().Be(404);
    }

    #endregion

    #region AdminMemberLevelsController - EnableLevel (POST /api/admin/members/levels/{id}/enable)

    [Fact]
    public async Task EnableLevel_WithOperatorAuth_ReturnsOk()
    {
        SetupOperatorAuth();
        _memberAppServiceMock.Setup(s => s.EnableLevelAsync(LevelId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await _client.PostAsync($"/api/admin/members/levels/{LevelId}/enable", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse>();
        result!.Code.Should().Be(200);
    }

    [Fact]
    public async Task EnableLevel_AlreadyEnabled_ShouldReturnConflict()
    {
        SetupAdminAuth();
        _memberAppServiceMock.Setup(s => s.EnableLevelAsync(LevelId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new MembershipDomainException("会员等级定义已启用", "MEMBER_LEVEL_ALREADY_ENABLED"));

        var response = await _client.PostAsync($"/api/admin/members/levels/{LevelId}/enable", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse>();
        result!.Code.Should().Be(409);
    }

    [Fact]
    public async Task EnableLevel_ShouldCallServiceWithRouteLevelId()
    {
        SetupAdminAuth();
        _memberAppServiceMock.Setup(s => s.EnableLevelAsync(LevelId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _client.PostAsync($"/api/admin/members/levels/{LevelId}/enable", null);

        _memberAppServiceMock.Verify(
            s => s.EnableLevelAsync(LevelId, It.IsAny<CancellationToken>()),
            Times.Once,
            "levelId 必须从路由参数取");
    }

    #endregion

    #region AdminMemberLevelsController - DisableLevel (POST /api/admin/members/levels/{id}/disable)

    [Fact]
    public async Task DisableLevel_WithOperatorAuth_ReturnsOk()
    {
        SetupOperatorAuth();
        _memberAppServiceMock.Setup(s => s.DisableLevelAsync(LevelId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await _client.PostAsync($"/api/admin/members/levels/{LevelId}/disable", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse>();
        result!.Code.Should().Be(200);
    }

    [Fact]
    public async Task DisableLevel_AlreadyDisabled_ShouldReturnConflict()
    {
        SetupAdminAuth();
        _memberAppServiceMock.Setup(s => s.DisableLevelAsync(LevelId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new MembershipDomainException("会员等级定义已停用", "MEMBER_LEVEL_ALREADY_DISABLED"));

        var response = await _client.PostAsync($"/api/admin/members/levels/{LevelId}/disable", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse>();
        result!.Code.Should().Be(409);
    }

    [Fact]
    public async Task DisableLevel_NotExist_ShouldReturnNotFound()
    {
        SetupAdminAuth();
        _memberAppServiceMock.Setup(s => s.DisableLevelAsync(LevelId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new MembershipDomainException($"会员等级定义 {LevelId} 不存在", "MEMBER_LEVEL_NOT_FOUND"));

        var response = await _client.PostAsync($"/api/admin/members/levels/{LevelId}/disable", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse>();
        result!.Code.Should().Be(404);
    }

    #endregion

    #region Auth Helpers

    private void SetupBuyerAuth()
    {
        _currentUserMock.SetupGet(c => c.IsAuthenticated).Returns(true);
        _currentUserMock.SetupGet(c => c.UserId).Returns(UserId);
        _currentUserMock.SetupGet(c => c.Role).Returns("Buyer");
        // 设置 X-Test-Role 头使 TestAuthHandler 仅注入 Buyer 角色，触发 [Authorize(Roles="Operator,Admin")] 返回 403
        SetTestRole("Buyer");
    }

    private void SetupOperatorAuth()
    {
        _currentUserMock.SetupGet(c => c.IsAuthenticated).Returns(true);
        _currentUserMock.SetupGet(c => c.UserId).Returns(UserId);
        _currentUserMock.SetupGet(c => c.Role).Returns("Operator");
        SetTestRole("Operator");
    }

    private void SetupAdminAuth()
    {
        _currentUserMock.SetupGet(c => c.IsAuthenticated).Returns(true);
        _currentUserMock.SetupGet(c => c.UserId).Returns(UserId);
        _currentUserMock.SetupGet(c => c.Role).Returns("Admin");
        SetTestRole("Admin");
    }

    private void SetTestRole(string role)
    {
        // 移除旧的 X-Test-Role 头（若存在），再添加当前角色
        _client.DefaultRequestHeaders.Remove("X-Test-Role");
        _client.DefaultRequestHeaders.Add("X-Test-Role", role);
    }

    #endregion
}

/// <summary>
/// 测试鉴权处理器，模拟 JWT 鉴权。
/// 通过 X-Test-Role 请求头控制注入的角色，便于 RBAC 403 测试：
/// - 头存在时：仅注入指定角色（如 Buyer），访问运营端 [Authorize(Roles="Operator,Admin")] 返回 403
/// - 头不存在时：注入全部角色（Buyer/Seller/Admin/Operator），[Authorize] 始终通过
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

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, "test"),
            new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())
        };

        var testRoleHeader = Request.Headers["X-Test-Role"].FirstOrDefault();
        if (!string.IsNullOrEmpty(testRoleHeader))
        {
            // 头存在：仅注入指定角色，用于 RBAC 403 测试
            foreach (var role in testRoleHeader.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }
        }
        else
        {
            // 头不存在：注入全部角色，[Authorize] 始终通过
            claims.Add(new Claim(ClaimTypes.Role, "Buyer"));
            claims.Add(new Claim(ClaimTypes.Role, "Seller"));
            claims.Add(new Claim(ClaimTypes.Role, "Admin"));
            claims.Add(new Claim(ClaimTypes.Role, "Operator"));
        }

        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "Test");

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
