extern alias identity;
extern alias points;
extern alias membership;
extern alias review;
using Leno.Identity.Application;
using Leno.Identity.Application.DTOs;
using Leno.Membership.Application;
using Leno.Membership.Application.DTOs;
using Leno.Membership.Domain.ValueObjects;
using Leno.Points.Application;
using Leno.Points.Application.DTOs;
using Leno.Review.Application;
using Leno.Review.Application.DTOs;
using Leno.Review.Domain.ValueObjects;

// 各域 Api 项目的 Program 类均在全局命名空间下声明（partial class Program），
// 通过 extern alias 引用避免跨域多 Program 类的命名冲突。
// 使用方式：identity::Program / points::Program / membership::Program / review::Program
using IdentityProgram = identity::Program;
using PointsProgram = points::Program;
using MembershipProgram = membership::Program;
using ReviewProgram = review::Program;

namespace Leno.Integration.Tests;

/// <summary>
/// 域迁移跨域集成测试（Task D2）。
/// <para>
/// 验证域拆分迁移计划阶段 1 完成后，4 个新域的关键端点行为契约：
/// <list type="bullet">
///   <item>Identity 域 login 端点返回 ApiResponse&lt;TokenDto&gt; 包装结构</item>
///   <item>Points 域 internal 端点强制 X-Internal-Key 鉴权（fail-closed）</item>
///   <item>Membership 域套餐路径使用连字符 membership-packages（命名规范）</item>
///   <item>Review 域商品评价匿名可访问（匿名端点）</item>
/// </list>
/// </para>
/// <para>
/// 测试使用 WebApplicationFactory 分别启动各域 API 宿主，通过 Mock 替换应用服务层，
/// 聚焦验证 Controller 路由、鉴权中间件、ApiResponse 包装等跨域一致契约。
/// 各域已通过自有 Api.Tests 项目覆盖详细业务场景，本测试仅汇总验证迁移关键不变量。
/// </para>
/// </summary>
public sealed class IdentityDomainMigrationTests : IClassFixture<WebApplicationFactory<IdentityProgram>>
{
    private readonly HttpClient _client;
    private readonly Mock<IAuthAppService> _authAppServiceMock = new();
    private readonly Mock<IUserProfileAppService> _userProfileAppServiceMock = new();
    private readonly Mock<ITwoFactorService> _twoFactorServiceMock = new();
    private readonly Mock<IPasswordService> _passwordServiceMock = new();
    private readonly Mock<IExternalLoginService> _externalLoginServiceMock = new();
    private readonly Mock<IOAuthService> _oauthServiceMock = new();
    private readonly Mock<ICurrentUserContext> _currentUserMock = new();
    private readonly Mock<IOAuthClientAppService> _oauthClientAppServiceMock = new();
    private readonly Mock<IUserAdminAppService> _userAdminAppServiceMock = new();
    private readonly Mock<IUserInternalAppService> _userInternalAppServiceMock = new();

    public IdentityDomainMigrationTests(WebApplicationFactory<IdentityProgram> factory)
    {
        _client = factory.WithWebHostBuilder(builder =>
        {
            // 使用 Development 环境，跳过程序启动期的敏感配置校验（Program.cs 仅在非 Development 抛异常）
            builder.UseSetting("Environment", "Development");
            // 提供 OAuth2:AesKey 配置，避免 AddIdentityInfrastructure 中 fail-fast 检查抛异常
            // 32 字节全零 Base64 编码（仅测试用，非生产密钥）
            builder.UseSetting("OAuth2:AesKey", "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=");
            // 清空 InternalAuth:ApiKey，使 InternalApiKeyMiddleware 在 Development 环境放行 /internal 请求
            builder.UseSetting("InternalAuth:ApiKey", "");

            builder.ConfigureServices(services =>
            {
                // 先移除真实服务注册（Scoped），再添加 Mock 单例，避免 Remove 方法误删 Mock 注册
                IntegrationTestHostHelpers.RemoveMassTransitServices(services);
                IntegrationTestHostHelpers.RemoveElasticsearchServices(services);
                IntegrationTestHostHelpers.RemoveEventBusServices(services);
                IntegrationTestHostHelpers.ReplaceDistributedLockProvider(services);

                RemoveIdentityApplicationServices(services);

                services.AddSingleton(_authAppServiceMock.Object);
                services.AddSingleton(_userProfileAppServiceMock.Object);
                services.AddSingleton(_twoFactorServiceMock.Object);
                services.AddSingleton(_passwordServiceMock.Object);
                services.AddSingleton(_externalLoginServiceMock.Object);
                services.AddSingleton(_oauthServiceMock.Object);
                services.AddSingleton(_currentUserMock.Object);
                services.AddSingleton(_oauthClientAppServiceMock.Object);
                services.AddSingleton(_userAdminAppServiceMock.Object);
                services.AddSingleton(_userInternalAppServiceMock.Object);

                services.AddAuthentication(defaultScheme: IntegrationTestAuthHandler.SchemeName)
                    .AddScheme<AuthenticationSchemeOptions, IntegrationTestAuthHandler>(
                        IntegrationTestAuthHandler.SchemeName, _ => { });
            });
        }).CreateClient();

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(IntegrationTestAuthHandler.SchemeName);
    }

