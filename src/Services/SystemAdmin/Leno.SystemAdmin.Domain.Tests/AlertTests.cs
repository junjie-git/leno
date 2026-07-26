using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Exceptions;
using Leno.SystemAdmin.Domain.ValueObjects;

namespace Leno.SystemAdmin.Domain.Tests;

/// <summary>
/// 告警事件聚合根单元测试，覆盖工厂创建、状态机（Firing → Acknowledged → Resolved）与字段校验。
/// </summary>
public class AlertTests
{
    private static readonly Guid ValidAlertId = Guid.NewGuid();
    private const string ValidName = "HighErrorRate";
    private const string ValidModule = "Payment";
    private const string ValidSummary = "支付服务错误率超过 5%";
    private const string ValidDescription = "近 5 分钟支付失败率持续超过 5%，请立即排查";
    private const string ValidRelatedMetric = "payment_error_rate";
    private const string ValidOperatorId = "op-001";
    private static readonly DateTime ValidTriggeredAt = DateTime.UtcNow.AddMinutes(-10);
    private const long ValidDurationSeconds = 600;

    private static Dictionary<string, string> ValidLabels => new()
    {
        ["alertname"] = ValidName,
        ["module"] = ValidModule,
        ["severity"] = "critical"
    };

    private static Dictionary<string, string> ValidAnnotations => new()
    {
        ["summary"] = ValidSummary,
        ["description"] = ValidDescription
    };

    #region Create - Happy Path

    [Fact]
    public void Create_WithValidParameters_ShouldSetAllProperties()
    {
        var alert = Alert.Create(
            ValidAlertId,
            ValidName,
            ValidModule,
            AlertSeverity.Critical,
            AlertStatus.Firing,
            ValidLabels,
            ValidAnnotations,
            ValidRelatedMetric,
            ValidSummary,
            ValidDescription,
            ValidTriggeredAt,
            ValidDurationSeconds);

        alert.Id.Should().Be(ValidAlertId);
        alert.Name.Should().Be(ValidName);
        alert.Module.Should().Be(ValidModule);
        alert.Severity.Should().Be(AlertSeverity.Critical);
        alert.Status.Should().Be(AlertStatus.Firing);
        alert.Labels.Should().BeEquivalentTo(ValidLabels);
        alert.Annotations.Should().BeEquivalentTo(ValidAnnotations);
        alert.RelatedMetric.Should().Be(ValidRelatedMetric);
        alert.Summary.Should().Be(ValidSummary);
        alert.Description.Should().Be(ValidDescription);
        alert.TriggeredAt.Should().Be(ValidTriggeredAt);
        alert.DurationSeconds.Should().Be(ValidDurationSeconds);
        alert.AcknowledgedAt.Should().BeNull();
        alert.AcknowledgedBy.Should().BeNull();
        alert.AcknowledgeComment.Should().BeNull();
    }

    [Fact]
    public void Create_ShouldTrimStringFields()
    {
        var alert = Alert.Create(
            ValidAlertId,
            "  " + ValidName + "  ",
            "  " + ValidModule + "  ",
            AlertSeverity.Warning,
            AlertStatus.Firing,
            ValidLabels,
            ValidAnnotations,
            "  " + ValidRelatedMetric + "  ",
            "  " + ValidSummary + "  ",
            "  " + ValidDescription + "  ",
            ValidTriggeredAt,
            ValidDurationSeconds);

        alert.Name.Should().Be(ValidName);
        alert.Module.Should().Be(ValidModule);
        alert.RelatedMetric.Should().Be(ValidRelatedMetric);
        alert.Summary.Should().Be(ValidSummary);
        alert.Description.Should().Be(ValidDescription);
    }

    [Fact]
    public void Create_WithNullOptionalFields_ShouldNormalizeToNull()
    {
        var alert = Alert.Create(
            ValidAlertId,
            ValidName,
            ValidModule,
            AlertSeverity.Info,
            AlertStatus.Firing,
            ValidLabels,
            ValidAnnotations,
            relatedMetric: null,
            summary: null,
            description: null,
            ValidTriggeredAt,
            ValidDurationSeconds);

        alert.RelatedMetric.Should().BeNull();
        alert.Summary.Should().BeNull();
        alert.Description.Should().BeNull();
    }

