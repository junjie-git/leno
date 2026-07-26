using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Exceptions;

namespace Leno.SystemAdmin.Domain.Tests;

/// <summary>
/// 告警静默规则聚合根单元测试，覆盖工厂创建、JSON 匹配器解析、过期判断与字段校验。
/// </summary>
public class AlertSilenceTests
{
    private static readonly Guid ValidSilenceId = Guid.NewGuid();
    private const string ValidMatchersJson = "[{\"name\":\"module\",\"value\":\"Payment\",\"isRegex\":false}]";
    private const string ValidDuration = "2h";
    private const string ValidReason = "支付模块维护期间静默";
    private const string ValidCreatedBy = "op-001";
    private static readonly DateTime ValidStartsAt = DateTime.UtcNow;
    private static readonly DateTime ValidEndsAt = DateTime.UtcNow.AddHours(2);
    private static readonly DateTime ValidCreatedAt = DateTime.UtcNow;

    #region Create - Happy Path

    [Fact]
    public void Create_WithValidParameters_ShouldSetAllProperties()
    {
        var silence = AlertSilence.Create(
            ValidSilenceId,
            ValidMatchersJson,
            ValidDuration,
            ValidReason,
            ValidStartsAt,
            ValidEndsAt,
            ValidCreatedBy,
            ValidCreatedAt);

        silence.Id.Should().Be(ValidSilenceId);
        silence.Matchers.Should().Be(ValidMatchersJson);
        silence.Duration.Should().Be(ValidDuration);
        silence.Reason.Should().Be(ValidReason);
        silence.StartsAt.Should().Be(ValidStartsAt);
        silence.EndsAt.Should().Be(ValidEndsAt);
        silence.CreatedBy.Should().Be(ValidCreatedBy);
        silence.CreatedAt.Should().Be(ValidCreatedAt);
    }

    [Fact]
    public void Create_ShouldTrimFields()
    {
        var silence = AlertSilence.Create(
            ValidSilenceId,
            "  " + ValidMatchersJson + "  ",
            "  " + ValidDuration + "  ",
            "  " + ValidReason + "  ",
            ValidStartsAt,
            ValidEndsAt,
            "  " + ValidCreatedBy + "  ",
            ValidCreatedAt);

        silence.Matchers.Should().Be(ValidMatchersJson);
        silence.Duration.Should().Be(ValidDuration);
        silence.Reason.Should().Be(ValidReason);
        silence.CreatedBy.Should().Be(ValidCreatedBy);
    }

    #endregion

    #region Create - Validation

