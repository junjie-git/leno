using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Exceptions;

namespace Leno.SystemAdmin.Domain.Tests;

/// <summary>
/// Outbox 归档历史聚合根单元测试，覆盖工厂创建与字段校验。
/// </summary>
public class OutboxArchiveRecordTests
{
    private static readonly Guid ValidRecordId = Guid.NewGuid();
    private const string ValidContext = "Order";
    private const int ValidArchivedCount = 42;
    private static readonly DateTime ValidArchivedBefore = DateTime.UtcNow.AddHours(-24);
    private static readonly DateTime ValidArchivedAt = DateTime.UtcNow;
    private const string ValidArchivedBy = "op-001";
    private const string ValidReason = "陈旧积压事件归档清理";

    #region Create - Happy Path

    [Fact]
    public void Create_WithValidParameters_ShouldSetAllProperties()
    {
        var record = OutboxArchiveRecord.Create(
            ValidRecordId,
            ValidContext,
            ValidArchivedCount,
            ValidArchivedBefore,
            ValidArchivedAt,
            ValidArchivedBy,
            ValidReason);

        record.Id.Should().Be(ValidRecordId);
        record.Context.Should().Be(ValidContext);
        record.ArchivedCount.Should().Be(ValidArchivedCount);
        record.ArchivedBefore.Should().Be(ValidArchivedBefore);
        record.ArchivedAt.Should().Be(ValidArchivedAt);
        record.ArchivedBy.Should().Be(ValidArchivedBy);
        record.Reason.Should().Be(ValidReason);
    }

    [Fact]
    public void Create_ShouldTrimFields()
    {
        var record = OutboxArchiveRecord.Create(
            ValidRecordId,
            "  " + ValidContext + "  ",
            ValidArchivedCount,
            ValidArchivedBefore,
            ValidArchivedAt,
            "  " + ValidArchivedBy + "  ",
            "  " + ValidReason + "  ");

        record.Context.Should().Be(ValidContext);
        record.ArchivedBy.Should().Be(ValidArchivedBy);
        record.Reason.Should().Be(ValidReason);
    }

    [Fact]
    public void Create_WithZeroArchivedCount_ShouldBeAllowed()
    {
        var record = OutboxArchiveRecord.Create(
            ValidRecordId,
            ValidContext,
            0,
            ValidArchivedBefore,
            ValidArchivedAt,
            ValidArchivedBy,
            ValidReason);

        record.ArchivedCount.Should().Be(0);
    }

    #endregion

    #region Create - Validation

    [Fact]
    public void Create_WithEmptyId_ShouldThrowIdEmpty()
    {
        var act = () => OutboxArchiveRecord.Create(
            Guid.Empty,
            ValidContext,
            ValidArchivedCount,
            ValidArchivedBefore,
            ValidArchivedAt,
            ValidArchivedBy,
            ValidReason);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("OUTBOX_ARCHIVE_ID_EMPTY");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithInvalidContext_ShouldThrowContextEmpty(string? context)
    {
        var act = () => OutboxArchiveRecord.Create(
            ValidRecordId,
            context!,
            ValidArchivedCount,
            ValidArchivedBefore,
            ValidArchivedAt,
            ValidArchivedBy,
            ValidReason);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("OUTBOX_ARCHIVE_CONTEXT_EMPTY");
    }

    [Fact]
    public void Create_WithTooLongContext_ShouldThrowContextLength()
    {
        var context = new string('c', 129);

        var act = () => OutboxArchiveRecord.Create(
            ValidRecordId,
            context,
            ValidArchivedCount,
            ValidArchivedBefore,
            ValidArchivedAt,
            ValidArchivedBy,
            ValidReason);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("OUTBOX_ARCHIVE_CONTEXT_LENGTH");
    }

    [Fact]
    public void Create_WithNegativeArchivedCount_ShouldThrowCountNegative()
    {
        var act = () => OutboxArchiveRecord.Create(
            ValidRecordId,
            ValidContext,
            -1,
            ValidArchivedBefore,
            ValidArchivedAt,
            ValidArchivedBy,
            ValidReason);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("OUTBOX_ARCHIVE_COUNT_NEGATIVE");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithInvalidArchivedBy_ShouldThrowByEmpty(string? archivedBy)
    {
        var act = () => OutboxArchiveRecord.Create(
            ValidRecordId,
            ValidContext,
            ValidArchivedCount,
            ValidArchivedBefore,
            ValidArchivedAt,
            archivedBy!,
            ValidReason);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("OUTBOX_ARCHIVE_BY_EMPTY");
    }

    [Fact]
    public void Create_WithTooLongArchivedBy_ShouldThrowByLength()
    {
        var archivedBy = new string('o', 65);

        var act = () => OutboxArchiveRecord.Create(
            ValidRecordId,
            ValidContext,
            ValidArchivedCount,
            ValidArchivedBefore,
            ValidArchivedAt,
            archivedBy,
            ValidReason);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("OUTBOX_ARCHIVE_BY_LENGTH");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithInvalidReason_ShouldThrowReasonEmpty(string? reason)
    {
        var act = () => OutboxArchiveRecord.Create(
            ValidRecordId,
            ValidContext,
            ValidArchivedCount,
            ValidArchivedBefore,
            ValidArchivedAt,
            ValidArchivedBy,
            reason!);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("OUTBOX_ARCHIVE_REASON_EMPTY");
    }

    [Fact]
    public void Create_WithTooLongReason_ShouldThrowReasonLength()
    {
        var reason = new string('r', 1001);

        var act = () => OutboxArchiveRecord.Create(
            ValidRecordId,
            ValidContext,
            ValidArchivedCount,
            ValidArchivedBefore,
            ValidArchivedAt,
            ValidArchivedBy,
            reason);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("OUTBOX_ARCHIVE_REASON_LENGTH");
    }

    #endregion
}
