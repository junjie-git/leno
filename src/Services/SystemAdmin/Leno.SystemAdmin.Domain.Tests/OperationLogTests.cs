using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Exceptions;

namespace Leno.SystemAdmin.Domain.Tests;

public class OperationLogTests
{
    private static readonly Guid ValidLogId = Guid.NewGuid();
    private static readonly Guid ValidOperatorId = Guid.NewGuid();
    private const string ValidOperationType = "Update";
    private const string ValidModule = "SystemConfig";
    private const string ValidDescription = "Updated timeout setting";
    private const string ValidBeforeSnapshot = "{\"value\":\"30\"}";
    private const string ValidAfterSnapshot = "{\"value\":\"60\"}";
    private const string ValidIpAddress = "192.168.1.1";
    private static readonly DateTime ValidOccurredAt = DateTime.UtcNow;

    #region Create - Happy Path

    [Fact]
    public void Create_WithValidParameters_ShouldSetAllProperties()
    {
        var log = OperationLog.Create(
            ValidLogId, ValidOperatorId, ValidOperationType, ValidModule,
            ValidDescription, ValidBeforeSnapshot, ValidAfterSnapshot,
            ValidIpAddress, ValidOccurredAt);

        log.LogId.Should().Be(ValidLogId);
        log.Id.Should().Be(ValidLogId);
        log.OperatorId.Should().Be(ValidOperatorId);
        log.OperationType.Should().Be(ValidOperationType);
        log.Module.Should().Be(ValidModule);
        log.Description.Should().Be(ValidDescription);
        log.BeforeSnapshot.Should().Be(ValidBeforeSnapshot);
        log.AfterSnapshot.Should().Be(ValidAfterSnapshot);
        log.IpAddress.Should().Be(ValidIpAddress);
        log.OccurredAt.Should().Be(ValidOccurredAt);
    }

    [Fact]
    public void Create_WithMinimalParameters_ShouldSetNullsForOptionals()
    {
        var log = OperationLog.Create(
            ValidLogId, ValidOperatorId, ValidOperationType, ValidModule,
            description: null, beforeSnapshot: null, afterSnapshot: null,
            ipAddress: null, ValidOccurredAt);

        log.Description.Should().BeNull();
        log.BeforeSnapshot.Should().BeNull();
        log.AfterSnapshot.Should().BeNull();
        log.IpAddress.Should().BeNull();
    }

    [Fact]
    public void Create_WithWhitespaceOptionals_ShouldNormalizeToNull()
    {
        var log = OperationLog.Create(
            ValidLogId, ValidOperatorId, ValidOperationType, ValidModule,
            "   ", "   ", "   ", "   ", ValidOccurredAt);

        log.Description.Should().BeNull();
        log.BeforeSnapshot.Should().BeNull();
        log.AfterSnapshot.Should().BeNull();
        log.IpAddress.Should().BeNull();
    }

    [Fact]
    public void Create_ShouldTrimOperationTypeAndModule()
    {
        var log = OperationLog.Create(
            ValidLogId, ValidOperatorId, "  Update  ", "  SystemConfig  ",
            ValidDescription, ValidBeforeSnapshot, ValidAfterSnapshot,
            ValidIpAddress, ValidOccurredAt);

        log.OperationType.Should().Be("Update");
        log.Module.Should().Be("SystemConfig");
    }

    #endregion

    #region Create - Validation

    [Fact]
    public void Create_WithEmptyLogId_ShouldThrowOpLogIdEmpty()
    {
        var act = () => OperationLog.Create(
            Guid.Empty, ValidOperatorId, ValidOperationType, ValidModule,
            ValidDescription, ValidBeforeSnapshot, ValidAfterSnapshot,
            ValidIpAddress, ValidOccurredAt);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("OP_LOG_ID_EMPTY");
    }

    [Fact]
    public void Create_WithEmptyOperatorId_ShouldThrowOpLogOperatorEmpty()
    {
        var act = () => OperationLog.Create(
            ValidLogId, Guid.Empty, ValidOperationType, ValidModule,
            ValidDescription, ValidBeforeSnapshot, ValidAfterSnapshot,
            ValidIpAddress, ValidOccurredAt);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("OP_LOG_OPERATOR_EMPTY");
    }

    [Fact]
    public void Create_WithNullOperationType_ShouldThrowOpLogTypeEmpty()
    {
        var act = () => OperationLog.Create(
            ValidLogId, ValidOperatorId, null!, ValidModule,
            ValidDescription, ValidBeforeSnapshot, ValidAfterSnapshot,
            ValidIpAddress, ValidOccurredAt);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("OP_LOG_TYPE_EMPTY");
    }

    [Fact]
    public void Create_WithEmptyOperationType_ShouldThrowOpLogTypeEmpty()
    {
        var act = () => OperationLog.Create(
            ValidLogId, ValidOperatorId, "", ValidModule,
            ValidDescription, ValidBeforeSnapshot, ValidAfterSnapshot,
            ValidIpAddress, ValidOccurredAt);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("OP_LOG_TYPE_EMPTY");
    }

    [Fact]
    public void Create_WithOperationTypeTooLong_ShouldThrowOpLogTypeLength()
    {
        var longType = new string('t', 65);

        var act = () => OperationLog.Create(
            ValidLogId, ValidOperatorId, longType, ValidModule,
            ValidDescription, ValidBeforeSnapshot, ValidAfterSnapshot,
            ValidIpAddress, ValidOccurredAt);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("OP_LOG_TYPE_LENGTH");
    }

