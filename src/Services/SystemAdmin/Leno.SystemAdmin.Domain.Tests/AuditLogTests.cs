using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Exceptions;

namespace Leno.SystemAdmin.Domain.Tests;

public class AuditLogTests
{
    private static readonly Guid ValidLogId = Guid.NewGuid();
    private static readonly Guid ValidOperatorId = Guid.NewGuid();
    private const string ValidAction = "Create";
    private const string ValidResourceType = "SystemConfig";
    private const string ValidResourceId = "config-123";
    private const string ValidRequestSummary = "Created a new system configuration";
    private const string ValidIpAddress = "10.0.0.1";
    private const string ValidTraceId = "trace-abc-123";
    private static readonly DateTime ValidOccurredAt = DateTime.UtcNow;

    #region Create - Happy Path

    [Fact]
    public void Create_WithValidParameters_ShouldSetAllProperties()
    {
        var log = AuditLog.Create(
            ValidLogId, ValidOperatorId, ValidAction, ValidResourceType, ValidResourceId,
            ValidRequestSummary, responseStatus: 200, ValidIpAddress, ValidTraceId, ValidOccurredAt);

        log.LogId.Should().Be(ValidLogId);
        log.Id.Should().Be(ValidLogId);
        log.OperatorId.Should().Be(ValidOperatorId);
        log.Action.Should().Be(ValidAction);
        log.ResourceType.Should().Be(ValidResourceType);
        log.ResourceId.Should().Be(ValidResourceId);
        log.RequestSummary.Should().Be(ValidRequestSummary);
        log.ResponseStatus.Should().Be(200);
        log.IpAddress.Should().Be(ValidIpAddress);
        log.TraceId.Should().Be(ValidTraceId);
        log.OccurredAt.Should().Be(ValidOccurredAt);
    }

    [Fact]
    public void Create_WithMinimalParameters_ShouldSetNullsForOptionals()
    {
        var log = AuditLog.Create(
            ValidLogId, ValidOperatorId, ValidAction, ValidResourceType, ValidResourceId,
            requestSummary: null, responseStatus: 500, ipAddress: null, traceId: null, ValidOccurredAt);

        log.RequestSummary.Should().BeNull();
        log.IpAddress.Should().BeNull();
        log.TraceId.Should().BeNull();
        log.ResponseStatus.Should().Be(500);
    }

    [Fact]
    public void Create_WithNegativeResponseStatus_ShouldWork()
    {
        var log = AuditLog.Create(
            ValidLogId, ValidOperatorId, ValidAction, ValidResourceType, ValidResourceId,
            ValidRequestSummary, responseStatus: -1, ValidIpAddress, ValidTraceId, ValidOccurredAt);

        log.ResponseStatus.Should().Be(-1);
    }

    [Fact]
    public void Create_WithZeroResponseStatus_ShouldWork()
    {
        var log = AuditLog.Create(
            ValidLogId, ValidOperatorId, ValidAction, ValidResourceType, ValidResourceId,
            ValidRequestSummary, responseStatus: 0, ValidIpAddress, ValidTraceId, ValidOccurredAt);

        log.ResponseStatus.Should().Be(0);
    }

    [Fact]
    public void Create_ShouldTrimActionResourceTypeAndResourceId()
    {
        var log = AuditLog.Create(
            ValidLogId, ValidOperatorId, "  Create  ", "  SystemConfig  ", "  config-123  ",
            ValidRequestSummary, 200, ValidIpAddress, ValidTraceId, ValidOccurredAt);

        log.Action.Should().Be("Create");
        log.ResourceType.Should().Be("SystemConfig");
        log.ResourceId.Should().Be("config-123");
    }

    [Fact]
    public void Create_WithWhitespaceOptionals_ShouldNormalizeToNull()
    {
        var log = AuditLog.Create(
            ValidLogId, ValidOperatorId, ValidAction, ValidResourceType, ValidResourceId,
            "   ", 200, "   ", "   ", ValidOccurredAt);

        log.RequestSummary.Should().BeNull();
        log.IpAddress.Should().BeNull();
        log.TraceId.Should().BeNull();
    }

    #endregion

    #region Create - Validation

    [Fact]
    public void Create_WithEmptyLogId_ShouldThrowAuditLogIdEmpty()
    {
        var act = () => AuditLog.Create(
            Guid.Empty, ValidOperatorId, ValidAction, ValidResourceType, ValidResourceId,
            ValidRequestSummary, 200, ValidIpAddress, ValidTraceId, ValidOccurredAt);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("AUDIT_LOG_ID_EMPTY");
    }

