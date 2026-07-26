using Leno.PointsMembership.Application.DTOs;
using Leno.PointsMembership.Application.Services;
using Leno.PointsMembership.Domain.Aggregates;
using Leno.PointsMembership.Domain.Exceptions;
using Leno.PointsMembership.Domain.Repositories;
using Leno.PointsMembership.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using Moq;
using PointsRuleAggregate = Leno.PointsMembership.Domain.Aggregates.PointsRule;

namespace Leno.PointsMembership.Application.Tests;

/// <summary>
/// 积分规则管理应用服务单元测试，覆盖 CRUD 成功、编码唯一约束冲突、启停状态转换、不存在 ruleId 场景。
/// </summary>
public class PointsRuleAppServiceTests
{
    private readonly Mock<IPointsRuleRepository> _ruleRepoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly PointsRuleAppService _sut;

    private static readonly Guid RuleId = Guid.NewGuid();

    public PointsRuleAppServiceTests()
    {
        _sut = new PointsRuleAppService(_ruleRepoMock.Object, _uowMock.Object);
    }

    #region GetRulesAsync

    [Fact]
    public async Task GetRulesAsync_ShouldReturnAllRules()
    {
        var rules = new List<PointsRuleAggregate>
        {
            PointsRuleAggregate.Create(Guid.NewGuid(), "DAILY_CHECK", "每日签到", PointsActionType.CheckIn, 5, 1),
            PointsRuleAggregate.Create(Guid.NewGuid(), "ORDER_DONE", "下单得积分", PointsActionType.Order, 10, 5),
            PointsRuleAggregate.Create(Guid.NewGuid(), "REVIEW", "评价得积分", PointsActionType.Review, 20, 3)
        };
        _ruleRepoMock.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(rules);

        var result = await _sut.GetRulesAsync();

        result.Should().HaveCount(3);
        result[0].Code.Should().Be("DAILY_CHECK");
        result[1].Code.Should().Be("ORDER_DONE");
        result[2].Code.Should().Be("REVIEW");
    }

    [Fact]
    public async Task GetRulesAsync_Empty_ShouldReturnEmptyList()
    {
        _ruleRepoMock.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await _sut.GetRulesAsync();

        result.Should().BeEmpty();
    }

    #endregion

    #region CreateRuleAsync

