using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Exceptions;

namespace Leno.SystemAdmin.Domain.Tests;

public class AuditLogEntryTests
{
    private static readonly Guid ValidEntryId = Guid.NewGuid();
    private static readonly Guid ValidEventId = Guid.NewGuid();
    private const string ValidEventType = "OrderCreatedEvent";
    private static readonly Guid ValidAggregateId = Guid.NewGuid();
    private const string ValidModule = "Order";
    private const string ValidAction = "OrderCreated";
    private static readonly Guid ValidOperatorId = Guid.NewGuid();
    private const string ValidOperatorName = "TestUser";
    private const string ValidRequestSummary = "订单创建 金额=100.00 CNY";
    private const string ValidIpAddress = "10.0.0.1";
    private static readonly DateTime ValidTimestamp = DateTime.UtcNow;

    #region Create - Happy Path

    [Fact]
    public void Create_WithValidParameters_ShouldSetAllProperties()
    {
        var entry = AuditLogEntry.Create(
            ValidEntryId, ValidEventId, ValidEventType, ValidAggregateId, ValidModule,
            ValidAction, ValidOperatorId, ValidOperatorName, ValidRequestSummary,
            ValidTimestamp, ValidIpAddress);

        entry.EntryId.Should().Be(ValidEntryId);
        entry.Id.Should().Be(ValidEntryId);
        entry.EventId.Should().Be(ValidEventId);
        entry.EventType.Should().Be(ValidEventType);
        entry.AggregateId.Should().Be(ValidAggregateId);
        entry.Module.Should().Be(ValidModule);
        entry.Action.Should().Be(ValidAction);
        entry.OperatorId.Should().Be(ValidOperatorId);
        entry.OperatorName.Should().Be(ValidOperatorName);
        entry.RequestSummary.Should().Be(ValidRequestSummary);
        entry.Timestamp.Should().Be(ValidTimestamp);
        entry.IpAddress.Should().Be(ValidIpAddress);
    }

    [Fact]
    public void Create_WithMinimalParameters_ShouldSetNullsForOptionals()
    {
        var entry = AuditLogEntry.Create(
            ValidEntryId, ValidEventId, ValidEventType, ValidAggregateId, ValidModule,
            ValidAction, Guid.Empty, operatorName: null, requestSummary: null,
            ValidTimestamp, ipAddress: null);

        entry.OperatorId.Should().Be(Guid.Empty);
        entry.OperatorName.Should().BeNull();
        entry.RequestSummary.Should().BeNull();
        entry.IpAddress.Should().BeNull();
    }

    [Fact]
    public void Create_ShouldTrimStringFields()
    {
        var entry = AuditLogEntry.Create(
            ValidEntryId, ValidEventId, "  OrderCreatedEvent  ", ValidAggregateId,
            "  Order  ", "  OrderCreated  ", ValidOperatorId,
            "  TestUser  ", "  summary  ", ValidTimestamp, "  10.0.0.1  ");

        entry.EventType.Should().Be("OrderCreatedEvent");
        entry.Module.Should().Be("Order");
        entry.Action.Should().Be("OrderCreated");
        entry.OperatorName.Should().Be("TestUser");
        entry.RequestSummary.Should().Be("summary");
        entry.IpAddress.Should().Be("10.0.0.1");
    }

    [Fact]
    public void Create_WithWhitespaceOptionals_ShouldNormalizeToNull()
    {
        var entry = AuditLogEntry.Create(
            ValidEntryId, ValidEventId, ValidEventType, ValidAggregateId, ValidModule,
            ValidAction, ValidOperatorId, "   ", "   ", ValidTimestamp, "   ");

        entry.OperatorName.Should().BeNull();
        entry.RequestSummary.Should().BeNull();
        entry.IpAddress.Should().BeNull();
    }

    [Fact]
    public void Create_WithEmptyOperatorId_ShouldSucceed()
    {
        var entry = AuditLogEntry.Create(
            ValidEntryId, ValidEventId, ValidEventType, ValidAggregateId, ValidModule,
            ValidAction, Guid.Empty, ValidOperatorName, ValidRequestSummary,
            ValidTimestamp, ValidIpAddress);

        entry.OperatorId.Should().Be(Guid.Empty);
    }

    #endregion

    #region Create - Validation

    [Fact]
    public void Create_WithEmptyEntryId_ShouldThrowAuditEntryIdEmpty()
    {
        var act = () => AuditLogEntry.Create(
            Guid.Empty, ValidEventId, ValidEventType, ValidAggregateId, ValidModule,
            ValidAction, ValidOperatorId, ValidOperatorName, ValidRequestSummary,
            ValidTimestamp, ValidIpAddress);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("AUDIT_ENTRY_ID_EMPTY");
    }

    [Fact]
    public void Create_WithEmptyEventId_ShouldThrowAuditEntryEventIdEmpty()
    {
        var act = () => AuditLogEntry.Create(
            ValidEntryId, Guid.Empty, ValidEventType, ValidAggregateId, ValidModule,
            ValidAction, ValidOperatorId, ValidOperatorName, ValidRequestSummary,
            ValidTimestamp, ValidIpAddress);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("AUDIT_ENTRY_EVENT_ID_EMPTY");
    }