    [Fact]
    public void Create_WithEmptyOperatorId_ShouldThrowAuditOperatorEmpty()
    {
        var act = () => AuditLog.Create(
            ValidLogId, Guid.Empty, ValidAction, ValidResourceType, ValidResourceId,
            ValidRequestSummary, 200, ValidIpAddress, ValidTraceId, ValidOccurredAt);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("AUDIT_OPERATOR_EMPTY");
    }

    [Fact]
    public void Create_WithNullAction_ShouldThrowAuditActionEmpty()
    {
        var act = () => AuditLog.Create(
            ValidLogId, ValidOperatorId, null!, ValidResourceType, ValidResourceId,
            ValidRequestSummary, 200, ValidIpAddress, ValidTraceId, ValidOccurredAt);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("AUDIT_ACTION_EMPTY");
    }

    [Fact]
    public void Create_WithEmptyAction_ShouldThrowAuditActionEmpty()
    {
        var act = () => AuditLog.Create(
            ValidLogId, ValidOperatorId, "", ValidResourceType, ValidResourceId,
            ValidRequestSummary, 200, ValidIpAddress, ValidTraceId, ValidOccurredAt);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("AUDIT_ACTION_EMPTY");
    }

    [Fact]
    public void Create_WithActionTooLong_ShouldThrowAuditActionLength()
    {
        var longAction = new string('a', 129);

        var act = () => AuditLog.Create(
            ValidLogId, ValidOperatorId, longAction, ValidResourceType, ValidResourceId,
            ValidRequestSummary, 200, ValidIpAddress, ValidTraceId, ValidOccurredAt);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("AUDIT_ACTION_LENGTH");
    }

    [Fact]
    public void Create_WithActionAtMaxLength_ShouldSucceed()
    {
        var action = new string('a', 128);

        var log = AuditLog.Create(
            ValidLogId, ValidOperatorId, action, ValidResourceType, ValidResourceId,
            ValidRequestSummary, 200, ValidIpAddress, ValidTraceId, ValidOccurredAt);

        log.Action.Should().Be(action);
    }

    [Fact]
    public void Create_WithNullResourceType_ShouldThrowAuditResourceTypeEmpty()
    {
        var act = () => AuditLog.Create(
            ValidLogId, ValidOperatorId, ValidAction, null!, ValidResourceId,
            ValidRequestSummary, 200, ValidIpAddress, ValidTraceId, ValidOccurredAt);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("AUDIT_RESOURCE_TYPE_EMPTY");
    }

    [Fact]
    public void Create_WithEmptyResourceType_ShouldThrowAuditResourceTypeEmpty()
    {
        var act = () => AuditLog.Create(
            ValidLogId, ValidOperatorId, ValidAction, "", ValidResourceId,
            ValidRequestSummary, 200, ValidIpAddress, ValidTraceId, ValidOccurredAt);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("AUDIT_RESOURCE_TYPE_EMPTY");
    }

    [Fact]
    public void Create_WithResourceTypeTooLong_ShouldThrowAuditResourceTypeLength()
    {
        var longType = new string('t', 65);

        var act = () => AuditLog.Create(
            ValidLogId, ValidOperatorId, ValidAction, longType, ValidResourceId,
            ValidRequestSummary, 200, ValidIpAddress, ValidTraceId, ValidOccurredAt);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("AUDIT_RESOURCE_TYPE_LENGTH");
    }

    [Fact]
    public void Create_WithResourceTypeAtMaxLength_ShouldSucceed()
    {
        var resourceType = new string('t', 64);

        var log = AuditLog.Create(
            ValidLogId, ValidOperatorId, ValidAction, resourceType, ValidResourceId,
            ValidRequestSummary, 200, ValidIpAddress, ValidTraceId, ValidOccurredAt);

        log.ResourceType.Should().Be(resourceType);
    }

    [Fact]
    public void Create_WithNullResourceId_ShouldThrowAuditResourceIdEmpty()
    {
        var act = () => AuditLog.Create(
            ValidLogId, ValidOperatorId, ValidAction, ValidResourceType, null!,
            ValidRequestSummary, 200, ValidIpAddress, ValidTraceId, ValidOccurredAt);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("AUDIT_RESOURCE_ID_EMPTY");
    }

