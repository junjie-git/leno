using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Events;
using Leno.SystemAdmin.Domain.Exceptions;
using Leno.SystemAdmin.Domain.ValueObjects;

namespace Leno.SystemAdmin.Domain.Tests;

public class RateLimitRuleTests
{
    private static readonly Guid ValidRuleId = Guid.NewGuid();
    private const string ValidTargetApi = "/api/orders";
    private const string ValidTargetContext = "userId";
    private const int ValidLimit = 100;
    private const int ValidWindowSeconds = 60;

    #region Create - Happy Path

    [Fact]
    public void Create_WithValidParameters_ShouldSetAllProperties()
    {
        var rule = RateLimitRule.Create(
            ValidRuleId, ValidTargetApi, ValidTargetContext, ValidLimit,
            ValidWindowSeconds, LimitAlgorithm.SlidingWindow, LimitScope.User);

        rule.RuleId.Should().Be(ValidRuleId);
        rule.Id.Should().Be(ValidRuleId);
        rule.TargetApi.Should().Be(ValidTargetApi);
        rule.TargetContext.Should().Be(ValidTargetContext);
        rule.Limit.Should().Be(ValidLimit);
        rule.WindowSeconds.Should().Be(ValidWindowSeconds);
        rule.Algorithm.Should().Be(LimitAlgorithm.SlidingWindow);
        rule.Scope.Should().Be(LimitScope.User);
        rule.Enabled.Should().BeTrue();
    }

    [Fact]
    public void Create_WithMinimalParameters_ShouldSetDefaults()
    {
        var rule = RateLimitRule.Create(
            ValidRuleId, ValidTargetApi, targetContext: null, ValidLimit,
            ValidWindowSeconds, LimitAlgorithm.FixedWindow, LimitScope.Global);

        rule.TargetContext.Should().BeNull();
        rule.Enabled.Should().BeTrue();
        rule.Algorithm.Should().Be(LimitAlgorithm.FixedWindow);
        rule.Scope.Should().Be(LimitScope.Global);
    }

    [Fact]
    public void Create_ShouldTrimTargetApi()
    {
        var rule = RateLimitRule.Create(
            ValidRuleId, "  /api/orders  ", ValidTargetContext, ValidLimit,
            ValidWindowSeconds, LimitAlgorithm.FixedWindow, LimitScope.Global);

        rule.TargetApi.Should().Be("/api/orders");
    }

    [Fact]
    public void Create_WithWhitespaceTargetContext_ShouldNormalizeToNull()
    {
        var rule = RateLimitRule.Create(
            ValidRuleId, ValidTargetApi, "   ", ValidLimit,
            ValidWindowSeconds, LimitAlgorithm.FixedWindow, LimitScope.Global);

        rule.TargetContext.Should().BeNull();
    }

    [Fact]
    public void Create_WithTokenBucketAlgorithm_ShouldSetCorrectly()
    {
        var rule = RateLimitRule.Create(
            ValidRuleId, ValidTargetApi, ValidTargetContext, ValidLimit,
            ValidWindowSeconds, LimitAlgorithm.TokenBucket, LimitScope.Ip);

        rule.Algorithm.Should().Be(LimitAlgorithm.TokenBucket);
        rule.Scope.Should().Be(LimitScope.Ip);
    }

    [Fact]
    public void Create_WithShopScope_ShouldSetCorrectly()
    {
        var rule = RateLimitRule.Create(
            ValidRuleId, ValidTargetApi, ValidTargetContext, ValidLimit,
            ValidWindowSeconds, LimitAlgorithm.SlidingWindow, LimitScope.Shop);

        rule.Scope.Should().Be(LimitScope.Shop);
    }

    #endregion

    #region Create - Validation

    [Fact]
    public void Create_WithEmptyRuleId_ShouldThrowRateLimitRuleIdEmpty()
    {
        var act = () => RateLimitRule.Create(
            Guid.Empty, ValidTargetApi, ValidTargetContext, ValidLimit,
            ValidWindowSeconds, LimitAlgorithm.FixedWindow, LimitScope.Global);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("RATE_LIMIT_RULE_ID_EMPTY");
    }

    [Fact]
    public void Create_WithNullTargetApi_ShouldThrowRateLimitTargetApiEmpty()
    {
        var act = () => RateLimitRule.Create(
            ValidRuleId, null!, ValidTargetContext, ValidLimit,
            ValidWindowSeconds, LimitAlgorithm.FixedWindow, LimitScope.Global);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("RATE_LIMIT_TARGET_API_EMPTY");
    }

    [Fact]
    public void Create_WithEmptyTargetApi_ShouldThrowRateLimitTargetApiEmpty()
    {
        var act = () => RateLimitRule.Create(
            ValidRuleId, "", ValidTargetContext, ValidLimit,
            ValidWindowSeconds, LimitAlgorithm.FixedWindow, LimitScope.Global);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("RATE_LIMIT_TARGET_API_EMPTY");
    }