    [Fact]
    public void Create_WithNullEventType_ShouldThrowAuditEntryEventTypeEmpty()
    {
        var act = () => AuditLogEntry.Create(
            ValidEntryId, ValidEventId, null!, ValidAggregateId, ValidModule,
            ValidAction, ValidOperatorId, ValidOperatorName, ValidRequestSummary,
            ValidTimestamp, ValidIpAddress);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("AUDIT_ENTRY_EVENT_TYPE_EMPTY");
    }

    [Fact]
    public void Create_WithEmptyEventType_ShouldThrowAuditEntryEventTypeEmpty()
    {
        var act = () => AuditLogEntry.Create(
            ValidEntryId, ValidEventId, "", ValidAggregateId, ValidModule,
            ValidAction, ValidOperatorId, ValidOperatorName, ValidRequestSummary,
            ValidTimestamp, ValidIpAddress);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("AUDIT_ENTRY_EVENT_TYPE_EMPTY");
    }

    [Fact]
    public void Create_WithEventTypeTooLong_ShouldThrowAuditEntryEventTypeLength()
    {
        var longType = new string('e', 129);

        var act = () => AuditLogEntry.Create(
            ValidEntryId, ValidEventId, longType, ValidAggregateId, ValidModule,
            ValidAction, ValidOperatorId, ValidOperatorName, ValidRequestSummary,
            ValidTimestamp, ValidIpAddress);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("AUDIT_ENTRY_EVENT_TYPE_LENGTH");
    }

    [Fact]
    public void Create_WithEventTypeAtMaxLength_ShouldSucceed()
    {
        var eventType = new string('e', 128);

        var entry = AuditLogEntry.Create(
            ValidEntryId, ValidEventId, eventType, ValidAggregateId, ValidModule,
            ValidAction, ValidOperatorId, ValidOperatorName, ValidRequestSummary,
            ValidTimestamp, ValidIpAddress);

        entry.EventType.Should().Be(eventType);
    }

    [Fact]
    public void Create_WithNullModule_ShouldThrowAuditEntryModuleEmpty()
    {
        var act = () => AuditLogEntry.Create(
            ValidEntryId, ValidEventId, ValidEventType, ValidAggregateId, null!,
            ValidAction, ValidOperatorId, ValidOperatorName, ValidRequestSummary,
            ValidTimestamp, ValidIpAddress);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("AUDIT_ENTRY_MODULE_EMPTY");
    }

    [Fact]
    public void Create_WithEmptyModule_ShouldThrowAuditEntryModuleEmpty()
    {
        var act = () => AuditLogEntry.Create(
            ValidEntryId, ValidEventId, ValidEventType, ValidAggregateId, "",
            ValidAction, ValidOperatorId, ValidOperatorName, ValidRequestSummary,
            ValidTimestamp, ValidIpAddress);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("AUDIT_ENTRY_MODULE_EMPTY");
    }

    [Fact]
    public void Create_WithModuleTooLong_ShouldThrowAuditEntryModuleLength()
    {
        var longModule = new string('m', 65);

        var act = () => AuditLogEntry.Create(
            ValidEntryId, ValidEventId, ValidEventType, ValidAggregateId, longModule,
            ValidAction, ValidOperatorId, ValidOperatorName, ValidRequestSummary,
            ValidTimestamp, ValidIpAddress);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("AUDIT_ENTRY_MODULE_LENGTH");
    }

    [Fact]
    public void Create_WithModuleAtMaxLength_ShouldSucceed()
    {
        var module = new string('m', 64);

        var entry = AuditLogEntry.Create(
            ValidEntryId, ValidEventId, ValidEventType, ValidAggregateId, module,
            ValidAction, ValidOperatorId, ValidOperatorName, ValidRequestSummary,
            ValidTimestamp, ValidIpAddress);

        entry.Module.Should().Be(module);
    }

    [Fact]
    public void Create_WithNullAction_ShouldThrowAuditEntryActionEmpty()
    {
        var act = () => AuditLogEntry.Create(
            ValidEntryId, ValidEventId, ValidEventType, ValidAggregateId, ValidModule,
            null!, ValidOperatorId, ValidOperatorName, ValidRequestSummary,
            ValidTimestamp, ValidIpAddress);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("AUDIT_ENTRY_ACTION_EMPTY");
    }

    [Fact]
    public void Create_WithEmptyAction_ShouldThrowAuditEntryActionEmpty()
    {
        var act = () => AuditLogEntry.Create(
            ValidEntryId, ValidEventId, ValidEventType, ValidAggregateId, ValidModule,
            "", ValidOperatorId, ValidOperatorName, ValidRequestSummary,
            ValidTimestamp, ValidIpAddress);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("AUDIT_ENTRY_ACTION_EMPTY");
    }