    [Fact]
    public void Create_WithWhitespaceOptionalFields_ShouldNormalizeToNull()
    {
        var alert = Alert.Create(
            ValidAlertId,
            ValidName,
            ValidModule,
            AlertSeverity.Info,
            AlertStatus.Firing,
            ValidLabels,
            ValidAnnotations,
            "   ",
            "   ",
            "   ",
            ValidTriggeredAt,
            ValidDurationSeconds);

        alert.RelatedMetric.Should().BeNull();
        alert.Summary.Should().BeNull();
        alert.Description.Should().BeNull();
    }

    [Fact]
    public void Create_WithAcknowledgedState_ShouldSetAckFields()
    {
        var ackAt = DateTime.UtcNow.AddMinutes(-5);
        var alert = Alert.Create(
            ValidAlertId,
            ValidName,
            ValidModule,
            AlertSeverity.Critical,
            AlertStatus.Acknowledged,
            ValidLabels,
            ValidAnnotations,
            null,
            null,
            null,
            ValidTriggeredAt,
            ValidDurationSeconds,
            acknowledgedAt: ackAt,
            acknowledgedBy: ValidOperatorId,
            acknowledgeComment: "已介入");

        alert.Status.Should().Be(AlertStatus.Acknowledged);
        alert.AcknowledgedAt.Should().Be(ackAt);
        alert.AcknowledgedBy.Should().Be(ValidOperatorId);
        alert.AcknowledgeComment.Should().Be("已介入");
    }

    #endregion

    #region Create - Validation

