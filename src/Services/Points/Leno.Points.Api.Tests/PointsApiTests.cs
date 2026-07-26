using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Leno.Infrastructure.Auth;
using Leno.Points.Application;
using Leno.Points.Application.DTOs;
using Leno.Points.Domain.ValueObjects;
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
/// 积分域买家端 API 集成测试。
/// 覆盖 PointsController（签到、账户、流水、兑换优惠券 4 端点）与 TasksController（任务列表、完成任务 2 端点）。
/// 使用 WebApplicationFactory+Mock 应用服务，验证路由、鉴权、ApiResponse 包装、UserId 从 JWT 注入。
/// </summary>
public class PointsApiTests : IClassFixture<WebApplicationFactory<Program>>
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

    private const string TestInternalKey = "test-internal-key-points";

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid CouponTemplateId = Guid.NewGuid();
    private static readonly Guid TaskId = Guid.NewGuid();

    public PointsApiTests(WebApplicationFactory<Program> factory)
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

    #region Health

    [Fact]
    public async Task Health_Live_ShouldReturnOk()
    {
        var response = await _client.GetAsync("/health/live");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion

    #region Authentication

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_ShouldReturnUnauthorized()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.GetAsync("/api/points/account");
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ProtectedEndpoint_WhenCurrentUserNotAuthenticated_ShouldReturnUnauthorized()
    {
        // 模拟 ICurrentUserContext 未认证（IsAuthenticated=false, UserId=null）
        _currentUserMock.SetupGet(c => c.IsAuthenticated).Returns(false);
        _currentUserMock.SetupGet(c => c.UserId).Returns((Guid?)null);

        var response = await _client.GetAsync("/api/points/account");

        // GetCurrentUserId 抛出 UnauthorizedAccessException → 全局异常中间件映射为 401
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region PointsController - CheckIn (POST /api/points/check-in)

    [Fact]
    public async Task CheckIn_ShouldReturnCheckInResult()
    {
        SetupBuyerAuth();
        var resultDto = new CheckInResultDto
        {
            RecordId = Guid.NewGuid(),
            UserId = UserId,
            CheckInDate = new DateOnly(2026, 7, 26),
            ContinuousDays = 3,
            PointsAwarded = 10
        };
        _checkInAppServiceMock.Setup(s => s.CheckInAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(resultDto);

        var response = await _client.PostAsync("/api/points/check-in", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<CheckInResultDto>>();
        result!.Code.Should().Be(200);
        result.Data!.UserId.Should().Be(UserId);
        result.Data.ContinuousDays.Should().Be(3);
        result.Data.PointsAwarded.Should().Be(10);
    }

    [Fact]
    public async Task CheckIn_ShouldCallServiceWithJwtUserId()
    {
        SetupBuyerAuth();
        var resultDto = new CheckInResultDto
        {
            RecordId = Guid.NewGuid(),
            UserId = UserId,
            CheckInDate = new DateOnly(2026, 7, 26),
            ContinuousDays = 1,
            PointsAwarded = 5
        };
        _checkInAppServiceMock.Setup(s => s.CheckInAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(resultDto);

        await _client.PostAsync("/api/points/check-in", null);

        _checkInAppServiceMock.Verify(
            s => s.CheckInAsync(UserId, It.IsAny<CancellationToken>()),
            Times.Once,
            "UserId 必须从 JWT 注入，不由客户端传入");
    }

    #endregion

    #region PointsController - GetAccount (GET /api/points/account)

    [Fact]
    public async Task GetAccount_ShouldReturnAccountDto()
    {
        SetupBuyerAuth();
        var dto = new PointsAccountDto
        {
            AccountId = Guid.NewGuid(),
            UserId = UserId,
            AvailableBalance = 500,
            FrozenBalance = 100,
            TotalEarned = 1000,
            TotalSpent = 400
        };
        _pointsAppServiceMock.Setup(s => s.GetAccountAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var response = await _client.GetAsync("/api/points/account");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PointsAccountDto>>();
        result!.Data!.AvailableBalance.Should().Be(500);
        result.Data.FrozenBalance.Should().Be(100);
        result.Data.TotalEarned.Should().Be(1000);
        result.Data.TotalSpent.Should().Be(400);
        result.Data.UserId.Should().Be(UserId);
    }

    [Fact]
    public async Task GetAccount_ShouldCallServiceWithJwtUserId()
    {
        SetupBuyerAuth();
        var dto = new PointsAccountDto
        {
            AccountId = Guid.NewGuid(),
            UserId = UserId,
            AvailableBalance = 0,
            FrozenBalance = 0,
            TotalEarned = 0,
            TotalSpent = 0
        };
        _pointsAppServiceMock.Setup(s => s.GetAccountAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        await _client.GetAsync("/api/points/account");

        _pointsAppServiceMock.Verify(
            s => s.GetAccountAsync(UserId, It.IsAny<CancellationToken>()),
            Times.Once,
            "/me 端点从 ICurrentUserContext.UserId 取，不传 userId 参数");
    }

    #endregion

    #region PointsController - GetLedger (GET /api/points/ledger)

    [Fact]
    public async Task GetLedger_ShouldReturnFlowList()
    {
        SetupBuyerAuth();
        var flows = new List<PointsFlowDto>
        {
            new()
            {
                FlowId = Guid.NewGuid(),
                AccountId = Guid.NewGuid(),
                TxType = PointsTxType.Earn,
                Amount = 10,
                BalanceAfter = 510,
                Source = PointsSource.CheckIn,
                ReferenceId = Guid.NewGuid(),
                Reason = "每日签到",
                OccurredAt = DateTime.UtcNow
            },
            new()
            {
                FlowId = Guid.NewGuid(),
                AccountId = Guid.NewGuid(),
                TxType = PointsTxType.CouponExchange,
                Amount = -50,
                BalanceAfter = 460,
                Source = PointsSource.CouponExchange,
                ReferenceId = Guid.NewGuid(),
                Reason = "积分兑换优惠券",
                OccurredAt = DateTime.UtcNow
            }
        };
        _pointsAppServiceMock.Setup(s => s.GetLedgerAsync(UserId, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(flows);

        var response = await _client.GetAsync("/api/points/ledger?page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<PointsFlowDto>>>();
        result!.Data.Should().HaveCount(2);
        result.Data![0].Amount.Should().Be(10);
        result.Data[1].Amount.Should().Be(-50);
    }

    [Fact]
    public async Task GetLedger_WithDefaultPaging_ShouldUsePage1PageSize20()
    {
        SetupBuyerAuth();
        _pointsAppServiceMock.Setup(s => s.GetLedgerAsync(UserId, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        await _client.GetAsync("/api/points/ledger");

        _pointsAppServiceMock.Verify(
            s => s.GetLedgerAsync(UserId, 1, 20, It.IsAny<CancellationToken>()),
            Times.Once,
            "默认分页参数 page=1, pageSize=20");
    }

    [Fact]
    public async Task GetLedger_WithCustomPaging_ShouldPassPageAndPageSize()
    {
        SetupBuyerAuth();
        _pointsAppServiceMock.Setup(s => s.GetLedgerAsync(UserId, 3, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        await _client.GetAsync("/api/points/ledger?page=3&pageSize=50");

        _pointsAppServiceMock.Verify(
            s => s.GetLedgerAsync(UserId, 3, 50, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region PointsController - ExchangeCoupon (POST /api/points/exchange-coupon)

    [Fact]
    public async Task ExchangeCoupon_ShouldReturnExchangeResult()
    {
        SetupBuyerAuth();
        var resultDto = new ExchangeCouponResultDto
        {
            ExchangeId = Guid.NewGuid(),
            UserId = UserId,
            CouponTemplateId = CouponTemplateId,
            PointsFrozen = 100,
            Status = "Pending"
        };
        _exchangeCouponAppServiceMock.Setup(s => s.ExchangeAsync(
                UserId, CouponTemplateId, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(resultDto);

        var body = new
        {
            couponTemplateId = CouponTemplateId,
            pointsRequired = 100
        };
        var response = await _client.PostAsJsonAsync("/api/points/exchange-coupon", body);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<ExchangeCouponResultDto>>();
        result!.Data!.ExchangeId.Should().NotBe(Guid.Empty);
        result.Data.UserId.Should().Be(UserId);
        result.Data.CouponTemplateId.Should().Be(CouponTemplateId);
        result.Data.PointsFrozen.Should().Be(100);
        result.Data.Status.Should().Be("Pending");
    }

    [Fact]
    public async Task ExchangeCoupon_ShouldCallServiceWithJwtUserId()
    {
        SetupBuyerAuth();
        var resultDto = new ExchangeCouponResultDto
        {
            ExchangeId = Guid.NewGuid(),
            UserId = UserId,
            CouponTemplateId = CouponTemplateId,
            PointsFrozen = 200,
            Status = "Pending"
        };
        _exchangeCouponAppServiceMock.Setup(s => s.ExchangeAsync(
                UserId, CouponTemplateId, 200, It.IsAny<CancellationToken>()))
            .ReturnsAsync(resultDto);

        var body = new
        {
            couponTemplateId = CouponTemplateId,
            pointsRequired = 200
        };
        await _client.PostAsJsonAsync("/api/points/exchange-coupon", body);

        _exchangeCouponAppServiceMock.Verify(
            s => s.ExchangeAsync(UserId, CouponTemplateId, 200, It.IsAny<CancellationToken>()),
            Times.Once,
            "UserId 必须从 JWT 注入，不由客户端传入");
    }

    #endregion

    #region TasksController - GetTasks (GET /api/points/tasks)

    [Fact]
    public async Task GetTasks_ShouldReturnTaskList()
    {
        SetupBuyerAuth();
        var tasks = new List<TaskDto>
        {
            new()
            {
                Id = TaskId,
                Type = TaskType.DailyCheckIn,
                Name = "每日签到",
                Description = "每天签到获取积分",
                RewardPoints = 5,
                CompletionCondition = "完成签到",
                IsDaily = true,
                IsOneTime = false,
                IsEnabled = true,
                UserStatus = UserTaskStatus.Completed,
                CompletedAt = DateTime.UtcNow
            },
            new()
            {
                Id = Guid.NewGuid(),
                Type = TaskType.CompleteProfile,
                Name = "完善资料",
                Description = "首次完善个人资料",
                RewardPoints = 50,
                CompletionCondition = "完善全部资料",
                IsDaily = false,
                IsOneTime = true,
                IsEnabled = true,
                UserStatus = UserTaskStatus.Pending,
                CompletedAt = null
            }
        };
        _taskAppServiceMock.Setup(s => s.GetTasksAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tasks);

        var response = await _client.GetAsync("/api/points/tasks");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<TaskDto>>>();
        result!.Data.Should().HaveCount(2);
        result.Data![0].Name.Should().Be("每日签到");
        result.Data[0].IsDaily.Should().BeTrue();
        result.Data[1].IsOneTime.Should().BeTrue();
    }

    [Fact]
    public async Task GetTasks_ShouldCallServiceWithJwtUserId()
    {
        SetupBuyerAuth();
        _taskAppServiceMock.Setup(s => s.GetTasksAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        await _client.GetAsync("/api/points/tasks");

        _taskAppServiceMock.Verify(
            s => s.GetTasksAsync(UserId, It.IsAny<CancellationToken>()),
            Times.Once,
            "UserId 必须从 JWT 注入");
    }

    #endregion

    #region TasksController - CompleteTask (POST /api/points/tasks/{taskId}/complete)

    [Fact]
    public async Task CompleteTask_ShouldReturnCompleteResult()
    {
        SetupBuyerAuth();
        var resultDto = new TaskCompleteResultDto
        {
            UserTaskId = Guid.NewGuid(),
            TaskId = TaskId,
            UserId = UserId,
            PointsAwarded = 50,
            CompletedAt = DateTime.UtcNow
        };
        _taskAppServiceMock.Setup(s => s.CompleteTaskAsync(UserId, TaskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(resultDto);

        var response = await _client.PostAsync($"/api/points/tasks/{TaskId}/complete", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<TaskCompleteResultDto>>();
        result!.Data!.TaskId.Should().Be(TaskId);
        result.Data.UserId.Should().Be(UserId);
        result.Data.PointsAwarded.Should().Be(50);
    }

    [Fact]
    public async Task CompleteTask_ShouldCallServiceWithJwtUserIdAndRouteTaskId()
    {
        SetupBuyerAuth();
        var resultDto = new TaskCompleteResultDto
        {
            UserTaskId = Guid.NewGuid(),
            TaskId = TaskId,
            UserId = UserId,
            PointsAwarded = 10,
            CompletedAt = DateTime.UtcNow
        };
        _taskAppServiceMock.Setup(s => s.CompleteTaskAsync(UserId, TaskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(resultDto);

        await _client.PostAsync($"/api/points/tasks/{TaskId}/complete", null);

        _taskAppServiceMock.Verify(
            s => s.CompleteTaskAsync(UserId, TaskId, It.IsAny<CancellationToken>()),
            Times.Once,
            "UserId 从 JWT 注入，TaskId 从路由参数取");
    }

    #endregion

    #region Auth Helpers

    private void SetupBuyerAuth()
    {
        _currentUserMock.SetupGet(c => c.IsAuthenticated).Returns(true);
        _currentUserMock.SetupGet(c => c.UserId).Returns(UserId);
        _currentUserMock.SetupGet(c => c.Role).Returns("Buyer");
    }

    #endregion
}

/// <summary>
/// 测试鉴权处理器，模拟 JWT 鉴权。
/// 在所有 Points.Api.Tests 测试类之间共享，注入全部角色便于 RBAC 验证。
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

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "test"),
            new Claim(ClaimTypes.Role, "Buyer"),
            new Claim(ClaimTypes.Role, "Seller"),
            new Claim(ClaimTypes.Role, "Admin"),
            new Claim(ClaimTypes.Role, "Operator"),
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "Test");

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