    [Fact]
    public void Create_WithActionTooLong_ShouldThrowAuditEntryActionLength()
    {
        var longAction = new string('a', 129);

        var act = () => AuditLogEntry.Create(
            ValidEntryId, ValidEventId, ValidEventType, ValidAggregateId, ValidModule,
            longAction, ValidOperatorId, ValidOperatorName, ValidRequestSummary,
            ValidTimestamp, ValidIpAddress);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("AUDIT_ENTRY_ACTION_LENGTH");
    }

    [Fact]
    public void Create_WithActionAtMaxLength_ShouldSucceed()
    {
        var action = new string('a', 128);

        var entry = AuditLogEntry.Create(
            ValidEntryId, ValidEventId, ValidEventType, ValidAggregateId, ValidModule,
            action, ValidOperatorId, ValidOperatorName, ValidRequestSummary,
            ValidTimestamp, ValidIpAddress);

        entry.Action.Should().Be(action);
    }

    [Fact]
    public void Create_WithOperatorNameTooLong_ShouldThrowAuditEntryOperatorNameLength()
    {
        var longName = new string('n', 129);

        var act = () => AuditLogEntry.Create(
            ValidEntryId, ValidEventId, ValidEventType, ValidAggregateId, ValidModule,
            ValidAction, ValidOperatorId, longName, ValidRequestSummary,
            ValidTimestamp, ValidIpAddress);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("AUDIT_ENTRY_OPERATOR_NAME_LENGTH");
    }

    [Fact]
    public void Create_WithOperatorNameAtMaxLength_ShouldSucceed()
    {
        var name = new string('n', 128);

        var entry = AuditLogEntry.Create(
            ValidEntryId, ValidEventId, ValidEventType, ValidAggregateId, ValidModule,
            ValidAction, ValidOperatorId, name, ValidRequestSummary,
            ValidTimestamp, ValidIpAddress);

        entry.OperatorName.Should().Be(name);
    }

    [Fact]
    public void Create_WithRequestSummaryTooLong_ShouldThrowAuditEntryRequestSummaryLength()
    {
        var longSummary = new string('s', 2001);

        var act = () => AuditLogEntry.Create(
            ValidEntryId, ValidEventId, ValidEventType, ValidAggregateId, ValidModule,
            ValidAction, ValidOperatorId, ValidOperatorName, longSummary,
            ValidTimestamp, ValidIpAddress);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("AUDIT_ENTRY_REQUEST_SUMMARY_LENGTH");
    }

    [Fact]
    public void Create_WithRequestSummaryAtMaxLength_ShouldSucceed()
    {
        var summary = new string('s', 2000);

        var entry = AuditLogEntry.Create(
            ValidEntryId, ValidEventId, ValidEventType, ValidAggregateId, ValidModule,
            ValidAction, ValidOperatorId, ValidOperatorName, summary,
            ValidTimestamp, ValidIpAddress);

        entry.RequestSummary.Should().Be(summary);
    }

    [Fact]
    public void Create_WithIpAddressTooLong_ShouldThrowAuditEntryIpLength()
    {
        var longIp = new string('i', 65);

        var act = () => AuditLogEntry.Create(
            ValidEntryId, ValidEventId, ValidEventType, ValidAggregateId, ValidModule,
            ValidAction, ValidOperatorId, ValidOperatorName, ValidRequestSummary,
            ValidTimestamp, longIp);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("AUDIT_ENTRY_IP_LENGTH");
    }

    [Fact]
    public void Create_WithIpAddressAtMaxLength_ShouldSucceed()
    {
        var ip = new string('i', 64);

        var entry = AuditLogEntry.Create(
            ValidEntryId, ValidEventId, ValidEventType, ValidAggregateId, ValidModule,
            ValidAction, ValidOperatorId, ValidOperatorName, ValidRequestSummary,
            ValidTimestamp, ip);

        entry.IpAddress.Should().Be(ip);
    }

    [Fact]
    public void Create_WithDefaultTimestamp_ShouldThrowAuditEntryTimestampEmpty()
    {
        var act = () => AuditLogEntry.Create(
            ValidEntryId, ValidEventId, ValidEventType, ValidAggregateId, ValidModule,
            ValidAction, ValidOperatorId, ValidOperatorName, ValidRequestSummary,
            default, ValidIpAddress);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("AUDIT_ENTRY_TIMESTAMP_EMPTY");
    }

    #endregion

    #region Immutability

    [Fact]
    public void AuditLogEntry_ShouldBeImmutable_NoPublicSetters()
    {
        var type = typeof(AuditLogEntry);
        var properties = type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

        foreach (var prop in properties)
        {
            if (prop.Name is nameof(AuditLogEntry.EntryId)
                or nameof(AuditLogEntry.CreatedAt) or nameof(AuditLogEntry.UpdatedAt)
                or nameof(AuditLogEntry.CreatedBy) or nameof(AuditLogEntry.UpdatedBy)
                or nameof(AuditLogEntry.DomainEvents))
            {
                continue;
            }

            var setter = prop.GetSetMethod(nonPublic: false);
            setter.Should().BeNull($"property {prop.Name} should not have a public setter");
        }
    }

    #endregion
}