    [Fact]
    public void Create_WithEmptyResourceId_ShouldThrowAuditResourceIdEmpty()
    {
        var act = () => AuditLog.Create(
            ValidLogId, ValidOperatorId, ValidAction, ValidResourceType, "",
            ValidRequestSummary, 200, ValidIpAddress, ValidTraceId, ValidOccurredAt);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("AUDIT_RESOURCE_ID_EMPTY");
    }

    [Fact]
    public void Create_WithResourceIdTooLong_ShouldThrowAuditResourceIdLength()
    {
        var longId = new string('i', 65);

        var act = () => AuditLog.Create(
            ValidLogId, ValidOperatorId, ValidAction, ValidResourceType, longId,
            ValidRequestSummary, 200, ValidIpAddress, ValidTraceId, ValidOccurredAt);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("AUDIT_RESOURCE_ID_LENGTH");
    }

    [Fact]
    public void Create_WithResourceIdAtMaxLength_ShouldSucceed()
    {
        var resourceId = new string('i', 64);

        var log = AuditLog.Create(
            ValidLogId, ValidOperatorId, ValidAction, ValidResourceType, resourceId,
            ValidRequestSummary, 200, ValidIpAddress, ValidTraceId, ValidOccurredAt);

        log.ResourceId.Should().Be(resourceId);
    }

    [Fact]
    public void Create_WithRequestSummaryTooLong_ShouldThrowAuditRequestSummaryLength()
    {
        var longSummary = new string('s', 2001);

        var act = () => AuditLog.Create(
            ValidLogId, ValidOperatorId, ValidAction, ValidResourceType, ValidResourceId,
            longSummary, 200, ValidIpAddress, ValidTraceId, ValidOccurredAt);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("AUDIT_REQUEST_SUMMARY_LENGTH");
    }

    [Fact]
    public void Create_WithRequestSummaryAtMaxLength_ShouldSucceed()
    {
        var summary = new string('s', 2000);

        var log = AuditLog.Create(
            ValidLogId, ValidOperatorId, ValidAction, ValidResourceType, ValidResourceId,
            summary, 200, ValidIpAddress, ValidTraceId, ValidOccurredAt);

        log.RequestSummary.Should().Be(summary);
    }

    [Fact]
    public void Create_WithIpAddressTooLong_ShouldThrowAuditIpLength()
    {
        var longIp = new string('i', 65);

        var act = () => AuditLog.Create(
            ValidLogId, ValidOperatorId, ValidAction, ValidResourceType, ValidResourceId,
            ValidRequestSummary, 200, longIp, ValidTraceId, ValidOccurredAt);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("AUDIT_IP_LENGTH");
    }

    [Fact]
    public void Create_WithIpAddressAtMaxLength_ShouldSucceed()
    {
        var ip = new string('i', 64);

        var log = AuditLog.Create(
            ValidLogId, ValidOperatorId, ValidAction, ValidResourceType, ValidResourceId,
            ValidRequestSummary, 200, ip, ValidTraceId, ValidOccurredAt);

        log.IpAddress.Should().Be(ip);
    }

    [Fact]
    public void Create_WithTraceIdTooLong_ShouldThrowAuditTraceLength()
    {
        var longTrace = new string('t', 65);

        var act = () => AuditLog.Create(
            ValidLogId, ValidOperatorId, ValidAction, ValidResourceType, ValidResourceId,
            ValidRequestSummary, 200, ValidIpAddress, longTrace, ValidOccurredAt);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("AUDIT_TRACE_LENGTH");
    }

    [Fact]
    public void Create_WithTraceIdAtMaxLength_ShouldSucceed()
    {
        var traceId = new string('t', 64);

        var log = AuditLog.Create(
            ValidLogId, ValidOperatorId, ValidAction, ValidResourceType, ValidResourceId,
            ValidRequestSummary, 200, ValidIpAddress, traceId, ValidOccurredAt);

        log.TraceId.Should().Be(traceId);
    }

    [Fact]
    public void Create_WithDefaultOccurredAt_ShouldThrowAuditOccurredAtEmpty()
    {
        var act = () => AuditLog.Create(
            ValidLogId, ValidOperatorId, ValidAction, ValidResourceType, ValidResourceId,
            ValidRequestSummary, 200, ValidIpAddress, ValidTraceId, default);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("AUDIT_OCCURRED_AT_EMPTY");
    }

    #endregion
}