    [Fact]
    public void Create_WithOperationTypeAtMaxLength_ShouldSucceed()
    {
        var type = new string('t', 64);

        var log = OperationLog.Create(
            ValidLogId, ValidOperatorId, type, ValidModule,
            ValidDescription, ValidBeforeSnapshot, ValidAfterSnapshot,
            ValidIpAddress, ValidOccurredAt);

        log.OperationType.Should().Be(type);
    }

    [Fact]
    public void Create_WithNullModule_ShouldThrowOpLogModuleEmpty()
    {
        var act = () => OperationLog.Create(
            ValidLogId, ValidOperatorId, ValidOperationType, null!,
            ValidDescription, ValidBeforeSnapshot, ValidAfterSnapshot,
            ValidIpAddress, ValidOccurredAt);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("OP_LOG_MODULE_EMPTY");
    }

    [Fact]
    public void Create_WithEmptyModule_ShouldThrowOpLogModuleEmpty()
    {
        var act = () => OperationLog.Create(
            ValidLogId, ValidOperatorId, ValidOperationType, "",
            ValidDescription, ValidBeforeSnapshot, ValidAfterSnapshot,
            ValidIpAddress, ValidOccurredAt);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("OP_LOG_MODULE_EMPTY");
    }

    [Fact]
    public void Create_WithModuleTooLong_ShouldThrowOpLogModuleLength()
    {
        var longModule = new string('m', 65);

        var act = () => OperationLog.Create(
            ValidLogId, ValidOperatorId, ValidOperationType, longModule,
            ValidDescription, ValidBeforeSnapshot, ValidAfterSnapshot,
            ValidIpAddress, ValidOccurredAt);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("OP_LOG_MODULE_LENGTH");
    }

    [Fact]
    public void Create_WithModuleAtMaxLength_ShouldSucceed()
    {
        var module = new string('m', 64);

        var log = OperationLog.Create(
            ValidLogId, ValidOperatorId, ValidOperationType, module,
            ValidDescription, ValidBeforeSnapshot, ValidAfterSnapshot,
            ValidIpAddress, ValidOccurredAt);

        log.Module.Should().Be(module);
    }

    [Fact]
    public void Create_WithDescriptionTooLong_ShouldThrowOpLogDescLength()
    {
        var longDesc = new string('d', 501);

        var act = () => OperationLog.Create(
            ValidLogId, ValidOperatorId, ValidOperationType, ValidModule,
            longDesc, ValidBeforeSnapshot, ValidAfterSnapshot,
            ValidIpAddress, ValidOccurredAt);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("OP_LOG_DESC_LENGTH");
    }

    [Fact]
    public void Create_WithDescriptionAtMaxLength_ShouldSucceed()
    {
        var desc = new string('d', 500);

        var log = OperationLog.Create(
            ValidLogId, ValidOperatorId, ValidOperationType, ValidModule,
            desc, ValidBeforeSnapshot, ValidAfterSnapshot,
            ValidIpAddress, ValidOccurredAt);

        log.Description.Should().Be(desc);
    }

    [Fact]
    public void Create_WithBeforeSnapshotTooLong_ShouldThrowOpLogSnapshotLength()
    {
        var longSnapshot = new string('s', 4001);

        var act = () => OperationLog.Create(
            ValidLogId, ValidOperatorId, ValidOperationType, ValidModule,
            ValidDescription, longSnapshot, ValidAfterSnapshot,
            ValidIpAddress, ValidOccurredAt);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("OP_LOG_SNAPSHOT_LENGTH");
    }

    [Fact]
    public void Create_WithAfterSnapshotTooLong_ShouldThrowOpLogSnapshotLength()
    {
        var longSnapshot = new string('s', 4001);

        var act = () => OperationLog.Create(
            ValidLogId, ValidOperatorId, ValidOperationType, ValidModule,
            ValidDescription, ValidBeforeSnapshot, longSnapshot,
            ValidIpAddress, ValidOccurredAt);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("OP_LOG_SNAPSHOT_LENGTH");
    }

    [Fact]
    public void Create_WithSnapshotAtMaxLength_ShouldSucceed()
    {
        var snapshot = new string('s', 4000);

        var log = OperationLog.Create(
            ValidLogId, ValidOperatorId, ValidOperationType, ValidModule,
            ValidDescription, snapshot, snapshot,
            ValidIpAddress, ValidOccurredAt);

        log.BeforeSnapshot.Should().Be(snapshot);
        log.AfterSnapshot.Should().Be(snapshot);
    }

    [Fact]
    public void Create_WithIpAddressTooLong_ShouldThrowOpLogIpLength()
    {
        var longIp = new string('i', 65);

        var act = () => OperationLog.Create(
            ValidLogId, ValidOperatorId, ValidOperationType, ValidModule,
            ValidDescription, ValidBeforeSnapshot, ValidAfterSnapshot,
            longIp, ValidOccurredAt);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("OP_LOG_IP_LENGTH");
    }

    [Fact]
    public void Create_WithIpAddressAtMaxLength_ShouldSucceed()
    {
        var ip = new string('i', 64);

        var log = OperationLog.Create(
            ValidLogId, ValidOperatorId, ValidOperationType, ValidModule,
            ValidDescription, ValidBeforeSnapshot, ValidAfterSnapshot,
            ip, ValidOccurredAt);

        log.IpAddress.Should().Be(ip);
    }

    [Fact]
    public void Create_WithDefaultOccurredAt_ShouldThrowOpLogOccurredAtEmpty()
    {
        var act = () => OperationLog.Create(
            ValidLogId, ValidOperatorId, ValidOperationType, ValidModule,
            ValidDescription, ValidBeforeSnapshot, ValidAfterSnapshot,
            ValidIpAddress, default);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("OP_LOG_OCCURRED_AT_EMPTY");
    }

    #endregion
}