    private static void RemoveIdentityApplicationServices(IServiceCollection services)
    {
        var appServiceInterfaces = new[]
        {
            typeof(IAuthAppService),
            typeof(IUserProfileAppService),
            typeof(ITwoFactorService),
            typeof(IPasswordService),
            typeof(IExternalLoginService),
            typeof(IOAuthService),
            typeof(IOAuthClientAppService),
            typeof(IUserAdminAppService),
            typeof(IUserInternalAppService)
        };

        var descriptors = services
            .Where(s => appServiceInterfaces.Contains(s.ServiceType))
            .ToList();
        foreach (var d in descriptors) services.Remove(d);
    }

    /// <summary>
    /// 验证 Identity 域 POST /api/auth/login 端点返回 ApiResponse&lt;TokenDto&gt; 包装结构。
    /// 关键不变量：
    /// 1. 端点存在且为匿名访问（无 Authorization 头也能调用）
    /// 2. 响应体由 ApiResponse&lt;TokenDto&gt; 包装（Code=200, Message="success"）
    /// 3. Data 字段携带 AccessToken、RefreshToken、ExpiresAt 三个令牌字段
    /// </summary>
    [Fact]
    public async Task Identity_Login_Endpoint_Returns_ApiResponse_Token()
    {
        // Arrange：Mock AuthAppService 返回标准 TokenDto
        var expectedToken = new TokenDto
        {
            AccessToken = "access-token-integration-test",
            RefreshToken = "refresh-token-integration-test",
            ExpiresAt = DateTime.UtcNow.AddMinutes(30)
        };
        _authAppServiceMock.Setup(s => s.LoginAsync(It.IsAny<LoginDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedToken);

        var loginBody = new { usernameOrEmail = "integration-tester", password = "Password123!" };

        // Act：不带 Authorization 头调用 login（应为匿名端点）
        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.PostAsJsonAsync("/api/auth/login", loginBody);
        // 恢复头部，避免污染后续测试
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(IntegrationTestAuthHandler.SchemeName);

        // Assert：HTTP 200 + ApiResponse 包装 + TokenDto 数据契约
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "login 是 [AllowAnonymous] 端点，未携带 Authorization 头时应正常处理");

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<TokenDto>>();
        result.Should().NotBeNull("响应体必须是 ApiResponse<T> 结构");
        result!.Code.Should().Be(200, "ApiResponse.Code 必须为 200 表示成功");
        result.Message.Should().Be("success", "ApiResponse.Message 必须为 success（由 ApiResponse.Success 工厂方法设置）");
        result.Data.Should().NotBeNull("ApiResponse.Data 必须承载 TokenDto");
        result.Data!.AccessToken.Should().Be("access-token-integration-test",
            "AccessToken 必须由 AuthAppService.LoginAsync 返回并透传给 ApiResponse.Success");
        result.Data.RefreshToken.Should().Be("refresh-token-integration-test",
            "RefreshToken 必须由 AuthAppService.LoginAsync 返回并透传给 ApiResponse.Success");
        result.Data.ExpiresAt.Should().BeCloseTo(expectedToken.ExpiresAt, TimeSpan.FromSeconds(1),
            "ExpiresAt 必须由 AuthAppService.LoginAsync 返回并透传给 ApiResponse.Success");
    }
}

