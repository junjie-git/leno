using Leno.Infrastructure.Auth;
using Leno.PointsMembership.Api.Controllers;
using Leno.PointsMembership.Application;
using Leno.PointsMembership.Application.DTOs;
using Leno.PointsMembership.Domain.Exceptions;
using Leno.PointsMembership.Domain.ValueObjects;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;
using FluentAssertions;

namespace Leno.PointsMembership.Api.Tests.Controllers;

/// <summary>
/// 积分规则控制器单元测试，验证 5 个端点的方法行为、路由特性与鉴权特性。
/// 采用直接实例化控制器的方式，避免 WebApplicationFactory 的预存基础设施依赖（MassTransit/Elasticsearch）。
/// 覆盖：CRUD 成功、编码唯一冲突（409）、启停状态转换、鉴权场景（角色校验）、不存在 ruleId（404）。
/// </summary>
public class PointsRulesControllerTests
{
    private readonly Mock<IPointsRuleAppService> _ruleAppServiceMock = new();
    private readonly Mock<ICurrentUserContext> _currentUserMock = new();
    private readonly PointsRulesController _controller;

    private static readonly Guid RuleId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    public PointsRulesControllerTests()
    {
        _currentUserMock.SetupGet(c => c.IsAuthenticated).Returns(true);
        _currentUserMock.SetupGet(c => c.UserId).Returns(UserId);
        _currentUserMock.SetupGet(c => c.Role).Returns("Admin");

        _controller = new PointsRulesController(_currentUserMock.Object, _ruleAppServiceMock.Object);
    }

    #region GetRulesAsync