    [Fact]
    public void Create_WithEmptyId_ShouldThrowAlertIdEmpty()
    {
        var act = () => Alert.Create(
            Guid.Empty,
            ValidName,
            ValidModule,
            AlertSeverity.Critical,
            AlertStatus.Firing,
            ValidLabels,
            ValidAnnotations,
            null,
            null,
            null,
            ValidTriggeredAt,
            ValidDurationSeconds);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("ALERT_ID_EMPTY");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithInvalidName_ShouldThrowNameEmpty(string? name)
    {
        var act = () => Alert.Create(
            ValidAlertId,
            name!,
            ValidModule,
            AlertSeverity.Critical,
            AlertStatus.Firing,
            ValidLabels,
            ValidAnnotations,
            null,
            null,
            null,
            ValidTriggeredAt,
            ValidDurationSeconds);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("ALERT_NAME_EMPTY");
    }

    [Fact]
    public void Create_WithTooLongName_ShouldThrowNameLength()
    {
        var name = new string('x', 257);

        var act = () => Alert.Create(
            ValidAlertId,
            name,
            ValidModule,
            AlertSeverity.Critical,
            AlertStatus.Firing,
            ValidLabels,
            ValidAnnotations,
            null,
            null,
            null,
            ValidTriggeredAt,
            ValidDurationSeconds);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("ALERT_NAME_LENGTH");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithInvalidModule_ShouldThrowModuleEmpty(string? module)
    {
        var act = () => Alert.Create(
            ValidAlertId,
            ValidName,
            module!,
            AlertSeverity.Critical,
            AlertStatus.Firing,
            ValidLabels,
            ValidAnnotations,
            null,
            null,
            null,
            ValidTriggeredAt,
            ValidDurationSeconds);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("ALERT_MODULE_EMPTY");
    }

    [Fact]
    public void Create_WithNegativeDuration_ShouldThrowDurationNegative()
    {
        var act = () => Alert.Create(
            ValidAlertId,
            ValidName,
            ValidModule,
            AlertSeverity.Critical,
            AlertStatus.Firing,
            ValidLabels,
            ValidAnnotations,
            null,
            null,
            null,
            ValidTriggeredAt,
            -1);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("ALERT_DURATION_NEGATIVE");
    }

    [Fact]
    public void Create_WithTooLongAckComment_ShouldThrowAckCommentLength()
    {
        var comment = new string('c', 1001);

        var act = () => Alert.Create(
            ValidAlertId,
            ValidName,
            ValidModule,
            AlertSeverity.Critical,
            AlertStatus.Firing,
            ValidLabels,
            ValidAnnotations,
            null,
            null,
            null,
            ValidTriggeredAt,
            ValidDurationSeconds,
            acknowledgeComment: comment);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("ALERT_ACK_COMMENT_LENGTH");
    }

    [Fact]
    public void Create_WithTooLongOperatorId_ShouldThrowOperatorLength()
    {
        var operatorId = new string('o', 65);

        var act = () => Alert.Create(
            ValidAlertId,
            ValidName,
            ValidModule,
            AlertSeverity.Critical,
            AlertStatus.Firing,
            ValidLabels,
            ValidAnnotations,
            null,
            null,
            null,
            ValidTriggeredAt,
            ValidDurationSeconds,
            acknowledgedBy: operatorId);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("ALERT_OPERATOR_LENGTH");
    }

    #endregion

    #region Acknowledge - State Machine

    [Fact]
    public void Acknowledge_FromFiring_ShouldTransitionToAcknowledged()
    {
        var alert = CreateAlert(AlertStatus.Firing);

        alert.Acknowledge(ValidOperatorId, "已介入处理");

        alert.Status.Should().Be(AlertStatus.Acknowledged);
        alert.AcknowledgedBy.Should().Be(ValidOperatorId);
        alert.AcknowledgeComment.Should().Be("已介入处理");
        alert.AcknowledgedAt.Should().NotBeNull();
        alert.AcknowledgedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Acknowledge_FromAcknowledged_ShouldBeIdempotent()
    {
        var originalAckAt = DateTime.UtcNow.AddMinutes(-5);
        var alert = Alert.Create(
            ValidAlertId,
            ValidName,
            ValidModule,
            AlertSeverity.Critical,
            AlertStatus.Acknowledged,
            ValidLabels,
            ValidAnnotations,
            ValidRelatedMetric,
            ValidSummary,
            ValidDescription,
            ValidTriggeredAt,
            ValidDurationSeconds,
            acknowledgedAt: originalAckAt,
            acknowledgedBy: "original-op",
            acknowledgeComment: "原备注");

        alert.Acknowledge(ValidOperatorId, "再次确认");

        alert.Status.Should().Be(AlertStatus.Acknowledged);
        alert.AcknowledgedAt.Should().Be(originalAckAt);
        alert.AcknowledgedBy.Should().Be("original-op");
        alert.AcknowledgeComment.Should().Be("原备注");
    }

    [Fact]
    public void Acknowledge_FromResolved_ShouldThrowAlreadyResolved()
    {
        var alert = CreateAlert(AlertStatus.Resolved);

        var act = () => alert.Acknowledge(ValidOperatorId, null);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("ALERT_ALREADY_RESOLVED");
    }

    [Fact]
    public void Acknowledge_WithEmptyOperatorId_ShouldThrowOperatorEmpty()
    {
        var alert = CreateAlert(AlertStatus.Firing);

        var act = () => alert.Acknowledge("", null);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("ALERT_ACK_OPERATOR_EMPTY");
    }

    [Fact]
    public void Acknowledge_WithNullComment_ShouldSetNullComment()
    {
        var alert = CreateAlert(AlertStatus.Firing);

        alert.Acknowledge(ValidOperatorId, null);

        alert.AcknowledgeComment.Should().BeNull();
        alert.AcknowledgedBy.Should().Be(ValidOperatorId);
    }

    [Fact]
    public void Acknowledge_WithWhitespaceComment_ShouldNormalizeToNull()
    {
        var alert = CreateAlert(AlertStatus.Firing);

        alert.Acknowledge(ValidOperatorId, "   ");

        alert.AcknowledgeComment.Should().BeNull();
    }

    #endregion

    #region Resolve - State Machine

    [Fact]
    public void Resolve_FromFiring_ShouldTransitionToResolved()
    {
        var alert = CreateAlert(AlertStatus.Firing);

        alert.Resolve();

        alert.Status.Should().Be(AlertStatus.Resolved);
    }

    [Fact]
    public void Resolve_FromAcknowledged_ShouldTransitionToResolved()
    {
        var alert = CreateAlert(AlertStatus.Acknowledged);

        alert.Resolve();

        alert.Status.Should().Be(AlertStatus.Resolved);
    }

    [Fact]
    public void Resolve_FromResolved_ShouldBeIdempotent()
    {
        var alert = CreateAlert(AlertStatus.Resolved);

        alert.Resolve();

        alert.Status.Should().Be(AlertStatus.Resolved);
    }

    #endregion

    private static Alert CreateAlert(AlertStatus status)
        => Alert.Create(
            ValidAlertId,
            ValidName,
            ValidModule,
            AlertSeverity.Critical,
            status,
            ValidLabels,
            ValidAnnotations,
            ValidRelatedMetric,
            ValidSummary,
            ValidDescription,
            ValidTriggeredAt,
            ValidDurationSeconds);
}