/// <summary>
/// Points 域迁移集成测试：验证 internal 端点强制 X-Internal-Key 鉴权。
/// </summary>
public sealed class PointsDomainMigrationTests : IClassFixture<WebApplicationFactory<PointsProgram>>
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

    private const string TestInternalKey = "test-internal-key-points-integration";

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid OrderId = Guid.NewGuid();

    public PointsDomainMigrationTests(WebApplicationFactory<PointsProgram> factory)
    {
        _client = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Environment", "Development");

            builder.ConfigureServices(services =>
            {
                IntegrationTestHostHelpers.RemoveMassTransitServices(services);
                IntegrationTestHostHelpers.RemoveElasticsearchServices(services);
                IntegrationTestHostHelpers.RemoveEventBusServices(services);
                IntegrationTestHostHelpers.ReplaceDistributedLockProvider(services);

                RemovePointsApplicationServices(services);

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

                services.AddAuthentication(defaultScheme: IntegrationTestAuthHandler.SchemeName)
                    .AddScheme<AuthenticationSchemeOptions, IntegrationTestAuthHandler>(
                        IntegrationTestAuthHandler.SchemeName, _ => { });
            });
        }).CreateClient();

        // 内部端点不依赖 JWT，但仍需 Authorization 头通过 TestAuthHandler（避免 401 干扰）
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(IntegrationTestAuthHandler.SchemeName);
    }

    private static void RemovePointsApplicationServices(IServiceCollection services)
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
    /// 验证 Points 域 POST internal/v1/points/freeze 端点强制 X-Internal-Key 鉴权。
    /// 关键不变量：
    /// 1. 携带正确 X-Internal-Key：返回 200 OK，业务正常处理
    /// 2. 缺失 X-Internal-Key：返回 401 Unauthorized（fail-closed）
    /// 3. 错误 X-Internal-Key：返回 401 Unauthorized
    /// </summary>
    [Fact]
    public async Task Points_Internal_Freeze_Endpoint_Requires_Internal_Key()
    {
        // Arrange：Mock IPointsInternalAppService.FreezeAsync 返回标准冻结结果
        var freezeResult = new FreezeResultDto
        {
            Success = true,
            Points = 100,
            OrderId = OrderId,
            AccountId = Guid.NewGuid(),
            AvailableBalanceAfter = 400,
            FrozenBalanceAfter = 100
        };
        _internalAppServiceMock.Setup(s => s.FreezeAsync(UserId, 100, OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(freezeResult);

        var requestBody = new { userId = UserId, points = 100, orderId = OrderId };

        // Act 1：携带正确 X-Internal-Key 调用 internal/v1/points/freeze
        var validRequest = new HttpRequestMessage(HttpMethod.Post, "/internal/v1/points/freeze")
        {
            Content = JsonContent.Create(requestBody)
        };
        validRequest.Headers.Add("X-Internal-Key", TestInternalKey);
        var validResponse = await _client.SendAsync(validRequest);

        // Assert 1：正确 key 时返回 200，业务正常处理
        validResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            "携带正确 X-Internal-Key 时 InternalApiKeyMiddleware 应放行请求");
        var validResult = await validResponse.Content.ReadFromJsonAsync<ApiResponse<FreezeResultDto>>();
        validResult.Should().NotBeNull("internal 端点同样使用 ApiResponse<T> 包装");
        validResult!.Code.Should().Be(200);
        validResult.Data!.Success.Should().BeTrue("应透传 FreezeResultDto.Success");
        validResult.Data.Points.Should().Be(100);
        validResult.Data.OrderId.Should().Be(OrderId);

        // Act 2：缺失 X-Internal-Key 调用同一端点
        var missingKeyRequest = new HttpRequestMessage(HttpMethod.Post, "/internal/v1/points/freeze")
        {
            Content = JsonContent.Create(requestBody)
        };
        // 不添加 X-Internal-Key 头
        var missingKeyResponse = await _client.SendAsync(missingKeyRequest);

        // Assert 2：缺失 key 时返回 401（fail-closed）
        missingKeyResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "缺失 X-Internal-Key 时 InternalApiKeyMiddleware 必须返回 401（fail-closed）");

        // Act 3：携带错误 X-Internal-Key 调用同一端点
        var wrongKeyRequest = new HttpRequestMessage(HttpMethod.Post, "/internal/v1/points/freeze")
        {
            Content = JsonContent.Create(requestBody)
        };
        wrongKeyRequest.Headers.Add("X-Internal-Key", "wrong-key-" + Guid.NewGuid());
        var wrongKeyResponse = await _client.SendAsync(wrongKeyRequest);

        // Assert 3：错误 key 时返回 401（fail-closed）
        wrongKeyResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "错误 X-Internal-Key 时 InternalApiKeyMiddleware 必须返回 401（fail-closed）");

        // Verify：成功路径下应用服务被调用一次，失败路径下应用服务未被调用
        _internalAppServiceMock.Verify(
            s => s.FreezeAsync(UserId, 100, OrderId, It.IsAny<CancellationToken>()),
            Times.Once,
            "仅正确 key 的请求应进入应用服务层");
    }
}