    [Fact]
    public void Create_WithWhitespaceTargetApi_ShouldThrowRateLimitTargetApiEmpty()
    {
        var act = () => RateLimitRule.Create(
            ValidRuleId, "   ", ValidTargetContext, ValidLimit,
            ValidWindowSeconds, LimitAlgorithm.FixedWindow, LimitScope.Global);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("RATE_LIMIT_TARGET_API_EMPTY");
    }

    [Fact]
    public void Create_WithTargetApiTooLong_ShouldThrowRateLimitTargetApiLength()
    {
        var longApi = new string('a', 257);

        var act = () => RateLimitRule.Create(
            ValidRuleId, longApi, ValidTargetContext, ValidLimit,
            ValidWindowSeconds, LimitAlgorithm.FixedWindow, LimitScope.Global);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("RATE_LIMIT_TARGET_API_LENGTH");
    }

    [Fact]
    public void Create_WithTargetApiAtMaxLength_ShouldSucceed()
    {
        var api = new string('a', 256);

        var rule = RateLimitRule.Create(
            ValidRuleId, api, ValidTargetContext, ValidLimit,
            ValidWindowSeconds, LimitAlgorithm.FixedWindow, LimitScope.Global);

        rule.TargetApi.Should().Be(api);
    }

    [Fact]
    public void Create_WithTargetContextTooLong_ShouldThrowRateLimitTargetContextLength()
    {
        var longCtx = new string('c', 65);

        var act = () => RateLimitRule.Create(
            ValidRuleId, ValidTargetApi, longCtx, ValidLimit,
            ValidWindowSeconds, LimitAlgorithm.FixedWindow, LimitScope.Global);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("RATE_LIMIT_TARGET_CONTEXT_LENGTH");
    }

    [Fact]
    public void Create_WithTargetContextAtMaxLength_ShouldSucceed()
    {
        var ctx = new string('c', 64);

        var rule = RateLimitRule.Create(
            ValidRuleId, ValidTargetApi, ctx, ValidLimit,
            ValidWindowSeconds, LimitAlgorithm.FixedWindow, LimitScope.Global);

        rule.TargetContext.Should().Be(ctx);
    }

    [Fact]
    public void Create_WithZeroLimit_ShouldThrowRateLimitLimitInvalid()
    {
        var act = () => RateLimitRule.Create(
            ValidRuleId, ValidTargetApi, ValidTargetContext, 0,
            ValidWindowSeconds, LimitAlgorithm.FixedWindow, LimitScope.Global);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("RATE_LIMIT_LIMIT_INVALID");
    }

    [Fact]
    public void Create_WithNegativeLimit_ShouldThrowRateLimitLimitInvalid()
    {
        var act = () => RateLimitRule.Create(
            ValidRuleId, ValidTargetApi, ValidTargetContext, -1,
            ValidWindowSeconds, LimitAlgorithm.FixedWindow, LimitScope.Global);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("RATE_LIMIT_LIMIT_INVALID");
    }

    [Fact]
    public void Create_WithZeroWindowSeconds_ShouldThrowRateLimitWindowInvalid()
    {
        var act = () => RateLimitRule.Create(
            ValidRuleId, ValidTargetApi, ValidTargetContext, ValidLimit,
            0, LimitAlgorithm.FixedWindow, LimitScope.Global);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("RATE_LIMIT_WINDOW_INVALID");
    }

    [Fact]
    public void Create_WithNegativeWindowSeconds_ShouldThrowRateLimitWindowInvalid()
    {
        var act = () => RateLimitRule.Create(
            ValidRuleId, ValidTargetApi, ValidTargetContext, ValidLimit,
            -1, LimitAlgorithm.FixedWindow, LimitScope.Global);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("RATE_LIMIT_WINDOW_INVALID");
    }

    #endregion

    #region Update

    [Fact]
    public void Update_WithValidParameters_ShouldUpdateProperties()
    {
        var rule = RateLimitRule.Create(
            ValidRuleId, ValidTargetApi, ValidTargetContext, ValidLimit,
            ValidWindowSeconds, LimitAlgorithm.FixedWindow, LimitScope.Global);

        rule.Update("/api/products", "shopId", 200, 120,
            LimitAlgorithm.TokenBucket, LimitScope.Shop);

        rule.TargetApi.Should().Be("/api/products");
        rule.TargetContext.Should().Be("shopId");
        rule.Limit.Should().Be(200);
        rule.WindowSeconds.Should().Be(120);
        rule.Algorithm.Should().Be(LimitAlgorithm.TokenBucket);
        rule.Scope.Should().Be(LimitScope.Shop);
    }

    [Fact]
    public void Update_ShouldRaiseRateLimitRuleUpdatedEvent()
    {
        var rule = RateLimitRule.Create(
            ValidRuleId, ValidTargetApi, ValidTargetContext, ValidLimit,
            ValidWindowSeconds, LimitAlgorithm.FixedWindow, LimitScope.Global);
        rule.ClearDomainEvents();

        rule.Update("/api/products", null, 200, 120,
            LimitAlgorithm.SlidingWindow, LimitScope.Global);

        rule.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<RateLimitRuleUpdatedEvent>();
        var evt = (RateLimitRuleUpdatedEvent)rule.DomainEvents.First();
        evt.RuleId.Should().Be(rule.Id);
    }