    [Fact]
    public async Task CreateRuleAsync_Valid_ShouldAddAndSave()
    {
        var dto = new CreatePointsRuleDto
        {
            Code = "DAILY_CHECK",
            Name = "每日签到",
            ActionType = PointsActionType.CheckIn,
            Points = 5,
            DailyLimit = 1,
            Status = PointsRuleStatus.Enabled
        };
        _ruleRepoMock.Setup(r => r.GetByCodeAsync(dto.Code, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PointsRuleAggregate?)null);

        var result = await _sut.CreateRuleAsync(dto);

        result.Should().NotBeNull();
        result.Code.Should().Be("DAILY_CHECK");
        result.Name.Should().Be("每日签到");
        result.ActionType.Should().Be(PointsActionType.CheckIn);
        result.Points.Should().Be(5);
        result.DailyLimit.Should().Be(1);
        result.Status.Should().Be(PointsRuleStatus.Enabled);
        _ruleRepoMock.Verify(r => r.AddAsync(It.IsAny<PointsRuleAggregate>(), It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateRuleAsync_DuplicateCode_ShouldThrowCodeExistsException()
    {
        var existing = PointsRuleAggregate.Create(RuleId, "DAILY_CHECK", "每日签到", PointsActionType.CheckIn, 5, 1);
        var dto = new CreatePointsRuleDto
        {
            Code = "DAILY_CHECK",
            Name = "每日签到重复",
            ActionType = PointsActionType.CheckIn,
            Points = 10,
            DailyLimit = 2
        };
        _ruleRepoMock.Setup(r => r.GetByCodeAsync(dto.Code, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var act = () => _sut.CreateRuleAsync(dto);

        var ex = await act.Should().ThrowAsync<PointsDomainException>();
        ex.Which.ErrorCode.Should().Be("POINTS_RULE_CODE_EXISTS");
        _ruleRepoMock.Verify(r => r.AddAsync(It.IsAny<PointsRuleAggregate>(), It.IsAny<CancellationToken>()), Times.Never);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateRuleAsync_NegativePoints_ShouldSucceedAsDeductionRule()
    {
        var dto = new CreatePointsRuleDto
        {
            Code = "REFUND_DEDUCT",
            Name = "退款扣减积分",
            ActionType = PointsActionType.Activity,
            Points = -50,
            DailyLimit = 10,
            Status = PointsRuleStatus.Enabled
        };
        _ruleRepoMock.Setup(r => r.GetByCodeAsync(dto.Code, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PointsRuleAggregate?)null);

        var result = await _sut.CreateRuleAsync(dto);

        result.Points.Should().Be(-50);
        result.Code.Should().Be("REFUND_DEDUCT");
        _ruleRepoMock.Verify(r => r.AddAsync(It.IsAny<PointsRuleAggregate>(), It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateRuleAsync_WithDisabledStatus_ShouldCreateAsDisabled()
    {
        var dto = new CreatePointsRuleDto
        {
            Code = "BROWSE_AWARD",
            Name = "浏览奖励",
            ActionType = PointsActionType.Browse,
            Points = 2,
            DailyLimit = 10,
            Status = PointsRuleStatus.Disabled
        };
        _ruleRepoMock.Setup(r => r.GetByCodeAsync(dto.Code, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PointsRuleAggregate?)null);

        var result = await _sut.CreateRuleAsync(dto);

        result.Status.Should().Be(PointsRuleStatus.Disabled);
    }

    #endregion

    #region UpdateRuleAsync

    [Fact]
    public async Task UpdateRuleAsync_Valid_ShouldUpdateAndSave()
    {
        var rule = PointsRuleAggregate.Create(RuleId, "DAILY_CHECK", "每日签到", PointsActionType.CheckIn, 5, 1);
        _ruleRepoMock.Setup(r => r.GetByIdAsync(RuleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(rule);
        var dto = new UpdatePointsRuleDto
        {
            Name = "每日签到（更新）",
            ActionType = PointsActionType.CheckIn,
            Points = 10,
            DailyLimit = 2
        };

        var result = await _sut.UpdateRuleAsync(RuleId, dto);

        result.Name.Should().Be("每日签到（更新）");
        result.Points.Should().Be(10);
        result.DailyLimit.Should().Be(2);
        result.Code.Should().Be("DAILY_CHECK");
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateRuleAsync_NotExist_ShouldThrowNotFoundException()
    {
        _ruleRepoMock.Setup(r => r.GetByIdAsync(RuleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PointsRuleAggregate?)null);
        var dto = new UpdatePointsRuleDto
        {
            Name = "不存在规则",
            ActionType = PointsActionType.CheckIn,
            Points = 5,
            DailyLimit = 1
        };

        var act = () => _sut.UpdateRuleAsync(RuleId, dto);

        var ex = await act.Should().ThrowAsync<PointsDomainException>();
        ex.Which.ErrorCode.Should().Be("POINTS_RULE_NOT_FOUND");
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateRuleAsync_ToNegativePoints_ShouldSucceedAsDeductionRule()
    {
        var rule = PointsRuleAggregate.Create(RuleId, "ORDER_BONUS", "下单奖励", PointsActionType.Order, 10, 5);
        _ruleRepoMock.Setup(r => r.GetByIdAsync(RuleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(rule);
        var dto = new UpdatePointsRuleDto
        {
            Name = "下单扣减",
            ActionType = PointsActionType.Order,
            Points = -20,
            DailyLimit = 5
        };

        var result = await _sut.UpdateRuleAsync(RuleId, dto);

        result.Points.Should().Be(-20);
        result.Name.Should().Be("下单扣减");
    }

    [Fact]
    public async Task UpdateRuleAsync_CodeShouldRemainUnchanged()
    {
        var rule = PointsRuleAggregate.Create(RuleId, "DAILY_CHECK", "每日签到", PointsActionType.CheckIn, 5, 1);
        _ruleRepoMock.Setup(r => r.GetByIdAsync(RuleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(rule);
        var dto = new UpdatePointsRuleDto
        {
            Name = "每日签到更新",
            ActionType = PointsActionType.CheckIn,
            Points = 8,
            DailyLimit = 1
        };

        var result = await _sut.UpdateRuleAsync(RuleId, dto);

        result.Code.Should().Be("DAILY_CHECK");
    }

    #endregion

    #region EnableRuleAsync

    [Fact]
    public async Task EnableRuleAsync_Disabled_ShouldEnableAndSave()
    {
        var rule = PointsRuleAggregate.Create(RuleId, "DAILY_CHECK", "每日签到", PointsActionType.CheckIn, 5, 1, PointsRuleStatus.Disabled);
        _ruleRepoMock.Setup(r => r.GetByIdAsync(RuleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(rule);

        await _sut.EnableRuleAsync(RuleId);

        rule.Status.Should().Be(PointsRuleStatus.Enabled);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EnableRuleAsync_AlreadyEnabled_ShouldThrowAlreadyEnabledException()
    {
        var rule = PointsRuleAggregate.Create(RuleId, "DAILY_CHECK", "每日签到", PointsActionType.CheckIn, 5, 1, PointsRuleStatus.Enabled);
        _ruleRepoMock.Setup(r => r.GetByIdAsync(RuleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(rule);

        var act = () => _sut.EnableRuleAsync(RuleId);

        var ex = await act.Should().ThrowAsync<PointsDomainException>();
        ex.Which.ErrorCode.Should().Be("POINTS_RULE_ALREADY_ENABLED");
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EnableRuleAsync_NotExist_ShouldThrowNotFoundException()
    {
        _ruleRepoMock.Setup(r => r.GetByIdAsync(RuleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PointsRuleAggregate?)null);

        var act = () => _sut.EnableRuleAsync(RuleId);

        var ex = await act.Should().ThrowAsync<PointsDomainException>();
        ex.Which.ErrorCode.Should().Be("POINTS_RULE_NOT_FOUND");
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region DisableRuleAsync

    [Fact]
    public async Task DisableRuleAsync_Enabled_ShouldDisableAndSave()
    {
        var rule = PointsRuleAggregate.Create(RuleId, "DAILY_CHECK", "每日签到", PointsActionType.CheckIn, 5, 1, PointsRuleStatus.Enabled);
        _ruleRepoMock.Setup(r => r.GetByIdAsync(RuleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(rule);

        await _sut.DisableRuleAsync(RuleId);

        rule.Status.Should().Be(PointsRuleStatus.Disabled);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DisableRuleAsync_AlreadyDisabled_ShouldThrowAlreadyDisabledException()
    {
        var rule = PointsRuleAggregate.Create(RuleId, "DAILY_CHECK", "每日签到", PointsActionType.CheckIn, 5, 1, PointsRuleStatus.Disabled);
        _ruleRepoMock.Setup(r => r.GetByIdAsync(RuleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(rule);

        var act = () => _sut.DisableRuleAsync(RuleId);

        var ex = await act.Should().ThrowAsync<PointsDomainException>();
        ex.Which.ErrorCode.Should().Be("POINTS_RULE_ALREADY_DISABLED");
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DisableRuleAsync_NotExist_ShouldThrowNotFoundException()
    {
        _ruleRepoMock.Setup(r => r.GetByIdAsync(RuleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PointsRuleAggregate?)null);

        var act = () => _sut.DisableRuleAsync(RuleId);

        var ex = await act.Should().ThrowAsync<PointsDomainException>();
        ex.Which.ErrorCode.Should().Be("POINTS_RULE_NOT_FOUND");
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region State Transition

    [Fact]
    public async Task StateTransition_EnableThenDisable_ShouldToggleCorrectly()
    {
        var rule = PointsRuleAggregate.Create(RuleId, "DAILY_CHECK", "每日签到", PointsActionType.CheckIn, 5, 1, PointsRuleStatus.Enabled);
        _ruleRepoMock.Setup(r => r.GetByIdAsync(RuleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(rule);

        await _sut.DisableRuleAsync(RuleId);
        rule.Status.Should().Be(PointsRuleStatus.Disabled);

        await _sut.EnableRuleAsync(RuleId);
        rule.Status.Should().Be(PointsRuleStatus.Enabled);

        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    #endregion
}