/// <summary>
/// Membership 域迁移集成测试：验证套餐路径使用连字符 membership-packages（命名规范）。
/// </summary>
public sealed class MembershipDomainMigrationTests : IClassFixture<WebApplicationFactory<MembershipProgram>>
{
    private readonly HttpClient _client;
    private readonly Mock<IMembershipPackageAppService> _packageAppServiceMock = new();
    private readonly Mock<IMemberAppService> _memberAppServiceMock = new();
    private readonly Mock<ICurrentUserContext> _currentUserMock = new();

    private static readonly Guid UserId = Guid.NewGuid();

    public MembershipDomainMigrationTests(WebApplicationFactory<MembershipProgram> factory)
    {
        _client = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Environment", "Development");

            builder.ConfigureServices(services =>
            {
                IntegrationTestHostHelpers.RemoveMassTransitServices(services);
                IntegrationTestHostHelpers.RemoveElasticsearchServices(services);
                IntegrationTestHostHelpers.RemoveEventBusServices(services);
                IntegrationTestHostHelpers.ReplaceDistributedLockProvider(services);

                RemoveMembershipApplicationServices(services);

                services.AddSingleton(_packageAppServiceMock.Object);
                services.AddSingleton(_memberAppServiceMock.Object);
                services.AddSingleton(_currentUserMock.Object);

                services.Configure<InternalApiKeyOptions>(o =>
                {
                    o.ApiKey = "test-internal-key-membership-integration";
                    o.RoutePrefix = "internal/";
                });

                services.AddAuthentication(defaultScheme: IntegrationTestAuthHandler.SchemeName)
                    .AddScheme<AuthenticationSchemeOptions, IntegrationTestAuthHandler>(
                        IntegrationTestAuthHandler.SchemeName, _ => { });
            });
        }).CreateClient();

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(IntegrationTestAuthHandler.SchemeName);
        // 设置 X-Test-Role 头注入 Buyer 角色（套餐列表端点要求 [Authorize(Roles = "Buyer")]）
        _client.DefaultRequestHeaders.Add("X-Test-Role", "Buyer");

        // 配置 ICurrentUserContext 为已认证 Buyer
        _currentUserMock.SetupGet(c => c.IsAuthenticated).Returns(true);
        _currentUserMock.SetupGet(c => c.UserId).Returns(UserId);
        _currentUserMock.SetupGet(c => c.Role).Returns("Buyer");
    }

    private static void RemoveMembershipApplicationServices(IServiceCollection services)
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
    /// 验证 Membership 域套餐端点路径为 /api/membership-packages（连字符命名规范）。
    /// 关键不变量：
    /// 1. GET /api/membership-packages 返回 200 OK（路径存在且路由匹配）
    /// 2. GET /api/membershippackages（无连字符）返回 404 Not Found（旧路径不存在）
    /// 3. 响应体由 ApiResponse&lt;List&lt;MembershipPackageDto&gt;&gt; 包装
    /// </summary>
    [Fact]
    public async Task Membership_Packages_Path_Uses_Hyphen()
    {
        // Arrange：Mock 套餐列表返回 2 个套餐
        var packages = new List<MembershipPackageDto>
        {
            new()
            {
                Id = Guid.NewGuid(),
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

        // Act 1：调用连字符路径 /api/membership-packages
        var hyphenResponse = await _client.GetAsync("/api/membership-packages");

        // Assert 1：200 OK + ApiResponse 包装
        hyphenResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            "GET /api/membership-packages 路径必须存在并返回 200");
        var hyphenResult = await hyphenResponse.Content.ReadFromJsonAsync<ApiResponse<List<MembershipPackageDto>>>();
        hyphenResult.Should().NotBeNull("响应必须是 ApiResponse<List<MembershipPackageDto>> 包装");
        hyphenResult!.Code.Should().Be(200);
        hyphenResult.Data.Should().HaveCount(2, "应透传 Mock 返回的 2 个套餐");
        hyphenResult.Data![0].Name.Should().Be("月度会员");
        hyphenResult.Data[1].Name.Should().Be("年度会员");

        // Act 2：调用无连字符路径 /api/membershippackages（应不存在）
        var noHyphenResponse = await _client.GetAsync("/api/membershippackages");

        // Assert 2：404 Not Found（命名规范：必须使用连字符）
        noHyphenResponse.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "GET /api/membershippackages（无连字符）路径必须不存在，命名规范要求使用 membership-packages");

        // Verify：仅连字符路径触发了应用服务调用
        _packageAppServiceMock.Verify(
            s => s.GetPackagesAsync(It.IsAny<CancellationToken>()),
            Times.Once,
            "仅 /api/membership-packages 路径应触发 GetPackagesAsync 调用");
    }
}

