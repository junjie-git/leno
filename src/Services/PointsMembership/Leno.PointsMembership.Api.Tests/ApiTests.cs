using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Leno.Infrastructure.Auth;
using Leno.PointsMembership.Application;
using Leno.PointsMembership.Application.DTOs;
using Leno.PointsMembership.Domain.Exceptions;
using Leno.PointsMembership.Domain.ValueObjects;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Leno.PointsMembership.Api.Tests;

public class PointsMembershipApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly Mock<IPointsAppService> _pointsAppServiceMock = new();
    private readonly Mock<IMemberAppService> _memberAppServiceMock = new();
    private readonly Mock<IMembershipPackageAppService> _packageAppServiceMock = new();
    private readonly Mock<IPointsInternalAppService> _internalAppServiceMock = new();
    private readonly Mock<IPointsRuleAppService> _ruleAppServiceMock = new();
    private readonly Mock<ICurrentUserContext> _currentUserMock = new();

    private const string TestInternalKey = "test-internal-key-pm";

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid PackageId = Guid.NewGuid();
    private static readonly Guid LevelId = Guid.NewGuid();
    private static readonly Guid RuleId = Guid.NewGuid();

    public PointsMembershipApiTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Environment", "Development");

            builder.ConfigureServices(services =>
            {
                services.AddSingleton(_pointsAppServiceMock.Object);
                services.AddSingleton(_memberAppServiceMock.Object);
                services.AddSingleton(_packageAppServiceMock.Object);
                services.AddSingleton(_internalAppServiceMock.Object);
                services.AddSingleton(_ruleAppServiceMock.Object);
                services.AddSingleton(_currentUserMock.Object);

                services.Configure<InternalApiKeyOptions>(o =>
                {
                    o.ApiKey = TestInternalKey;
                    o.RoutePrefix = "internal/";
                });

                RemoveMassTransitServices(services);
                RemoveElasticsearchServices(services);

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

    #region Health

    [Fact]
    public async Task Health_Live_ShouldReturnOk()
    {
        var response = await _client.GetAsync("/health/live");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_ShouldReturnUnauthorized()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.GetAsync("/api/points/account");
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region PointsController - Buyer

    [Fact]
    public async Task CheckIn_ShouldReturnCheckInResult()
    {
        SetupBuyerAuth();
        var resultDto = new CheckInResultDto
        {
            RecordId = Guid.NewGuid(), UserId = UserId,
            CheckInDate = new DateOnly(2026, 7, 12), ContinuousDays = 3, PointsAwarded = 10
        };
        _pointsAppServiceMock.Setup(s => s.CheckInAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(resultDto);

        var response = await _client.PostAsync("/api/points/check-in", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<CheckInResultDto>>();
        result!.Data!.ContinuousDays.Should().Be(3);
        result.Data.PointsAwarded.Should().Be(10);
    }

    [Fact]
    public async Task GetAccount_ShouldReturnAccountDto()
    {
        SetupBuyerAuth();
        var dto = new PointsAccountDto
        {
            Id = Guid.NewGuid(), UserId = UserId, Balance = 500, TotalEarned = 1000
        };
        _pointsAppServiceMock.Setup(s => s.GetPointsAccountAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var response = await _client.GetAsync("/api/points/account");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PointsAccountDto>>();
        result!.Data!.Balance.Should().Be(500);
    }

    [Fact]
    public async Task GetLedger_ShouldReturnLedgerList()
    {
        SetupBuyerAuth();
        _pointsAppServiceMock.Setup(s => s.GetLedgerAsync(UserId, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var response = await _client.GetAsync("/api/points/ledger?page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion

    #region PointsController - Admin

    [Fact]
    public async Task AwardPoints_ShouldReturnSuccess()
    {
        SetupAdminAuth();
        _pointsAppServiceMock.Setup(s => s.AwardPointsAsync(It.IsAny<AwardPointsDto>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var body = new { userId = UserId, amount = 100, reason = "活动奖励" };
        var response = await _client.PostAsJsonAsync("/api/admin/points/award", body);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion

    #region MembersController - Buyer

    [Fact]
    public async Task GetMyMemberInfo_ShouldReturnMemberDto()
    {
        SetupBuyerAuth();
        var dto = new MemberDto
        {
            Id = Guid.NewGuid(), UserId = UserId, CurrentLevel = 2, TotalConsumption = 500m
        };
        _memberAppServiceMock.Setup(s => s.GetMemberInfoAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var response = await _client.GetAsync("/api/members/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<MemberDto>>();
        result!.Data!.CurrentLevel.Should().Be(2);
    }

    #endregion

    #region MembersController - Admin

    [Fact]
    public async Task GetLevels_ShouldReturnLevelList()
    {
        SetupAdminAuth();
        var levels = new List<MembershipLevelDto>
        {
            new() { Id = LevelId, Name = "金卡会员", Level = 3, MinConsumption = 1000m, DiscountRate = 0.95m }
        };
        _memberAppServiceMock.Setup(s => s.GetLevelsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(levels);

        var response = await _client.GetAsync("/api/admin/members/levels");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<MembershipLevelDto>>>();
        result!.Data.Should().HaveCount(1);
    }

    [Fact]
    public async Task CreateLevel_ShouldReturnCreatedLevel()
    {
        SetupAdminAuth();
        var dto = new MembershipLevelDto
        {
            Id = LevelId, Name = "钻石会员", Level = 4, MinConsumption = 2000m, DiscountRate = 0.9m
        };
        _memberAppServiceMock.Setup(s => s.CreateLevelAsync(It.IsAny<CreateMembershipLevelDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var body = new { name = "钻石会员", level = 4, minConsumption = 2000m, discountRate = 0.9m };
        var response = await _client.PostAsJsonAsync("/api/admin/members/levels", body);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<MembershipLevelDto>>();
        result!.Data!.Name.Should().Be("钻石会员");
    }

    [Fact]
    public async Task UpdateLevel_ShouldReturnUpdatedLevel()
    {
        SetupAdminAuth();
        var dto = new MembershipLevelDto
        {
            Id = LevelId, Name = "更新等级", Level = 3, MinConsumption = 1500m, DiscountRate = 0.92m
        };
        _memberAppServiceMock.Setup(s => s.UpdateLevelAsync(LevelId, It.IsAny<UpdateMembershipLevelDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var body = new { name = "更新等级", minConsumption = 1500m, discountRate = 0.92m };
        var response = await _client.PutAsJsonAsync($"/api/admin/members/levels/{LevelId}", body);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task EnableLevel_ShouldReturnSuccess()
    {
        SetupAdminAuth();
        _memberAppServiceMock.Setup(s => s.EnableLevelAsync(LevelId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await _client.PostAsync($"/api/admin/members/levels/{LevelId}/enable", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DisableLevel_ShouldReturnSuccess()
    {
        SetupAdminAuth();
        _memberAppServiceMock.Setup(s => s.DisableLevelAsync(LevelId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await _client.PostAsync($"/api/admin/members/levels/{LevelId}/disable", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion

    #region MembershipPackagesController - Buyer

    [Fact]
    public async Task GetPackages_ShouldReturnPackageList()
    {
        SetupBuyerAuth();
        var packages = new List<MembershipPackageDto>
        {
            new() { Id = PackageId, Name = "月度会员", Price = 29.9m, DurationDays = 30 }
        };
        _packageAppServiceMock.Setup(s => s.GetPackagesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(packages);

        var response = await _client.GetAsync("/api/membership-packages");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<MembershipPackageDto>>>();
        result!.Data.Should().HaveCount(1);
    }

    [Fact]
    public async Task Subscribe_ShouldReturnSuccess()
    {
        SetupBuyerAuth();
        _packageAppServiceMock.Setup(s => s.SubscribeAsync(UserId, PackageId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await _client.PostAsync($"/api/membership-packages/{PackageId}/subscribe", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion

    #region MembershipPackagesController - Admin

    [Fact]
    public async Task CreatePackage_ShouldReturnCreatedPackage()
    {
        SetupAdminAuth();
        var dto = new MembershipPackageDto
        {
            Id = PackageId, Name = "年度会员", Level = 3, Price = 299m, DurationDays = 365,
            Benefits = "[\"免运费\",\"专属折扣\"]"
        };
        _packageAppServiceMock.Setup(s => s.CreatePackageAsync(It.IsAny<CreateMembershipPackageDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var body = new
        {
            name = "年度会员", level = 3, price = 299m, durationDays = 365,
            benefits = "[\"免运费\",\"专属折扣\"]"
        };
        var response = await _client.PostAsJsonAsync("/api/admin/membership-packages", body);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<MembershipPackageDto>>();
        result!.Data!.Name.Should().Be("年度会员");
    }

    [Fact]
    public async Task UpdatePackage_ShouldReturnUpdatedPackage()
    {
        SetupAdminAuth();
        var dto = new MembershipPackageDto
        {
            Id = PackageId, Name = "更新套餐", Level = 2, Price = 39.9m, DurationDays = 30,
            Benefits = "[]"
        };
        _packageAppServiceMock.Setup(s => s.UpdatePackageAsync(PackageId, It.IsAny<UpdateMembershipPackageDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var body = new { name = "更新套餐", price = 39.9m, durationDays = 30, benefits = "[]" };
        var response = await _client.PutAsJsonAsync($"/api/admin/membership-packages/{PackageId}", body);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task EnablePackage_ShouldReturnSuccess()
    {
        SetupAdminAuth();
        _packageAppServiceMock.Setup(s => s.EnablePackageAsync(PackageId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await _client.PostAsync($"/api/admin/membership-packages/{PackageId}/enable", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DisablePackage_ShouldReturnSuccess()
    {
        SetupAdminAuth();
        _packageAppServiceMock.Setup(s => s.DisablePackageAsync(PackageId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await _client.PostAsync($"/api/admin/membership-packages/{PackageId}/disable", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion

    #region InternalPointsController

    [Fact]
    public async Task TrialOffset_ShouldReturnResult()
    {
        var resultDto = new TrialOffsetResultDto { OffsetAmount = 1.5m, Currency = "CNY" };
        _internalAppServiceMock.Setup(s => s.TrialOffsetAsync(It.IsAny<TrialOffsetDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(resultDto);

        var request = new HttpRequestMessage(HttpMethod.Post, "/internal/points/trial-offset")
        {
            Content = JsonContent.Create(new { userId = UserId, pointsToUse = 150 })
        };
        request.Headers.Add("X-Internal-Key", TestInternalKey);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<TrialOffsetResultDto>>();
        result!.Data!.OffsetAmount.Should().Be(1.5m);
    }

    [Fact]
    public async Task Freeze_ShouldReturnSuccess()
    {
        _internalAppServiceMock.Setup(s => s.FreezeAsync(It.IsAny<FreezePointsDto>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var request = new HttpRequestMessage(HttpMethod.Post, "/internal/points/freeze")
        {
            Content = JsonContent.Create(new { userId = UserId, orderId = Guid.NewGuid(), pointsToUse = 100 })
        };
        request.Headers.Add("X-Internal-Key", TestInternalKey);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Release_ShouldReturnSuccess()
    {
        _internalAppServiceMock.Setup(s => s.ReleaseAsync(It.IsAny<ReleasePointsDto>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var request = new HttpRequestMessage(HttpMethod.Post, "/internal/points/release")
        {
            Content = JsonContent.Create(new { orderId = Guid.NewGuid() })
        };
        request.Headers.Add("X-Internal-Key", TestInternalKey);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion

    #region PointsRulesController - Admin

    [Fact]
    public async Task GetRules_ShouldReturnRuleList()
    {
        SetupAdminAuth();
        var rules = new List<PointsRuleDto>
        {
            new()
            {
                Id = RuleId, Code = "DAILY_CHECK", Name = "每日签到",
                ActionType = PointsActionType.CheckIn, Points = 5, DailyLimit = 1,
                Status = PointsRuleStatus.Enabled, UpdatedAt = DateTime.UtcNow
            },
            new()
            {
                Id = Guid.NewGuid(), Code = "ORDER_DONE", Name = "下单得积分",
                ActionType = PointsActionType.Order, Points = 10, DailyLimit = 5,
                Status = PointsRuleStatus.Enabled, UpdatedAt = DateTime.UtcNow
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
    public async Task CreateRule_ShouldReturnCreatedRule()
    {
        SetupAdminAuth();
        var dto = new PointsRuleDto
        {
            Id = RuleId, Code = "DAILY_CHECK", Name = "每日签到",
            ActionType = PointsActionType.CheckIn, Points = 5, DailyLimit = 1,
            Status = PointsRuleStatus.Enabled, UpdatedAt = DateTime.UtcNow
        };
        _ruleAppServiceMock.Setup(s => s.CreateRuleAsync(It.IsAny<CreatePointsRuleDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var body = new
        {
            code = "DAILY_CHECK", name = "每日签到",
            actionType = (int)PointsActionType.CheckIn, points = 5, dailyLimit = 1
        };
        var response = await _client.PostAsJsonAsync("/api/admin/points/rules", body);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PointsRuleDto>>();
        result!.Data!.Code.Should().Be("DAILY_CHECK");
        result.Data.Points.Should().Be(5);
    }

    [Fact]
    public async Task CreateRule_DuplicateCode_ShouldReturnConflict()
    {
        SetupAdminAuth();
        _ruleAppServiceMock.Setup(s => s.CreateRuleAsync(It.IsAny<CreatePointsRuleDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new PointsDomainException("积分规则编码 DAILY_CHECK 已存在", "POINTS_RULE_CODE_EXISTS"));

        var body = new
        {
            code = "DAILY_CHECK", name = "每日签到",
            actionType = (int)PointsActionType.CheckIn, points = 5, dailyLimit = 1
        };
        var response = await _client.PostAsJsonAsync("/api/admin/points/rules", body);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse>();
        result!.Code.Should().Be(409);
    }

    [Fact]
    public async Task UpdateRule_ShouldReturnUpdatedRule()
    {
        SetupAdminAuth();
        var dto = new PointsRuleDto
        {
            Id = RuleId, Code = "DAILY_CHECK", Name = "每日签到（更新）",
            ActionType = PointsActionType.CheckIn, Points = 10, DailyLimit = 2,
            Status = PointsRuleStatus.Enabled, UpdatedAt = DateTime.UtcNow
        };
        _ruleAppServiceMock.Setup(s => s.UpdateRuleAsync(RuleId, It.IsAny<UpdatePointsRuleDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var body = new
        {
            name = "每日签到（更新）",
            actionType = (int)PointsActionType.CheckIn, points = 10, dailyLimit = 2
        };
        var response = await _client.PutAsJsonAsync($"/api/admin/points/rules/{RuleId}", body);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PointsRuleDto>>();
        result!.Data!.Name.Should().Be("每日签到（更新）");
        result.Data.Points.Should().Be(10);
        result.Data.Code.Should().Be("DAILY_CHECK");
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
            actionType = (int)PointsActionType.CheckIn, points = 5, dailyLimit = 1
        };
        var response = await _client.PutAsJsonAsync($"/api/admin/points/rules/{RuleId}", body);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse>();
        result!.Code.Should().Be(404);
    }

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

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse>();
        result!.Code.Should().Be(409);
    }

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

    [Fact]
    public async Task PointsRules_WithoutToken_ShouldReturnUnauthorized()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.GetAsync("/api/admin/points/rules");
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

    private void SetupBuyerAuth()
    {
        _currentUserMock.SetupGet(c => c.IsAuthenticated).Returns(true);
        _currentUserMock.SetupGet(c => c.UserId).Returns(UserId);
        _currentUserMock.SetupGet(c => c.Role).Returns("Buyer");
    }

    #endregion
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
            new Claim(ClaimTypes.Role, "Operator"),
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "Test");

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}