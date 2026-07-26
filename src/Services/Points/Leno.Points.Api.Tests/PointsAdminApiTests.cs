using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Leno.Infrastructure.Auth;
using Leno.Points.Application;
using Leno.Points.Application.DTOs;
using Leno.Points.Domain.Exceptions;
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
/// 积分域运营端 API 集成测试。
/// 覆盖 PointsRulesController（GET/POST/PUT/enable/disable 5 端点）与 AdminPointsController（award 1 端点）。
/// 验证 Operator/Admin RBAC 鉴权、ApiResponse 包装、领域异常映射 409/404。
/// </summary>
public class PointsAdminApiTests : IClassFixture<WebApplicationFactory<Program>>
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

    private const string TestInternalKey = "test-internal-key-points-admin";

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid RuleId = Guid.NewGuid();

    public PointsAdminApiTests(WebApplicationFactory<Program> factory)
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

    #region PointsRulesController - GetRules (GET /api/admin/points/rules)

    [Fact]
    public async Task GetRules_AsAdmin_ShouldReturnRuleList()
    {
        SetupAdminAuth();
        var rules = new List<PointsRuleDto>
        {
            new()
            {
                Id = RuleId,
                Code = "DAILY_CHECK",
                Name = "每日签到",
                ActionType = PointsActionType.CheckIn,
                Points = 5,
                DailyLimit = 1,
                Status = PointsRuleStatus.Enabled,
                UpdatedAt = DateTime.UtcNow
            },
            new()
            {
                Id = Guid.NewGuid(),
                Code = "ORDER_DONE",
                Name = "下单得积分",
                ActionType = PointsActionType.Order,
                Points = 10,
                DailyLimit = 5,
                Status = PointsRuleStatus.Enabled,
                UpdatedAt = DateTime.UtcNow
            }
        };
        _ruleAppServiceMock.Setup(s => s.GetRulesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(rules);

        var response = await _client.GetAsync("/api/admin/points/rules");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<PointsRuleDto>>>();
        result!.Data.Should().HaveCount(2);
        result.Data![0].Code.Should().Be("DAILY_CHECK");
        result.Data[1].Code.Should().Be("ORDER_DONE");
    }

    [Fact]
    public async Task GetRules_AsOperator_ShouldReturnRuleList()
    {
        SetupOperatorAuth();
        _ruleAppServiceMock.Setup(s => s.GetRulesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var response = await _client.GetAsync("/api/admin/points/rules");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetRules_WithoutToken_ShouldReturnUnauthorized()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.GetAsync("/api/admin/points/rules");
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region PointsRulesController - CreateRule (POST /api/admin/points/rules)

    [Fact]
    public async Task CreateRule_ShouldReturnCreatedRule()
    {
        SetupAdminAuth();
        var dto = new PointsRuleDto
        {
            Id = RuleId,
            Code = "DAILY_CHECK",
            Name = "每日签到",
            ActionType = PointsActionType.CheckIn,
            Points = 5,
            DailyLimit = 1,
            Status = PointsRuleStatus.Enabled,
            UpdatedAt = DateTime.UtcNow
        };
        _ruleAppServiceMock.Setup(s => s.CreateRuleAsync(It.IsAny<CreatePointsRuleDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var body = new
        {
            code = "DAILY_CHECK",
            name = "每日签到",
            actionType = (int)PointsActionType.CheckIn,
            points = 5,
            dailyLimit = 1
        };
        var response = await _client.PostAsJsonAsync("/api/admin/points/rules", body);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PointsRuleDto>>();
        result!.Data!.Code.Should().Be("DAILY_CHECK");
        result.Data.Points.Should().Be(5);
        result.Data.DailyLimit.Should().Be(1);
    }

    [Fact]
    public async Task CreateRule_DuplicateCode_ShouldReturnConflict()
    {
        SetupAdminAuth();
        _ruleAppServiceMock.Setup(s => s.CreateRuleAsync(It.IsAny<CreatePointsRuleDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new PointsDomainException("积分规则编码 DAILY_CHECK 已存在", "POINTS_RULE_CODE_EXISTS"));

        var body = new
        {
            code = "DAILY_CHECK",
            name = "每日签到",
            actionType = (int)PointsActionType.CheckIn,
            points = 5,
            dailyLimit = 1
        };
        var response = await _client.PostAsJsonAsync("/api/admin/points/rules", body);

        // POINTS_RULE_CODE_EXISTS 后缀匹配 _EXISTS → 409
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse>();
        result!.Code.Should().Be(409);
    }

    [Fact]
    public async Task CreateRule_WithNegativePoints_ShouldSupportDeductRule()
    {
        SetupAdminAuth();
        var dto = new PointsRuleDto
        {
            Id = RuleId,
            Code = "REFUND_DEDUCT",
            Name = "退款扣减",
            ActionType = PointsActionType.Order,
            Points = -20,
            DailyLimit = 5,
            Status = PointsRuleStatus.Enabled,
            UpdatedAt = DateTime.UtcNow
        };
        _ruleAppServiceMock.Setup(s => s.CreateRuleAsync(It.IsAny<CreatePointsRuleDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var body = new
        {
            code = "REFUND_DEDUCT",
            name = "退款扣减",
            actionType = (int)PointsActionType.Order,
            points = -20,
            dailyLimit = 5
        };
        var response = await _client.PostAsJsonAsync("/api/admin/points/rules", body);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PointsRuleDto>>();
        result!.Data!.Points.Should().Be(-20, "支持正负积分值（发放/扣减）");
    }

    #endregion

    #region PointsRulesController - UpdateRule (PUT /api/admin/points/rules/{ruleId})

    [Fact]
    public async Task UpdateRule_ShouldReturnUpdatedRule()
    {
        SetupAdminAuth();
        var dto = new PointsRuleDto
        {
            Id = RuleId,
            Code = "DAILY_CHECK",
            Name = "每日签到（更新）",
            ActionType = PointsActionType.CheckIn,
            Points = 10,
            DailyLimit = 2,
            Status = PointsRuleStatus.Enabled,
            UpdatedAt = DateTime.UtcNow
        };
        _ruleAppServiceMock.Setup(s => s.UpdateRuleAsync(RuleId, It.IsAny<UpdatePointsRuleDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var body = new
        {
            name = "每日签到（更新）",
            actionType = (int)PointsActionType.CheckIn,
            points = 10,
            dailyLimit = 2
        };
        var response = await _client.PutAsJsonAsync($"/api/admin/points/rules/{RuleId}", body);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PointsRuleDto>>();
        result!.Data!.Name.Should().Be("每日签到（更新）");
        result.Data.Points.Should().Be(10);
        result.Data.Code.Should().Be("DAILY_CHECK", "编码不可改");
    }

    [Fact]
    public async Task UpdateRule_NotExist_ShouldReturnNotFound()
    {
        SetupAdminAuth();
        _ruleAppServiceMock.Setup(s => s.UpdateRuleAsync(RuleId, It.IsAny<UpdatePointsRuleDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new PointsDomainException($"积分规则 {RuleId} 不存在", "POINTS_RULE_NOT_FOUND"));

        var body = new
        {
            name = "不存在规则",
            actionType = (int)PointsActionType.CheckIn,
            points = 5,
            dailyLimit = 1
        };
        var response = await _client.PutAsJsonAsync($"/api/admin/points/rules/{RuleId}", body);

        // POINTS_RULE_NOT_FOUND 后缀匹配 _NOT_FOUND → 404
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse>();
        result!.Code.Should().Be(404);
    }

    [Fact]
    public async Task UpdateRule_ShouldCallServiceWithRouteRuleId()
    {
        SetupAdminAuth();
        var dto = new PointsRuleDto
        {
            Id = RuleId,
            Code = "DAILY_CHECK",
            Name = "更新",
            ActionType = PointsActionType.CheckIn,
            Points = 15,
            DailyLimit = 3,
            Status = PointsRuleStatus.Enabled,
            UpdatedAt = DateTime.UtcNow
        };
        _ruleAppServiceMock.Setup(s => s.UpdateRuleAsync(RuleId, It.IsAny<UpdatePointsRuleDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var body = new
        {
            name = "更新",
            actionType = (int)PointsActionType.CheckIn,
            points = 15,
            dailyLimit = 3
        };
        await _client.PutAsJsonAsync($"/api/admin/points/rules/{RuleId}", body);

        _ruleAppServiceMock.Verify(
            s => s.UpdateRuleAsync(RuleId, It.IsAny<UpdatePointsRuleDto>(), It.IsAny<CancellationToken>()),
            Times.Once,
            "ruleId 必须从路由参数取");
    }

    #endregion

    #region PointsRulesController - EnableRule (POST /api/admin/points/rules/{ruleId}/enable)

    [Fact]
    public async Task EnableRule_ShouldReturnSuccess()
    {
        SetupAdminAuth();
        _ruleAppServiceMock.Setup(s => s.EnableRuleAsync(RuleId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await _client.PostAsync($"/api/admin/points/rules/{RuleId}/enable", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse>();
        result!.Code.Should().Be(200);
    }

    [Fact]
    public async Task EnableRule_AlreadyEnabled_ShouldReturnConflict()
    {
        SetupAdminAuth();
        _ruleAppServiceMock.Setup(s => s.EnableRuleAsync(RuleId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new PointsDomainException("积分规则已启用", "POINTS_RULE_ALREADY_ENABLED"));

        var response = await _client.PostAsync($"/api/admin/points/rules/{RuleId}/enable", null);

        // POINTS_RULE_ALREADY_ENABLED 中间标记 _ALREADY_ → 409
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse>();
        result!.Code.Should().Be(409);
    }

    [Fact]
    public async Task EnableRule_ShouldCallServiceWithRouteRuleId()
    {
        SetupAdminAuth();
        _ruleAppServiceMock.Setup(s => s.EnableRuleAsync(RuleId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _client.PostAsync($"/api/admin/points/rules/{RuleId}/enable", null);

        _ruleAppServiceMock.Verify(
            s => s.EnableRuleAsync(RuleId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region PointsRulesController - DisableRule (POST /api/admin/points/rules/{ruleId}/disable)

    [Fact]
    public async Task DisableRule_ShouldReturnSuccess()
    {
        SetupAdminAuth();
        _ruleAppServiceMock.Setup(s => s.DisableRuleAsync(RuleId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await _client.PostAsync($"/api/admin/points/rules/{RuleId}/disable", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse>();
        result!.Code.Should().Be(200);
    }

    [Fact]
    public async Task DisableRule_AlreadyDisabled_ShouldReturnConflict()
    {
        SetupAdminAuth();
        _ruleAppServiceMock.Setup(s => s.DisableRuleAsync(RuleId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new PointsDomainException("积分规则已停用", "POINTS_RULE_ALREADY_DISABLED"));

        var response = await _client.PostAsync($"/api/admin/points/rules/{RuleId}/disable", null);

        // POINTS_RULE_ALREADY_DISABLED 中间标记 _ALREADY_ → 409
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse>();
        result!.Code.Should().Be(409);
    }

    [Fact]
    public async Task DisableRule_NotExist_ShouldReturnNotFound()
    {
        SetupAdminAuth();
        _ruleAppServiceMock.Setup(s => s.DisableRuleAsync(RuleId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new PointsDomainException($"积分规则 {RuleId} 不存在", "POINTS_RULE_NOT_FOUND"));

        var response = await _client.PostAsync($"/api/admin/points/rules/{RuleId}/disable", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse>();
        result!.Code.Should().Be(404);
    }

    #endregion

    #region AdminPointsController - Award (POST /api/admin/points/award)

    [Fact]
    public async Task Award_AsAdmin_ShouldReturnAwardResult()
    {
        SetupAdminAuth();
        var resultDto = new AwardResultDto
        {
            AccountId = Guid.NewGuid(),
            UserId = UserId,
            Amount = 100,
            AvailableBalanceAfter = 600,
            TotalEarnedAfter = 1100
        };
        _awardAppServiceMock.Setup(s => s.AwardAsync(UserId, 100, "活动奖励", It.IsAny<CancellationToken>()))
            .ReturnsAsync(resultDto);

        var body = new { userId = UserId, amount = 100, reason = "活动奖励" };
        var response = await _client.PostAsJsonAsync("/api/admin/points/award", body);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<AwardResultDto>>();
        result!.Data!.UserId.Should().Be(UserId);
        result.Data.Amount.Should().Be(100);
        result.Data.AvailableBalanceAfter.Should().Be(600);
        result.Data.TotalEarnedAfter.Should().Be(1100);
    }

    [Fact]
    public async Task Award_AsOperator_ShouldReturnAwardResult()
    {
        SetupOperatorAuth();
        var resultDto = new AwardResultDto
        {
            AccountId = Guid.NewGuid(),
            UserId = UserId,
            Amount = 50,
            AvailableBalanceAfter = 550,
            TotalEarnedAfter = 1050
        };
        _awardAppServiceMock.Setup(s => s.AwardAsync(UserId, 50, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(resultDto);

        var body = new { userId = UserId, amount = 50, reason = "运营发放" };
        var response = await _client.PostAsJsonAsync("/api/admin/points/award", body);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Award_ShouldCallServiceWithDtoValues()
    {
        SetupAdminAuth();
        var resultDto = new AwardResultDto
        {
            AccountId = Guid.NewGuid(),
            UserId = UserId,
            Amount = 200,
            AvailableBalanceAfter = 700,
            TotalEarnedAfter = 1200
        };
        _awardAppServiceMock.Setup(s => s.AwardAsync(UserId, 200, "补偿", It.IsAny<CancellationToken>()))
            .ReturnsAsync(resultDto);

        var body = new { userId = UserId, amount = 200, reason = "补偿" };
        await _client.PostAsJsonAsync("/api/admin/points/award", body);

        _awardAppServiceMock.Verify(
            s => s.AwardAsync(UserId, 200, "补偿", It.IsAny<CancellationToken>()),
            Times.Once,
            "UserId、Amount、Reason 必须从请求体 DTO 传入");
    }

    [Fact]
    public async Task Award_WithoutToken_ShouldReturnUnauthorized()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var body = new { userId = UserId, amount = 100, reason = "活动奖励" };
        var response = await _client.PostAsJsonAsync("/api/admin/points/award", body);
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Auth Helpers

    private void SetupAdminAuth()
    {
        _currentUserMock.SetupGet(c => c.IsAuthenticated).Returns(true);
        _currentUserMock.SetupGet(c => c.UserId).Returns(UserId);
        _currentUserMock.SetupGet(c => c.Role).Returns("Admin");
    }

    private void SetupOperatorAuth()
    {
        _currentUserMock.SetupGet(c => c.IsAuthenticated).Returns(true);
        _currentUserMock.SetupGet(c => c.UserId).Returns(UserId);
        _currentUserMock.SetupGet(c => c.Role).Returns("Operator");
    }

    #endregion
}