/// <summary>
/// Review 域迁移集成测试：验证商品评价匿名可访问。
/// </summary>
public sealed class ReviewDomainMigrationTests : IClassFixture<WebApplicationFactory<ReviewProgram>>
{
    private readonly HttpClient _client;
    private readonly Mock<IReviewAppService> _reviewAppServiceMock = new();
    private readonly Mock<ICurrentUserContext> _currentUserMock = new();

    private static readonly Guid SpuId = Guid.NewGuid();

    public ReviewDomainMigrationTests(WebApplicationFactory<ReviewProgram> factory)
    {
        _client = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Environment", "Development");

            builder.ConfigureServices(services =>
            {
                IntegrationTestHostHelpers.RemoveMassTransitServices(services);
                IntegrationTestHostHelpers.RemoveElasticsearchServices(services);
                IntegrationTestHostHelpers.RemoveRedisServices(services);
                IntegrationTestHostHelpers.RemoveEventBusServices(services);
                IntegrationTestHostHelpers.ReplaceDistributedLockProvider(services);

                // 移除 Review 域注册的真实 IReviewAppService（Scoped），由 Mock 单例替换
                var reviewServiceDescriptor = services
                    .Where(s => s.ServiceType == typeof(IReviewAppService))
                    .ToList();
                foreach (var d in reviewServiceDescriptor) services.Remove(d);

                services.AddSingleton(_reviewAppServiceMock.Object);
                services.AddSingleton(_currentUserMock.Object);

                services.AddAuthentication(defaultScheme: IntegrationTestAuthHandler.SchemeName)
                    .AddScheme<AuthenticationSchemeOptions, IntegrationTestAuthHandler>(
                        IntegrationTestAuthHandler.SchemeName, _ => { });
            });
        }).CreateClient();
    }

    /// <summary>
    /// 验证 Review 域 GET /api/products/{spuId}/reviews 端点匿名可访问。
    /// 关键不变量：
    /// 1. 不携带 Authorization 头也能调用（匿名端点）
    /// 2. 返回 200 OK，响应体由 ApiResponse&lt;ReviewListResultDto&gt; 包装
    /// 3. 路由参数 spuId 正确透传到应用服务
    /// </summary>
    [Fact]
    public async Task Review_Anonymous_Can_Access_Product_Reviews()
    {
        // Arrange：Mock GetReviewsBySpuAsync 返回标准分页结果
        var expectedResult = new ReviewListResultDto
        {
            Items = new List<ReviewDto>
            {
                new()
                {
                    ReviewId = Guid.NewGuid(),
                    OrderId = Guid.NewGuid(),
                    OrderLineId = Guid.NewGuid(),
                    SpuId = SpuId,
                    SkuId = Guid.NewGuid(),
                    UserId = Guid.NewGuid(),
                    SellerId = Guid.NewGuid(),
                    Rating = 5,
                    Content = "商品质量很好，物流很快！",
                    Images = new List<string>(),
                    Status = ReviewStatus.Approved,
                    SubmittedAt = DateTime.UtcNow.AddDays(-1),
                    AppendImages = new List<string>()
                },
                new()
                {
                    ReviewId = Guid.NewGuid(),
                    OrderId = Guid.NewGuid(),
                    OrderLineId = Guid.NewGuid(),
                    SpuId = SpuId,
                    SkuId = Guid.NewGuid(),
                    UserId = Guid.NewGuid(),
                    SellerId = Guid.NewGuid(),
                    Rating = 4,
                    Content = "整体不错，包装可以改进。",
                    Images = new List<string>(),
                    Status = ReviewStatus.Approved,
                    SubmittedAt = DateTime.UtcNow.AddDays(-2),
                    AppendImages = new List<string>()
                }
            },
            Total = 2,
            Page = 1,
            PageSize = 20
        };
        _reviewAppServiceMock.Setup(s => s.GetReviewsBySpuAsync(SpuId, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act：不带 Authorization 头调用商品评价列表端点
        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.GetAsync($"/api/products/{SpuId}/reviews?page=1&pageSize=20");
        // 恢复头部，避免污染后续测试
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(IntegrationTestAuthHandler.SchemeName);

        // Assert：HTTP 200 + ApiResponse 包装 + 数据契约
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "GET /api/products/{spuId}/reviews 必须为匿名端点，未携带 Authorization 头时应正常返回");

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<ReviewListResultDto>>();
        result.Should().NotBeNull("响应体必须是 ApiResponse<ReviewListResultDto> 结构");
        result!.Code.Should().Be(200, "ApiResponse.Code 必须为 200");
        result.Message.Should().Be("success", "ApiResponse.Message 必须为 success");
        result.Data.Should().NotBeNull("ApiResponse.Data 必须承载 ReviewListResultDto");
        result.Data!.Total.Should().Be(2, "Total 字段应透传 Mock 返回的总数");
        result.Data.Page.Should().Be(1);
        result.Data.PageSize.Should().Be(20);
        result.Data.Items.Should().HaveCount(2, "Items 列表应透传 Mock 返回的 2 条评价");
        result.Data.Items[0].Rating.Should().Be(5);
        result.Data.Items[0].Content.Should().Be("商品质量很好，物流很快！");
        result.Data.Items[1].Rating.Should().Be(4);

        // Verify：spuId 路由参数正确透传到应用服务
        _reviewAppServiceMock.Verify(
            s => s.GetReviewsBySpuAsync(SpuId, 1, 20, It.IsAny<CancellationToken>()),
            Times.Once,
            "spuId 必须从路由参数取并透传到 GetReviewsBySpuAsync");
    }
}