    [Fact]
    public void Update_WithNullTargetApi_ShouldThrow()
    {
        var rule = RateLimitRule.Create(
            ValidRuleId, ValidTargetApi, ValidTargetContext, ValidLimit,
            ValidWindowSeconds, LimitAlgorithm.FixedWindow, LimitScope.Global);

        var act = () => rule.Update(null!, null, ValidLimit, ValidWindowSeconds,
            LimitAlgorithm.FixedWindow, LimitScope.Global);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("RATE_LIMIT_TARGET_API_EMPTY");
    }

    [Fact]
    public void Update_WithZeroLimit_ShouldThrow()
    {
        var rule = RateLimitRule.Create(
            ValidRuleId, ValidTargetApi, ValidTargetContext, ValidLimit,
            ValidWindowSeconds, LimitAlgorithm.FixedWindow, LimitScope.Global);

        var act = () => rule.Update(ValidTargetApi, null, 0, ValidWindowSeconds,
            LimitAlgorithm.FixedWindow, LimitScope.Global);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("RATE_LIMIT_LIMIT_INVALID");
    }

    #endregion

    #region Enable

    [Fact]
    public void Enable_WhenDisabled_ShouldSetEnabledToTrue()
    {
        var rule = RateLimitRule.Create(
            ValidRuleId, ValidTargetApi, ValidTargetContext, ValidLimit,
            ValidWindowSeconds, LimitAlgorithm.FixedWindow, LimitScope.Global);
        rule.Disable();
        rule.ClearDomainEvents();

        rule.Enable();

        rule.Enabled.Should().BeTrue();
    }

    [Fact]
    public void Enable_WhenAlreadyEnabled_ShouldNotRaiseEvent()
    {
        var rule = RateLimitRule.Create(
            ValidRuleId, ValidTargetApi, ValidTargetContext, ValidLimit,
            ValidWindowSeconds, LimitAlgorithm.FixedWindow, LimitScope.Global);
        rule.ClearDomainEvents();

        rule.Enable();

        rule.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Enable_ShouldRaiseRateLimitRuleUpdatedEvent()
    {
        var rule = RateLimitRule.Create(
            ValidRuleId, ValidTargetApi, ValidTargetContext, ValidLimit,
            ValidWindowSeconds, LimitAlgorithm.FixedWindow, LimitScope.Global);
        rule.Disable();
        rule.ClearDomainEvents();

        rule.Enable();

        rule.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<RateLimitRuleUpdatedEvent>();
    }

    #endregion

    #region Disable

    [Fact]
    public void Disable_WhenEnabled_ShouldSetEnabledToFalse()
    {
        var rule = RateLimitRule.Create(
            ValidRuleId, ValidTargetApi, ValidTargetContext, ValidLimit,
            ValidWindowSeconds, LimitAlgorithm.FixedWindow, LimitScope.Global);

        rule.Disable();

        rule.Enabled.Should().BeFalse();
    }

    [Fact]
    public void Disable_WhenAlreadyDisabled_ShouldNotRaiseEvent()
    {
        var rule = RateLimitRule.Create(
            ValidRuleId, ValidTargetApi, ValidTargetContext, ValidLimit,
            ValidWindowSeconds, LimitAlgorithm.FixedWindow, LimitScope.Global);
        rule.Disable();
        rule.ClearDomainEvents();

        rule.Disable();

        rule.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Disable_ShouldRaiseRateLimitRuleUpdatedEvent()
    {
        var rule = RateLimitRule.Create(
            ValidRuleId, ValidTargetApi, ValidTargetContext, ValidLimit,
            ValidWindowSeconds, LimitAlgorithm.FixedWindow, LimitScope.Global);
        rule.ClearDomainEvents();

        rule.Disable();

        rule.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<RateLimitRuleUpdatedEvent>();
    }

    #endregion

    #region All Algorithms and Scopes

    [Theory]
    [InlineData(LimitAlgorithm.FixedWindow)]
    [InlineData(LimitAlgorithm.SlidingWindow)]
    [InlineData(LimitAlgorithm.TokenBucket)]
    public void Create_WithAllAlgorithms_ShouldSetCorrectly(LimitAlgorithm algorithm)
    {
        var rule = RateLimitRule.Create(
            ValidRuleId, ValidTargetApi, ValidTargetContext, ValidLimit,
            ValidWindowSeconds, algorithm, LimitScope.Global);

        rule.Algorithm.Should().Be(algorithm);
    }

    [Theory]
    [InlineData(LimitScope.Ip)]
    [InlineData(LimitScope.User)]
    [InlineData(LimitScope.Global)]
    [InlineData(LimitScope.Shop)]
    public void Create_WithAllScopes_ShouldSetCorrectly(LimitScope scope)
    {
        var rule = RateLimitRule.Create(
            ValidRuleId, ValidTargetApi, ValidTargetContext, ValidLimit,
            ValidWindowSeconds, LimitAlgorithm.FixedWindow, scope);

        rule.Scope.Should().Be(scope);
    }

    #endregion
}