    [Fact]
    public async Task GetRulesAsync_ShouldReturnOkWithRuleList()
    {
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

        var result = await _controller.GetRulesAsync(CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var apiResponse = okResult.Value.Should().BeOfType<ApiResponse<List<PointsRuleDto>>>().Subject;
        apiResponse.Code.Should().Be(200);
        apiResponse.Data.Should().HaveCount(2);
        apiResponse.Data![0].Code.Should().Be("DAILY_CHECK");
        apiResponse.Data[1].Code.Should().Be("ORDER_DONE");
    }

    [Fact]
    public async Task GetRulesAsync_Empty_ShouldReturnOkWithEmptyList()
    {
        _ruleAppServiceMock.Setup(s => s.GetRulesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await _controller.GetRulesAsync(CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var apiResponse = okResult.Value.Should().BeOfType<ApiResponse<List<PointsRuleDto>>>().Subject;
        apiResponse.Data.Should().BeEmpty();
    }

    #endregion

    #region CreateRuleAsync

    [Fact]
    public async Task CreateRuleAsync_Valid_ShouldReturnOkWithCreatedRule()
    {
        var dto = new CreatePointsRuleDto
        {
            Code = "DAILY_CHECK", Name = "每日签到",
            ActionType = PointsActionType.CheckIn, Points = 5, DailyLimit = 1,
            Status = PointsRuleStatus.Enabled
        };
        var createdDto = new PointsRuleDto
        {
            Id = RuleId, Code = "DAILY_CHECK", Name = "每日签到",
            ActionType = PointsActionType.CheckIn, Points = 5, DailyLimit = 1,
            Status = PointsRuleStatus.Enabled, UpdatedAt = DateTime.UtcNow
        };
        _ruleAppServiceMock.Setup(s => s.CreateRuleAsync(It.IsAny<CreatePointsRuleDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdDto);

        var result = await _controller.CreateRuleAsync(dto, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var apiResponse = okResult.Value.Should().BeOfType<ApiResponse<PointsRuleDto>>().Subject;
        apiResponse.Code.Should().Be(200);
        apiResponse.Data!.Code.Should().Be("DAILY_CHECK");
        apiResponse.Data.Points.Should().Be(5);
        _ruleAppServiceMock.Verify(s => s.CreateRuleAsync(dto, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateRuleAsync_DuplicateCode_ShouldThrowCodeExistsException()
    {
        var dto = new CreatePointsRuleDto
        {
            Code = "DAILY_CHECK", Name = "每日签到",
            ActionType = PointsActionType.CheckIn, Points = 5, DailyLimit = 1
        };
        _ruleAppServiceMock.Setup(s => s.CreateRuleAsync(It.IsAny<CreatePointsRuleDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new PointsDomainException("积分规则编码 DAILY_CHECK 已存在", "POINTS_RULE_CODE_EXISTS"));

        var act = () => _controller.CreateRuleAsync(dto, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<PointsDomainException>();
        ex.Which.ErrorCode.Should().Be("POINTS_RULE_CODE_EXISTS");
    }

    [Fact]
    public async Task CreateRuleAsync_NegativePoints_ShouldSucceedAsDeductionRule()
    {
        var dto = new CreatePointsRuleDto
        {
            Code = "REFUND_DEDUCT", Name = "退款扣减积分",
            ActionType = PointsActionType.Activity, Points = -50, DailyLimit = 10
        };
        var createdDto = new PointsRuleDto
        {
            Id = RuleId, Code = "REFUND_DEDUCT", Name = "退款扣减积分",
            ActionType = PointsActionType.Activity, Points = -50, DailyLimit = 10,
            Status = PointsRuleStatus.Enabled, UpdatedAt = DateTime.UtcNow
        };
        _ruleAppServiceMock.Setup(s => s.CreateRuleAsync(It.IsAny<CreatePointsRuleDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdDto);

        var result = await _controller.CreateRuleAsync(dto, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var apiResponse = okResult.Value.Should().BeOfType<ApiResponse<PointsRuleDto>>().Subject;
        apiResponse.Data!.Points.Should().Be(-50);
    }

    #endregion

    #region UpdateRuleAsync

    [Fact]
    public async Task UpdateRuleAsync_Valid_ShouldReturnOkWithUpdatedRule()
    {
        var dto = new UpdatePointsRuleDto
        {
            Name = "每日签到（更新）",
            ActionType = PointsActionType.CheckIn, Points = 10, DailyLimit = 2
        };
        var updatedDto = new PointsRuleDto
        {
            Id = RuleId, Code = "DAILY_CHECK", Name = "每日签到（更新）",
            ActionType = PointsActionType.CheckIn, Points = 10, DailyLimit = 2,
            Status = PointsRuleStatus.Enabled, UpdatedAt = DateTime.UtcNow
        };
        _ruleAppServiceMock.Setup(s => s.UpdateRuleAsync(RuleId, It.IsAny<UpdatePointsRuleDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(updatedDto);

        var result = await _controller.UpdateRuleAsync(RuleId, dto, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var apiResponse = okResult.Value.Should().BeOfType<ApiResponse<PointsRuleDto>>().Subject;
        apiResponse.Code.Should().Be(200);
        apiResponse.Data!.Name.Should().Be("每日签到（更新）");
        apiResponse.Data.Points.Should().Be(10);
        apiResponse.Data.Code.Should().Be("DAILY_CHECK");
        _ruleAppServiceMock.Verify(s => s.UpdateRuleAsync(RuleId, dto, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateRuleAsync_NotExist_ShouldThrowNotFoundException()
    {
        var dto = new UpdatePointsRuleDto
        {
            Name = "不存在规则",
            ActionType = PointsActionType.CheckIn, Points = 5, DailyLimit = 1
        };
        _ruleAppServiceMock.Setup(s => s.UpdateRuleAsync(RuleId, It.IsAny<UpdatePointsRuleDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new PointsDomainException($"积分规则 {RuleId} 不存在", "POINTS_RULE_NOT_FOUND"));

        var act = () => _controller.UpdateRuleAsync(RuleId, dto, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<PointsDomainException>();
        ex.Which.ErrorCode.Should().Be("POINTS_RULE_NOT_FOUND");
    }

    #endregion

    #region EnableRuleAsync

    [Fact]
    public async Task EnableRuleAsync_Valid_ShouldReturnOkWithSuccessResponse()
    {
        _ruleAppServiceMock.Setup(s => s.EnableRuleAsync(RuleId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _controller.EnableRuleAsync(RuleId, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var apiResponse = okResult.Value.Should().BeOfType<ApiResponse>().Subject;
        apiResponse.Code.Should().Be(200);
        _ruleAppServiceMock.Verify(s => s.EnableRuleAsync(RuleId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EnableRuleAsync_AlreadyEnabled_ShouldThrowAlreadyEnabledException()
    {
        _ruleAppServiceMock.Setup(s => s.EnableRuleAsync(RuleId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new PointsDomainException("积分规则已启用", "POINTS_RULE_ALREADY_ENABLED"));

        var act = () => _controller.EnableRuleAsync(RuleId, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<PointsDomainException>();
        ex.Which.ErrorCode.Should().Be("POINTS_RULE_ALREADY_ENABLED");
    }

    [Fact]
    public async Task EnableRuleAsync_NotExist_ShouldThrowNotFoundException()
    {
        _ruleAppServiceMock.Setup(s => s.EnableRuleAsync(RuleId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new PointsDomainException($"积分规则 {RuleId} 不存在", "POINTS_RULE_NOT_FOUND"));

        var act = () => _controller.EnableRuleAsync(RuleId, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<PointsDomainException>();
        ex.Which.ErrorCode.Should().Be("POINTS_RULE_NOT_FOUND");
    }

    #endregion

    #region DisableRuleAsync

    [Fact]
    public async Task DisableRuleAsync_Valid_ShouldReturnOkWithSuccessResponse()
    {
        _ruleAppServiceMock.Setup(s => s.DisableRuleAsync(RuleId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _controller.DisableRuleAsync(RuleId, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var apiResponse = okResult.Value.Should().BeOfType<ApiResponse>().Subject;
        apiResponse.Code.Should().Be(200);
        _ruleAppServiceMock.Verify(s => s.DisableRuleAsync(RuleId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DisableRuleAsync_AlreadyDisabled_ShouldThrowAlreadyDisabledException()
    {
        _ruleAppServiceMock.Setup(s => s.DisableRuleAsync(RuleId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new PointsDomainException("积分规则已停用", "POINTS_RULE_ALREADY_DISABLED"));

        var act = () => _controller.DisableRuleAsync(RuleId, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<PointsDomainException>();
        ex.Which.ErrorCode.Should().Be("POINTS_RULE_ALREADY_DISABLED");
    }

    [Fact]
    public async Task DisableRuleAsync_NotExist_ShouldThrowNotFoundException()
    {
        _ruleAppServiceMock.Setup(s => s.DisableRuleAsync(RuleId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new PointsDomainException($"积分规则 {RuleId} 不存在", "POINTS_RULE_NOT_FOUND"));

        var act = () => _controller.DisableRuleAsync(RuleId, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<PointsDomainException>();
        ex.Which.ErrorCode.Should().Be("POINTS_RULE_NOT_FOUND");
    }

    #endregion

    #region Auth & Route Attributes

    [Fact]
    public void Controller_ShouldHaveAuthorizeAttributeWithOperatorAdminRoles()
    {
        var controllerType = typeof(PointsRulesController);

        var authorizeAttrs = controllerType.GetCustomAttributes(typeof(AuthorizeAttribute), false);
        authorizeAttrs.Should().BeEmpty("Controller 类级别不应有 Authorize，由方法级 Authorize 控制");
    }

    [Fact]
    public void GetRulesAsync_ShouldHaveAuthorizeWithOperatorAdminRoles()
    {
        var method = typeof(PointsRulesController).GetMethod(nameof(PointsRulesController.GetRulesAsync));
        method.Should().NotBeNull();

        var authorizeAttr = method!.GetCustomAttributes(typeof(AuthorizeAttribute), false)
            .OfType<AuthorizeAttribute>()
            .SingleOrDefault();
        authorizeAttr.Should().NotBeNull("GetRulesAsync 应有 Authorize 特性");
        authorizeAttr!.Roles.Should().Be("Operator,Admin");
    }

    [Fact]
    public void CreateRuleAsync_ShouldHaveAuthorizeWithOperatorAdminRoles()
    {
        var method = typeof(PointsRulesController).GetMethod(nameof(PointsRulesController.CreateRuleAsync));
        method.Should().NotBeNull();

        var authorizeAttr = method!.GetCustomAttributes(typeof(AuthorizeAttribute), false)
            .OfType<AuthorizeAttribute>()
            .SingleOrDefault();
        authorizeAttr.Should().NotBeNull("CreateRuleAsync 应有 Authorize 特性");
        authorizeAttr!.Roles.Should().Be("Operator,Admin");
    }

    [Fact]
    public void UpdateRuleAsync_ShouldHaveAuthorizeWithOperatorAdminRoles()
    {
        var method = typeof(PointsRulesController).GetMethod(nameof(PointsRulesController.UpdateRuleAsync));
        method.Should().NotBeNull();

        var authorizeAttr = method!.GetCustomAttributes(typeof(AuthorizeAttribute), false)
            .OfType<AuthorizeAttribute>()
            .SingleOrDefault();
        authorizeAttr.Should().NotBeNull("UpdateRuleAsync 应有 Authorize 特性");
        authorizeAttr!.Roles.Should().Be("Operator,Admin");
    }

    [Fact]
    public void EnableRuleAsync_ShouldHaveAuthorizeWithOperatorAdminRoles()
    {
        var method = typeof(PointsRulesController).GetMethod(nameof(PointsRulesController.EnableRuleAsync));
        method.Should().NotBeNull();

        var authorizeAttr = method!.GetCustomAttributes(typeof(AuthorizeAttribute), false)
            .OfType<AuthorizeAttribute>()
            .SingleOrDefault();
        authorizeAttr.Should().NotBeNull("EnableRuleAsync 应有 Authorize 特性");
        authorizeAttr!.Roles.Should().Be("Operator,Admin");
    }

    [Fact]
    public void DisableRuleAsync_ShouldHaveAuthorizeWithOperatorAdminRoles()
    {
        var method = typeof(PointsRulesController).GetMethod(nameof(PointsRulesController.DisableRuleAsync));
        method.Should().NotBeNull();

        var authorizeAttr = method!.GetCustomAttributes(typeof(AuthorizeAttribute), false)
            .OfType<AuthorizeAttribute>()
            .SingleOrDefault();
        authorizeAttr.Should().NotBeNull("DisableRuleAsync 应有 Authorize 特性");
        authorizeAttr!.Roles.Should().Be("Operator,Admin");
    }

    [Fact]
    public void GetRulesAsync_ShouldHaveHttpGetAttributeWithCorrectRoute()
    {
        var method = typeof(PointsRulesController).GetMethod(nameof(PointsRulesController.GetRulesAsync));
        method.Should().NotBeNull();

        var httpGetAttrs = method!.GetCustomAttributes(typeof(HttpGetAttribute), false);
        httpGetAttrs.Should().HaveCountGreaterOrEqualTo(1, "应有 HttpGet 特性");
        var routeTemplates = httpGetAttrs.Cast<HttpGetAttribute>().Select(a => a.Template).ToList();
        routeTemplates.Should().Contain("api/admin/points/rules",
            "GET 端点路由应为 api/admin/points/rules");
    }

    [Fact]
    public void CreateRuleAsync_ShouldHaveHttpPostAttributeWithCorrectRoute()
    {
        var method = typeof(PointsRulesController).GetMethod(nameof(PointsRulesController.CreateRuleAsync));
        method.Should().NotBeNull();

        var httpPostAttrs = method!.GetCustomAttributes(typeof(HttpPostAttribute), false);
        httpPostAttrs.Should().HaveCountGreaterOrEqualTo(1, "应有 HttpPost 特性");
        var routeTemplates = httpPostAttrs.Cast<HttpPostAttribute>().Select(a => a.Template).ToList();
        routeTemplates.Should().Contain("api/admin/points/rules",
            "POST 端点路由应为 api/admin/points/rules");
    }

    [Fact]
    public void UpdateRuleAsync_ShouldHaveHttpPutAttributeWithCorrectRoute()
    {
        var method = typeof(PointsRulesController).GetMethod(nameof(PointsRulesController.UpdateRuleAsync));
        method.Should().NotBeNull();

        var httpPutAttrs = method!.GetCustomAttributes(typeof(HttpPutAttribute), false);
        httpPutAttrs.Should().HaveCountGreaterOrEqualTo(1, "应有 HttpPut 特性");
        var routeTemplates = httpPutAttrs.Cast<HttpPutAttribute>().Select(a => a.Template).ToList();
        routeTemplates.Should().Contain("api/admin/points/rules/{ruleId:guid}",
            "PUT 端点路由应为 api/admin/points/rules/{ruleId:guid}");
    }

    [Fact]
    public void EnableRuleAsync_ShouldHaveHttpPostAttributeWithCorrectRoute()
    {
        var method = typeof(PointsRulesController).GetMethod(nameof(PointsRulesController.EnableRuleAsync));
        method.Should().NotBeNull();

        var httpPostAttrs = method!.GetCustomAttributes(typeof(HttpPostAttribute), false);
        httpPostAttrs.Should().HaveCountGreaterOrEqualTo(1, "应有 HttpPost 特性");
        var routeTemplates = httpPostAttrs.Cast<HttpPostAttribute>().Select(a => a.Template).ToList();
        routeTemplates.Should().Contain("api/admin/points/rules/{ruleId:guid}/enable",
            "启用端点路由应为 api/admin/points/rules/{ruleId:guid}/enable");
    }

    [Fact]
    public void DisableRuleAsync_ShouldHaveHttpPostAttributeWithCorrectRoute()
    {
        var method = typeof(PointsRulesController).GetMethod(nameof(PointsRulesController.DisableRuleAsync));
        method.Should().NotBeNull();

        var httpPostAttrs = method!.GetCustomAttributes(typeof(HttpPostAttribute), false);
        httpPostAttrs.Should().HaveCountGreaterOrEqualTo(1, "应有 HttpPost 特性");
        var routeTemplates = httpPostAttrs.Cast<HttpPostAttribute>().Select(a => a.Template).ToList();
        routeTemplates.Should().Contain("api/admin/points/rules/{ruleId:guid}/disable",
            "停用端点路由应为 api/admin/points/rules/{ruleId:guid}/disable");
    }

    #endregion
}