/// <summary>
/// 集成测试共享宿主辅助方法。
/// 集中处理各域 Program.cs 注册的真实依赖（MassTransit、Elasticsearch、EventBus、IDistributedLockProvider），
/// 这些依赖在测试环境无可用基础设施，必须移除或替换为 Mock，否则 WebApplicationFactory 启动会失败。
/// </summary>
internal static class IntegrationTestHostHelpers
{
    /// <summary>
    /// 移除 MassTransit 相关注册（IBus、IBusControl、IPublishEndpoint、ISendEndpointProvider 等）。
    /// 测试环境无 RabbitMQ，避免 MassTransit 连接失败阻止宿主启动。
    /// </summary>
    public static void RemoveMassTransitServices(IServiceCollection services)
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
    }

    /// <summary>
    /// 移除 Elasticsearch / Nest 相关注册。
    /// 测试环境无 Elasticsearch 节点，避免连接超时阻止宿主启动。
    /// 同时移除依赖 ElasticsearchClient 的派生注册：
    /// <list type="bullet">
    ///   <item>IEsReadModelRepository&lt;T&gt; 开放泛型（其实现 EsReadModelRepository&lt;T&gt; 依赖 ElasticsearchClient）</item>
    ///   <item>IHostedService 实现中的 *IndexInitializer（启动期创建 ES 索引，依赖 ElasticsearchClient）</item>
    ///   <item>MassTransit 消费者 *ReadModelSyncConsumer（依赖 IEsReadModelRepository&lt;T&gt;，间接依赖 ElasticsearchClient）</item>
    /// </list>
    /// </summary>
    public static void RemoveElasticsearchServices(IServiceCollection services)
    {
        // 1. 移除 ElasticsearchClient 及其直接工厂注册
        var descriptors = services
            .Where(s => s.ServiceType.FullName?.Contains("Elasticsearch") == true
                     || s.ServiceType.FullName?.Contains("Elastic") == true
                     || s.ServiceType.FullName?.Contains("Nest") == true
                     || s.ImplementationType?.FullName?.Contains("Elastic") == true)
            .ToList();
        foreach (var d in descriptors) services.Remove(d);

        // 2. 移除 IEsReadModelRepository<T> 开放泛型注册（实现 EsReadModelRepository<T> 构造函数依赖 ElasticsearchClient）
        var esRepoDescriptors = services
            .Where(s => s.ServiceType.IsGenericType
                     && s.ServiceType.GetGenericTypeDefinition().FullName?.Contains("IEsReadModelRepository") == true)
            .ToList();
        foreach (var d in esRepoDescriptors) services.Remove(d);

        // 3. 移除依赖 ElasticsearchClient 的 HostedService（如 ReviewIndexInitializer）
        var hostedServiceDescriptors = services
            .Where(s => s.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService)
                     && s.ImplementationType?.FullName?.Contains("IndexInitializer") == true)
            .ToList();
        foreach (var d in hostedServiceDescriptors) services.Remove(d);

        // 4. 移除依赖 IEsReadModelRepository<T> 的 MassTransit 消费者（如 ReviewReadModelSyncConsumer）
        //    双重保险：即使 RemoveMassTransitServices 已移除 IBus，消费者自身的 Scoped 注册仍可能被 ServiceProvider 校验
        var consumerDescriptors = services
            .Where(s => s.ImplementationType?.FullName?.Contains("ReadModelSyncConsumer") == true
                     || s.ImplementationType?.FullName?.Contains("ReviewReadModelSyncConsumer") == true)
            .ToList();
        foreach (var d in consumerDescriptors) services.Remove(d);
    }

    /// <summary>
    /// 移除 Redis 相关注册（StackExchange.Redis、IDatabase 等）。
    /// 测试环境无 Redis，避免 ConnectionMultiplexer 连接失败阻止宿主启动。
    /// </summary>
    public static void RemoveRedisServices(IServiceCollection services)
    {
        var descriptors = services
            .Where(s => s.ServiceType.FullName?.Contains("StackExchange.Redis") == true
                     || s.ServiceType.FullName?.Contains("Redis") == true
                     || s.ImplementationType?.FullName?.Contains("Redis") == true)
            .ToList();
        foreach (var d in descriptors) services.Remove(d);
    }

    /// <summary>
    /// 移除 IEventBus 注册（RabbitMqEventBus 依赖 MassTransit.IPublishEndpoint，移除 MassTransit 后无法构造）。
    /// 注意：仅移除 Leno.Infrastructure.Abstractions.IEventBus，保留 IIntegrationEventMapper（UnitOfWork 依赖）。
    /// </summary>
    public static void RemoveEventBusServices(IServiceCollection services)
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
    public static void ReplaceDistributedLockProvider(IServiceCollection services)
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
}

/// <summary>
/// 集成测试共享鉴权处理器，模拟 JWT 鉴权。
/// 通过 X-Test-Role 请求头控制注入的角色，便于 RBAC 403 测试：
/// <list type="bullet">
///   <item>头存在时：仅注入指定角色（如 Buyer），访问运营端 [Authorize(Roles="Operator,Admin")] 返回 403</item>
///   <item>头不存在时：注入全部角色（Buyer/Seller/Admin/Operator），[Authorize] 始终通过</item>
/// </list>
/// 各域 Api.Tests 项目自有同名 TestAuthHandler，本类专供 Integration.Tests 项目使用，避免命名冲突。
/// </summary>
public sealed class IntegrationTestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "IntegrationTest";

    public IntegrationTestAuthHandler(
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
            new(ClaimTypes.Name, "integration-test"),
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

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