    [Fact]
    public void Create_WithEmptyId_ShouldThrowIdEmpty()
    {
        var act = () => AlertSilence.Create(
            Guid.Empty,
            ValidMatchersJson,
            ValidDuration,
            ValidReason,
            ValidStartsAt,
            ValidEndsAt,
            ValidCreatedBy,
            ValidCreatedAt);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("ALERT_SILENCE_ID_EMPTY");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithInvalidMatchers_ShouldThrowMatchersEmpty(string? matchers)
    {
        var act = () => AlertSilence.Create(
            ValidSilenceId,
            matchers!,
            ValidDuration,
            ValidReason,
            ValidStartsAt,
            ValidEndsAt,
            ValidCreatedBy,
            ValidCreatedAt);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("ALERT_SILENCE_MATCHERS_EMPTY");
    }

    [Theory]
    [InlineData("{\"name\":\"module\"}")]
    [InlineData("notJson")]
    public void Create_WithNonArrayMatchers_ShouldThrowMatchersInvalidJson(string matchers)
    {
        var act = () => AlertSilence.Create(
            ValidSilenceId,
            matchers,
            ValidDuration,
            ValidReason,
            ValidStartsAt,
            ValidEndsAt,
            ValidCreatedBy,
            ValidCreatedAt);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("ALERT_SILENCE_MATCHERS_INVALID_JSON");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithInvalidDuration_ShouldThrowDurationEmpty(string? duration)
    {
        var act = () => AlertSilence.Create(
            ValidSilenceId,
            ValidMatchersJson,
            duration!,
            ValidReason,
            ValidStartsAt,
            ValidEndsAt,
            ValidCreatedBy,
            ValidCreatedAt);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("ALERT_SILENCE_DURATION_EMPTY");
    }

    [Fact]
    public void Create_WithTooLongDuration_ShouldThrowDurationLength()
    {
        var duration = new string('h', 65);

        var act = () => AlertSilence.Create(
            ValidSilenceId,
            ValidMatchersJson,
            duration,
            ValidReason,
            ValidStartsAt,
            ValidEndsAt,
            ValidCreatedBy,
            ValidCreatedAt);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("ALERT_SILENCE_DURATION_LENGTH");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithInvalidReason_ShouldThrowReasonEmpty(string? reason)
    {
        var act = () => AlertSilence.Create(
            ValidSilenceId,
            ValidMatchersJson,
            ValidDuration,
            reason!,
            ValidStartsAt,
            ValidEndsAt,
            ValidCreatedBy,
            ValidCreatedAt);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("ALERT_SILENCE_REASON_EMPTY");
    }

    [Fact]
    public void Create_WithTooLongReason_ShouldThrowReasonLength()
    {
        var reason = new string('r', 1001);

        var act = () => AlertSilence.Create(
            ValidSilenceId,
            ValidMatchersJson,
            ValidDuration,
            reason,
            ValidStartsAt,
            ValidEndsAt,
            ValidCreatedBy,
            ValidCreatedAt);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("ALERT_SILENCE_REASON_LENGTH");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithInvalidCreatedBy_ShouldThrowCreatedByEmpty(string? createdBy)
    {
        var act = () => AlertSilence.Create(
            ValidSilenceId,
            ValidMatchersJson,
            ValidDuration,
            ValidReason,
            ValidStartsAt,
            ValidEndsAt,
            createdBy!,
            ValidCreatedAt);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("ALERT_SILENCE_CREATED_BY_EMPTY");
    }

    [Fact]
    public void Create_WithTooLongCreatedBy_ShouldThrowCreatedByLength()
    {
        var createdBy = new string('o', 65);

        var act = () => AlertSilence.Create(
            ValidSilenceId,
            ValidMatchersJson,
            ValidDuration,
            ValidReason,
            ValidStartsAt,
            ValidEndsAt,
            createdBy,
            ValidCreatedAt);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("ALERT_SILENCE_CREATED_BY_LENGTH");
    }

    [Fact]
    public void Create_WithEndsAtBeforeStartsAt_ShouldThrowTimeRangeInvalid()
    {
        var act = () => AlertSilence.Create(
            ValidSilenceId,
            ValidMatchersJson,
            ValidDuration,
            ValidReason,
            ValidEndsAt,
            ValidStartsAt,
            ValidCreatedBy,
            ValidCreatedAt);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("ALERT_SILENCE_TIME_RANGE_INVALID");
    }

    [Fact]
    public void Create_WithEndsAtEqualToStartsAt_ShouldThrowTimeRangeInvalid()
    {
        var at = DateTime.UtcNow;
        var act = () => AlertSilence.Create(
            ValidSilenceId,
            ValidMatchersJson,
            ValidDuration,
            ValidReason,
            at,
            at,
            ValidCreatedBy,
            ValidCreatedAt);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("ALERT_SILENCE_TIME_RANGE_INVALID");
    }

    #endregion

    #region IsExpired

    [Fact]
    public void IsExpired_WhenNowAfterEndsAt_ShouldReturnTrue()
    {
        var silence = AlertSilence.Create(
            ValidSilenceId,
            ValidMatchersJson,
            ValidDuration,
            ValidReason,
            DateTime.UtcNow.AddHours(-3),
            DateTime.UtcNow.AddHours(-1),
            ValidCreatedBy,
            ValidCreatedAt);

        silence.IsExpired().Should().BeTrue();
    }

    [Fact]
    public void IsExpired_WhenNowBeforeEndsAt_ShouldReturnFalse()
    {
        var silence = AlertSilence.Create(
            ValidSilenceId,
            ValidMatchersJson,
            ValidDuration,
            ValidReason,
            DateTime.UtcNow,
            DateTime.UtcNow.AddHours(2),
            ValidCreatedBy,
            ValidCreatedAt);

        silence.IsExpired().Should().BeFalse();
    }

    [Fact]
    public void IsExpired_WithExplicitAt_ShouldUseProvidedTime()
    {
        var silence = AlertSilence.Create(
            ValidSilenceId,
            ValidMatchersJson,
            ValidDuration,
            ValidReason,
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 1, 2, 0, 0, DateTimeKind.Utc),
            ValidCreatedBy,
            ValidCreatedAt);

        silence.IsExpired(new DateTime(2026, 1, 1, 3, 0, 0, DateTimeKind.Utc)).Should().BeTrue();
        silence.IsExpired(new DateTime(2026, 1, 1, 1, 30, 0, DateTimeKind.Utc)).Should().BeFalse();
    }

    #endregion

    #region GetMatchers

    [Fact]
    public void GetMatchers_WithValidJson_ShouldReturnMatcherList()
    {
        var matchersJson = "[{\"name\":\"module\",\"value\":\"Payment\",\"isRegex\":false},{\"name\":\"severity\",\"value\":\"critical\",\"isRegex\":false}]";
        var silence = AlertSilence.Create(
            ValidSilenceId,
            matchersJson,
            ValidDuration,
            ValidReason,
            ValidStartsAt,
            ValidEndsAt,
            ValidCreatedBy,
            ValidCreatedAt);

        var matchers = silence.GetMatchers();

        matchers.Should().HaveCount(2);
        matchers[0].Name.Should().Be("module");
        matchers[0].Value.Should().Be("Payment");
        matchers[0].IsRegex.Should().BeFalse();
        matchers[1].Name.Should().Be("severity");
        matchers[1].Value.Should().Be("critical");
    }

    [Fact]
    public void GetMatchers_WithRegexFlag_ShouldPreserveIsRegex()
    {
        var matchersJson = "[{\"name\":\"module\",\"value\":\"Pay.*\",\"isRegex\":true}]";
        var silence = AlertSilence.Create(
            ValidSilenceId,
            matchersJson,
            ValidDuration,
            ValidReason,
            ValidStartsAt,
            ValidEndsAt,
            ValidCreatedBy,
            ValidCreatedAt);

        var matchers = silence.GetMatchers();

        matchers.Should().HaveCount(1);
        matchers[0].IsRegex.Should().BeTrue();
        matchers[0].Value.Should().Be("Pay.*");
    }

    [Fact]
    public void GetMatchers_WithEmptyArray_ShouldReturnEmptyList()
    {
        var silence = AlertSilence.Create(
            ValidSilenceId,
            "[]",
            ValidDuration,
            ValidReason,
            ValidStartsAt,
            ValidEndsAt,
            ValidCreatedBy,
            ValidCreatedAt);

        silence.GetMatchers().Should().BeEmpty();
    }

    [Fact]
    public void GetMatchers_WithInvalidJson_ShouldReturnEmptyListGracefully()
    {
        // Create 仅校验首尾方括号，"[broken]" 通过工厂校验但 JSON 解析失败时回退为空集合
        var silence = AlertSilence.Create(
            ValidSilenceId,
            "[broken]",
            ValidDuration,
            ValidReason,
            ValidStartsAt,
            ValidEndsAt,
            ValidCreatedBy,
            ValidCreatedAt);

        silence.GetMatchers().Should().BeEmpty();
    }

    [Fact]
    public void GetMatchers_WithNullNameField_ShouldThrowMatcherNameEmpty()
    {
        // AlertMatcher 构造器校验 name 非空，null/空名经 GetMatchers 反序列化后抛 SystemAdminDomainException
        var matchersJson = "[{\"name\":null,\"value\":\"v\",\"isRegex\":false}]";
        var silence = AlertSilence.Create(
            ValidSilenceId,
            matchersJson,
            ValidDuration,
            ValidReason,
            ValidStartsAt,
            ValidEndsAt,
            ValidCreatedBy,
            ValidCreatedAt);

        var act = () => silence.GetMatchers();

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("ALERT_MATCHER_NAME_EMPTY");
    }

    #endregion
}
