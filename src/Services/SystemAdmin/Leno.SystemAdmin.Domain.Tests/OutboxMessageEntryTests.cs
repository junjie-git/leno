using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Exceptions;

namespace Leno.SystemAdmin.Domain.Tests;

/// <summary>
/// Outbox 消息聚合根单元测试，覆盖工厂创建、积压判断、积压时长计算与字段校验。
/// </summary>
public class OutboxMessageEntryTests
{
    private static readonly Guid ValidMessageId = Guid.NewGuid();
    private static readonly Guid ValidAggregateId = Guid.NewGuid();
    private const string ValidContext = "Order";
    private const string ValidEventType = "OrderCreatedIntegrationEvent";
    private const string ValidPayload = "{\"orderId\":\"123\"}";
    private const string ValidStatus = "Pending";
    private static readonly DateTime ValidCreatedAt = DateTime.UtcNow.AddMinutes(-30);

    #region Create - Happy Path

    [Fact]
    public void Create_WithValidParameters_ShouldSetAllProperties()
    {
        var entry = OutboxMessageEntry.Create(
            ValidMessageId,
            ValidContext,
            ValidAggregateId,
            ValidEventType,
            ValidPayload,
            ValidStatus,
            retryCount: 2,
            error: "上次处理超时",
            createdAt: ValidCreatedAt,
            processedAt: null);

        entry.Id.Should().Be(ValidMessageId);
        entry.Context.Should().Be(ValidContext);
        entry.AggregateId.Should().Be(ValidAggregateId);
        entry.EventType.Should().Be(ValidEventType);
        entry.Payload.Should().Be(ValidPayload);
        entry.Status.Should().Be(ValidStatus);
        entry.RetryCount.Should().Be(2);
        entry.Error.Should().Be("上次处理超时");
        entry.CreatedAt.Should().Be(ValidCreatedAt);
        entry.ProcessedAt.Should().BeNull();
    }

    [Fact]
    public void Create_ShouldTrimFields()
    {
        var entry = OutboxMessageEntry.Create(
            ValidMessageId,
            "  " + ValidContext + "  ",
            ValidAggregateId,
            "  " + ValidEventType + "  ",
            ValidPayload,
            "  " + ValidStatus + "  ",
            0,
            "  错误信息  ",
            ValidCreatedAt,
            null);

        entry.Context.Should().Be(ValidContext);
        entry.EventType.Should().Be(ValidEventType);
        entry.Status.Should().Be(ValidStatus);
        entry.Error.Should().Be("错误信息");
    }

    [Fact]
    public void Create_WithWhitespaceError_ShouldNormalizeToNull()
    {
        var entry = OutboxMessageEntry.Create(
            ValidMessageId,
            ValidContext,
            ValidAggregateId,
            ValidEventType,
            ValidPayload,
            ValidStatus,
            0,
            "   ",
            ValidCreatedAt,
            null);

        entry.Error.Should().BeNull();
    }

    [Fact]
    public void Create_WithNullError_ShouldRemainNull()
    {
        var entry = OutboxMessageEntry.Create(
            ValidMessageId,
            ValidContext,
            ValidAggregateId,
            ValidEventType,
            ValidPayload,
            ValidStatus,
            0,
            null,
            ValidCreatedAt,
            null);

        entry.Error.Should().BeNull();
    }

    [Fact]
    public void Create_WithProcessedAt_ShouldSetProcessedAt()
    {
        var processedAt = DateTime.UtcNow.AddMinutes(-5);
        var entry = OutboxMessageEntry.Create(
            ValidMessageId,
            ValidContext,
            ValidAggregateId,
            ValidEventType,
            ValidPayload,
            "Processed",
            1,
            null,
            ValidCreatedAt,
            processedAt);

        entry.ProcessedAt.Should().Be(processedAt);
    }

    #endregion

    #region Create - Validation

    [Fact]
    public void Create_WithEmptyId_ShouldThrowIdEmpty()
    {
        var act = () => OutboxMessageEntry.Create(
            Guid.Empty,
            ValidContext,
            ValidAggregateId,
            ValidEventType,
            ValidPayload,
            ValidStatus,
            0,
            null,
            ValidCreatedAt,
            null);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("OUTBOX_MESSAGE_ID_EMPTY");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithInvalidContext_ShouldThrowContextEmpty(string? context)
    {
        var act = () => OutboxMessageEntry.Create(
            ValidMessageId,
            context!,
            ValidAggregateId,
            ValidEventType,
            ValidPayload,
            ValidStatus,
            0,
            null,
            ValidCreatedAt,
            null);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("OUTBOX_MESSAGE_CONTEXT_EMPTY");
    }

    [Fact]
    public void Create_WithTooLongContext_ShouldThrowContextLength()
    {
        var context = new string('c', 129);

        var act = () => OutboxMessageEntry.Create(
            ValidMessageId,
            context,
            ValidAggregateId,
            ValidEventType,
            ValidPayload,
            ValidStatus,
            0,
            null,
            ValidCreatedAt,
            null);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("OUTBOX_MESSAGE_CONTEXT_LENGTH");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithInvalidEventType_ShouldThrowEventTypeEmpty(string? eventType)
    {
        var act = () => OutboxMessageEntry.Create(
            ValidMessageId,
            ValidContext,
            ValidAggregateId,
            eventType!,
            ValidPayload,
            ValidStatus,
            0,
            null,
            ValidCreatedAt,
            null);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("OUTBOX_MESSAGE_EVENT_TYPE_EMPTY");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithInvalidPayload_ShouldThrowPayloadEmpty(string? payload)
    {
        var act = () => OutboxMessageEntry.Create(
            ValidMessageId,
            ValidContext,
            ValidAggregateId,
            ValidEventType,
            payload!,
            ValidStatus,
            0,
            null,
            ValidCreatedAt,
            null);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("OUTBOX_MESSAGE_PAYLOAD_EMPTY");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithInvalidStatus_ShouldThrowStatusEmpty(string? status)
    {
        var act = () => OutboxMessageEntry.Create(
            ValidMessageId,
            ValidContext,
            ValidAggregateId,
            ValidEventType,
            ValidPayload,
            status!,
            0,
            null,
            ValidCreatedAt,
            null);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("OUTBOX_MESSAGE_STATUS_EMPTY");
    }

    [Fact]
    public void Create_WithNegativeRetryCount_ShouldThrowRetryNegative()
    {
        var act = () => OutboxMessageEntry.Create(
            ValidMessageId,
            ValidContext,
            ValidAggregateId,
            ValidEventType,
            ValidPayload,
            ValidStatus,
            -1,
            null,
            ValidCreatedAt,
            null);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("OUTBOX_MESSAGE_RETRY_NEGATIVE");
    }

    #endregion

    #region IsBacklog

    [Theory]
    [InlineData("Pending")]
    [InlineData("Publishing")]
    [InlineData("DeadLetter")]
    public void IsBacklog_WithBacklogStatuses_ShouldReturnTrue(string status)
    {
        var entry = CreateEntry(status, processedAt: null);

        entry.IsBacklog().Should().BeTrue();
    }

    [Fact]
    public void IsBacklog_WithProcessedStatus_ShouldReturnFalse()
    {
        var entry = CreateEntry("Processed", DateTime.UtcNow);

        entry.IsBacklog().Should().BeFalse();
    }

    [Fact]
    public void IsBacklog_WithDeadLetterAndProcessedAt_ShouldReturnFalse()
    {
        // DeadLetter 且 ProcessedAt 非空表示已处理，不算积压
        var entry = CreateEntry("DeadLetter", DateTime.UtcNow);

        entry.IsBacklog().Should().BeFalse();
    }

    [Fact]
    public void IsBacklog_WithUnknownStatus_ShouldReturnFalse()
    {
        var entry = CreateEntry("Unknown", null);

        entry.IsBacklog().Should().BeFalse();
    }

    #endregion

    #region GetBacklogAgeMinutes

    [Fact]
    public void GetBacklogAgeMinutes_WithBacklogStatus_ShouldReturnAge()
    {
        var createdAt = DateTime.UtcNow.AddMinutes(-30);
        var entry = OutboxMessageEntry.Create(
            ValidMessageId,
            ValidContext,
            ValidAggregateId,
            ValidEventType,
            ValidPayload,
            "Pending",
            0,
            null,
            createdAt,
            null);

        var age = entry.GetBacklogAgeMinutes();

        age.Should().BeGreaterThanOrEqualTo(29);
        age.Should().BeLessThanOrEqualTo(31);
    }

    [Fact]
    public void GetBacklogAgeMinutes_WithNonBacklogStatus_ShouldReturnZero()
    {
        var entry = CreateEntry("Processed", DateTime.UtcNow);

        entry.GetBacklogAgeMinutes().Should().Be(0);
    }

    [Fact]
    public void GetBacklogAgeMinutes_WithFutureCreatedAt_ShouldReturnZero()
    {
        // 异常情况：CreatedAt 在未来（时钟漂移），返回 0 而非负数
        var futureCreatedAt = DateTime.UtcNow.AddMinutes(10);
        var entry = OutboxMessageEntry.Create(
            ValidMessageId,
            ValidContext,
            ValidAggregateId,
            ValidEventType,
            ValidPayload,
            "Pending",
            0,
            null,
            futureCreatedAt,
            null);

        entry.GetBacklogAgeMinutes().Should().Be(0);
    }

    [Fact]
    public void GetBacklogAgeMinutes_WithExplicitAt_ShouldUseProvidedTime()
    {
        var createdAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var entry = OutboxMessageEntry.Create(
            ValidMessageId,
            ValidContext,
            ValidAggregateId,
            ValidEventType,
            ValidPayload,
            "Pending",
            0,
            null,
            createdAt,
            null);

        var at = new DateTime(2026, 1, 1, 0, 30, 0, DateTimeKind.Utc);
        entry.GetBacklogAgeMinutes(at).Should().Be(30);
    }

    #endregion

    private static OutboxMessageEntry CreateEntry(string status, DateTime? processedAt = null)
        => OutboxMessageEntry.Create(
            ValidMessageId,
            ValidContext,
            ValidAggregateId,
            ValidEventType,
            ValidPayload,
            status,
            0,
            null,
            ValidCreatedAt,
            processedAt);
}
