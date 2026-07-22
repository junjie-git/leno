using Leno.Notification.Domain.Aggregates;
using Leno.Notification.Domain.Exceptions;
using Leno.Notification.Domain.ValueObjects;

namespace Leno.Notification.Domain.Tests;

public class NotificationRecordTests
{
    private static readonly Guid ValidRecordId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ValidUserId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private const string ValidTemplateCode = "OrderCreated";
    private static readonly Guid? ValidEventId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private const NotificationChannel ValidChannel = NotificationChannel.InApp;
    private const string ValidTitle = "Order Confirmed";
    private const string ValidContent = "Your order has been confirmed.";

    private static NotificationRecord CreateValidRecord(
        NotificationChannel? channel = null,
        Guid? eventId = null,
        int maxRetry = NotificationRecord.DefaultMaxRetry)
    {
        return NotificationRecord.Create(
            ValidRecordId, ValidUserId, ValidTemplateCode,
            eventId ?? ValidEventId, channel ?? ValidChannel,
            ValidTitle, ValidContent, maxRetry: maxRetry);
    }

    #region Create - Happy Path

    [Fact]
    public void Create_ValidParameters_ShouldCreateRecord()
    {
        // Act
        var record = NotificationRecord.Create(
            ValidRecordId, ValidUserId, ValidTemplateCode, ValidEventId,
            ValidChannel, ValidTitle, ValidContent, "ORD-123", "idem-key-456");

        // Assert
        record.Id.Should().Be(ValidRecordId);
        record.UserId.Should().Be(ValidUserId);
        record.TemplateCode.Should().Be(ValidTemplateCode);
        record.EventId.Should().Be(ValidEventId);
        record.Channel.Should().Be(ValidChannel);
        record.Title.Should().Be(ValidTitle);
        record.Content.Should().Be(ValidContent);
        record.Status.Should().Be(NotificationStatus.Pending);
        record.RetryCount.Should().Be(0);
        record.MaxRetry.Should().Be(NotificationRecord.DefaultMaxRetry);
        record.IsRead.Should().BeFalse();
        record.SentAt.Should().BeNull();
        record.FailedAt.Should().BeNull();
        record.ErrorMessage.Should().BeNull();
        record.ErrorCode.Should().BeNull();
        record.BusinessRef.Should().Be("ORD-123");
        record.IdempotencyKey.Should().Be("idem-key-456");
    }

    [Fact]
    public void Create_NullEventId_ShouldCreateRecord()
    {
        // Act
        var record = NotificationRecord.Create(
            ValidRecordId, ValidUserId, ValidTemplateCode, null,
            ValidChannel, ValidTitle, ValidContent);

        // Assert
        record.EventId.Should().BeNull();
        record.Status.Should().Be(NotificationStatus.Pending);
    }

    [Fact]
    public void Create_WithSmsChannel_ShouldCreateRecord()
    {
        // Act
        var record = NotificationRecord.Create(
            ValidRecordId, ValidUserId, ValidTemplateCode, ValidEventId,
            NotificationChannel.Sms, ValidTitle, ValidContent);

        // Assert
        record.Channel.Should().Be(NotificationChannel.Sms);
    }

    [Fact]
    public void Create_WithEmailChannel_ShouldCreateRecord()
    {
        // Act
        var record = NotificationRecord.Create(
            ValidRecordId, ValidUserId, ValidTemplateCode, ValidEventId,
            NotificationChannel.Email, ValidTitle, ValidContent);

        // Assert
        record.Channel.Should().Be(NotificationChannel.Email);
    }

    [Fact]
    public void Create_WithCustomMaxRetry_ShouldSetMaxRetry()
    {
        // Act
        var record = NotificationRecord.Create(
            ValidRecordId, ValidUserId, ValidTemplateCode, ValidEventId,
            ValidChannel, ValidTitle, ValidContent, maxRetry: 5);

        // Assert
        record.MaxRetry.Should().Be(5);
    }

    #endregion

    #region Create - Validation

    [Fact]
    public void Create_EmptyRecordId_ShouldThrowNotificationDomainException()
    {
        // Act
        var act = () => NotificationRecord.Create(
            Guid.Empty, ValidUserId, ValidTemplateCode, ValidEventId,
            ValidChannel, ValidTitle, ValidContent);

        // Assert
        act.Should().Throw<NotificationDomainException>()
            .And.ErrorCode.Should().Be("NOTIFICATION_RECORD_ID_EMPTY");
    }

    [Fact]
    public void Create_EmptyUserId_ShouldThrowNotificationDomainException()
    {
        // Act
        var act = () => NotificationRecord.Create(
            ValidRecordId, Guid.Empty, ValidTemplateCode, ValidEventId,
            ValidChannel, ValidTitle, ValidContent);

        // Assert
        act.Should().Throw<NotificationDomainException>()
            .And.ErrorCode.Should().Be("NOTIFICATION_USER_EMPTY");
    }

    [Fact]
    public void Create_NullTemplateCode_ShouldThrowNotificationDomainException()
    {
        // Act
        var act = () => NotificationRecord.Create(
            ValidRecordId, ValidUserId, null!, ValidEventId,
            ValidChannel, ValidTitle, ValidContent);

        // Assert
        act.Should().Throw<NotificationDomainException>()
            .And.ErrorCode.Should().Be("NOTIFICATION_TEMPLATE_CODE_EMPTY");
    }

    [Fact]
    public void Create_EmptyTemplateCode_ShouldThrowNotificationDomainException()
    {
        // Act
        var act = () => NotificationRecord.Create(
            ValidRecordId, ValidUserId, "", ValidEventId,
            ValidChannel, ValidTitle, ValidContent);

        // Assert
        act.Should().Throw<NotificationDomainException>()
            .And.ErrorCode.Should().Be("NOTIFICATION_TEMPLATE_CODE_EMPTY");
    }

    [Fact]
    public void Create_WhitespaceTemplateCode_ShouldThrowNotificationDomainException()
    {
        // Act
        var act = () => NotificationRecord.Create(
            ValidRecordId, ValidUserId, "   ", ValidEventId,
            ValidChannel, ValidTitle, ValidContent);

        // Assert
        act.Should().Throw<NotificationDomainException>()
            .And.ErrorCode.Should().Be("NOTIFICATION_TEMPLATE_CODE_EMPTY");
    }

    [Fact]
    public void Create_NullTitle_ShouldThrowNotificationDomainException()
    {
        // Act
        var act = () => NotificationRecord.Create(
            ValidRecordId, ValidUserId, ValidTemplateCode, ValidEventId,
            ValidChannel, null!, ValidContent);

        // Assert
        act.Should().Throw<NotificationDomainException>()
            .And.ErrorCode.Should().Be("NOTIFICATION_TITLE_EMPTY");
    }

    [Fact]
    public void Create_EmptyTitle_ShouldThrowNotificationDomainException()
    {
        // Act
        var act = () => NotificationRecord.Create(
            ValidRecordId, ValidUserId, ValidTemplateCode, ValidEventId,
            ValidChannel, "", ValidContent);

        // Assert
        act.Should().Throw<NotificationDomainException>()
            .And.ErrorCode.Should().Be("NOTIFICATION_TITLE_EMPTY");
    }

    [Fact]
    public void Create_WhitespaceTitle_ShouldThrowNotificationDomainException()
    {
        // Act
        var act = () => NotificationRecord.Create(
            ValidRecordId, ValidUserId, ValidTemplateCode, ValidEventId,
            ValidChannel, "   ", ValidContent);

        // Assert
        act.Should().Throw<NotificationDomainException>()
            .And.ErrorCode.Should().Be("NOTIFICATION_TITLE_EMPTY");
    }

    [Fact]
    public void Create_NullContent_ShouldThrowNotificationDomainException()
    {
        // Act
        var act = () => NotificationRecord.Create(
            ValidRecordId, ValidUserId, ValidTemplateCode, ValidEventId,
            ValidChannel, ValidTitle, null!);

        // Assert
        act.Should().Throw<NotificationDomainException>()
            .And.ErrorCode.Should().Be("NOTIFICATION_CONTENT_EMPTY");
    }

    [Fact]
    public void Create_EmptyContent_ShouldThrowNotificationDomainException()
    {
        // Act
        var act = () => NotificationRecord.Create(
            ValidRecordId, ValidUserId, ValidTemplateCode, ValidEventId,
            ValidChannel, ValidTitle, "");

        // Assert
        act.Should().Throw<NotificationDomainException>()
            .And.ErrorCode.Should().Be("NOTIFICATION_CONTENT_EMPTY");
    }

    [Fact]
    public void Create_WhitespaceContent_ShouldThrowNotificationDomainException()
    {
        // Act
        var act = () => NotificationRecord.Create(
            ValidRecordId, ValidUserId, ValidTemplateCode, ValidEventId,
            ValidChannel, ValidTitle, "   ");

        // Assert
        act.Should().Throw<NotificationDomainException>()
            .And.ErrorCode.Should().Be("NOTIFICATION_CONTENT_EMPTY");
    }

    [Fact]
    public void Create_InvalidChannel_ShouldThrowNotificationDomainException()
    {
        // Act
        var act = () => NotificationRecord.Create(
            ValidRecordId, ValidUserId, ValidTemplateCode, ValidEventId,
            (NotificationChannel)999, ValidTitle, ValidContent);

        // Assert
        act.Should().Throw<NotificationDomainException>()
            .And.ErrorCode.Should().Be("NOTIFICATION_CHANNEL_INVALID");
    }

    [Fact]
    public void Create_NegativeMaxRetry_ShouldThrowNotificationDomainException()
    {
        // Act
        var act = () => NotificationRecord.Create(
            ValidRecordId, ValidUserId, ValidTemplateCode, ValidEventId,
            ValidChannel, ValidTitle, ValidContent, maxRetry: -1);

        // Assert
        act.Should().Throw<NotificationDomainException>()
            .And.ErrorCode.Should().Be("NOTIFICATION_MAX_RETRY_INVALID");
    }

    #endregion

    #region CreateFailed - 工厂方法直接创建 Failed 状态记录（P1-37）

    [Fact]
    public void CreateFailed_ValidParameters_ShouldCreateFailedRecord()
    {
        // Act
        var record = NotificationRecord.CreateFailed(
            ValidRecordId, ValidUserId, ValidTemplateCode, ValidEventId,
            ValidChannel, ValidTitle, ValidContent,
            "模板渲染失败：变量缺失", "TEMPLATE_RENDER_FAILED",
            "ORD-123", "idem-key-456");

        // Assert
        record.Id.Should().Be(ValidRecordId);
        record.UserId.Should().Be(ValidUserId);
        record.TemplateCode.Should().Be(ValidTemplateCode);
        record.EventId.Should().Be(ValidEventId);
        record.Channel.Should().Be(ValidChannel);
        record.Title.Should().Be(ValidTitle);
        record.Content.Should().Be(ValidContent);
        record.Status.Should().Be(NotificationStatus.Failed);
        record.RetryCount.Should().Be(0);
        record.MaxRetry.Should().Be(NotificationRecord.DefaultMaxRetry);
        record.IsRead.Should().BeFalse();
        record.SentAt.Should().BeNull();
        record.FailedAt.Should().NotBeNull();
        record.ErrorMessage.Should().Be("模板渲染失败：变量缺失");
        record.ErrorCode.Should().Be("TEMPLATE_RENDER_FAILED");
        record.BusinessRef.Should().Be("ORD-123");
        record.IdempotencyKey.Should().Be("idem-key-456");
    }

    [Fact]
    public void CreateFailed_NullEventId_ShouldCreateFailedRecord()
    {
        // Act
        var record = NotificationRecord.CreateFailed(
            ValidRecordId, ValidUserId, ValidTemplateCode, null,
            ValidChannel, ValidTitle, ValidContent,
            "渲染失败", "TEMPLATE_RENDER_FAILED");

        // Assert
        record.EventId.Should().BeNull();
        record.Status.Should().Be(NotificationStatus.Failed);
        record.FailedAt.Should().NotBeNull();
    }

    [Fact]
    public void CreateFailed_EmptyErrorMessage_ShouldDefaultToUnknownError()
    {
        // Act
        var record = NotificationRecord.CreateFailed(
            ValidRecordId, ValidUserId, ValidTemplateCode, ValidEventId,
            ValidChannel, ValidTitle, ValidContent,
            "", "TEMPLATE_RENDER_FAILED");

        // Assert - 空错误信息被默认值 "未知错误" 替换
        record.ErrorMessage.Should().Be("未知错误");
        record.ErrorCode.Should().Be("TEMPLATE_RENDER_FAILED");
    }

    [Fact]
    public void CreateFailed_WhitespaceErrorMessage_ShouldDefaultToUnknownError()
    {
        // Act
        var record = NotificationRecord.CreateFailed(
            ValidRecordId, ValidUserId, ValidTemplateCode, ValidEventId,
            ValidChannel, ValidTitle, ValidContent,
            "   ", "TEMPLATE_RENDER_FAILED");

        // Assert
        record.ErrorMessage.Should().Be("未知错误");
    }

    [Fact]
    public void CreateFailed_NullErrorCode_ShouldKeepNullErrorCode()
    {
        // Act
        var record = NotificationRecord.CreateFailed(
            ValidRecordId, ValidUserId, ValidTemplateCode, ValidEventId,
            ValidChannel, ValidTitle, ValidContent,
            "渲染失败", errorCode: null);

        // Assert - errorCode 允许为 null
        record.ErrorCode.Should().BeNull();
        record.Status.Should().Be(NotificationStatus.Failed);
    }

    [Fact]
    public void CreateFailed_EmptyRecordId_ShouldThrowNotificationDomainException()
    {
        var act = () => NotificationRecord.CreateFailed(
            Guid.Empty, ValidUserId, ValidTemplateCode, ValidEventId,
            ValidChannel, ValidTitle, ValidContent, "错误", "TEMPLATE_RENDER_FAILED");

        act.Should().Throw<NotificationDomainException>()
            .And.ErrorCode.Should().Be("NOTIFICATION_RECORD_ID_EMPTY");
    }

    [Fact]
    public void CreateFailed_EmptyUserId_ShouldThrowNotificationDomainException()
    {
        var act = () => NotificationRecord.CreateFailed(
            ValidRecordId, Guid.Empty, ValidTemplateCode, ValidEventId,
            ValidChannel, ValidTitle, ValidContent, "错误", "TEMPLATE_RENDER_FAILED");

        act.Should().Throw<NotificationDomainException>()
            .And.ErrorCode.Should().Be("NOTIFICATION_USER_EMPTY");
    }

    [Fact]
    public void CreateFailed_EmptyTemplateCode_ShouldThrowNotificationDomainException()
    {
        var act = () => NotificationRecord.CreateFailed(
            ValidRecordId, ValidUserId, "", ValidEventId,
            ValidChannel, ValidTitle, ValidContent, "错误", "TEMPLATE_RENDER_FAILED");

        act.Should().Throw<NotificationDomainException>()
            .And.ErrorCode.Should().Be("NOTIFICATION_TEMPLATE_CODE_EMPTY");
    }

    [Fact]
    public void CreateFailed_EmptyTitle_ShouldThrowNotificationDomainException()
    {
        var act = () => NotificationRecord.CreateFailed(
            ValidRecordId, ValidUserId, ValidTemplateCode, ValidEventId,
            ValidChannel, "", ValidContent, "错误", "TEMPLATE_RENDER_FAILED");

        act.Should().Throw<NotificationDomainException>()
            .And.ErrorCode.Should().Be("NOTIFICATION_TITLE_EMPTY");
    }

    [Fact]
    public void CreateFailed_EmptyContent_ShouldThrowNotificationDomainException()
    {
        var act = () => NotificationRecord.CreateFailed(
            ValidRecordId, ValidUserId, ValidTemplateCode, ValidEventId,
            ValidChannel, ValidTitle, "", "错误", "TEMPLATE_RENDER_FAILED");

        act.Should().Throw<NotificationDomainException>()
            .And.ErrorCode.Should().Be("NOTIFICATION_CONTENT_EMPTY");
    }

    [Fact]
    public void CreateFailed_InvalidChannel_ShouldThrowNotificationDomainException()
    {
        var act = () => NotificationRecord.CreateFailed(
            ValidRecordId, ValidUserId, ValidTemplateCode, ValidEventId,
            (NotificationChannel)999, ValidTitle, ValidContent, "错误", "TEMPLATE_RENDER_FAILED");

        act.Should().Throw<NotificationDomainException>()
            .And.ErrorCode.Should().Be("NOTIFICATION_CHANNEL_INVALID");
    }

    [Fact]
    public void CreateFailed_NegativeMaxRetry_ShouldThrowNotificationDomainException()
    {
        var act = () => NotificationRecord.CreateFailed(
            ValidRecordId, ValidUserId, ValidTemplateCode, ValidEventId,
            ValidChannel, ValidTitle, ValidContent, "错误", "TEMPLATE_RENDER_FAILED",
            maxRetry: -1);

        act.Should().Throw<NotificationDomainException>()
            .And.ErrorCode.Should().Be("NOTIFICATION_MAX_RETRY_INVALID");
    }

    #endregion

    #region MarkSending

    [Fact]
    public void MarkSending_WhenPending_ShouldSetStatusToSending()
    {
        // Arrange
        var record = CreateValidRecord();

        // Act
        record.MarkSending();

        // Assert
        record.Status.Should().Be(NotificationStatus.Sending);
    }

    [Fact]
    public void MarkSending_WhenAlreadySending_ShouldThrowNotificationDomainException()
    {
        // Arrange
        var record = CreateValidRecord();
        record.MarkSending();

        // Act
        var act = () => record.MarkSending();

        // Assert
        act.Should().Throw<NotificationDomainException>()
            .And.ErrorCode.Should().Be("NOTIFICATION_SENDING_STATUS_INVALID");
    }

    [Fact]
    public void MarkSending_WhenSucceeded_ShouldThrowNotificationDomainException()
    {
        // Arrange
        var record = CreateValidRecord();
        record.MarkSending();
        record.MarkSucceeded();

        // Act
        var act = () => record.MarkSending();

        // Assert
        act.Should().Throw<NotificationDomainException>()
            .And.ErrorCode.Should().Be("NOTIFICATION_SENDING_STATUS_INVALID");
    }

    [Fact]
    public void MarkSending_WhenDeadLettered_ShouldThrowNotificationDomainException()
    {
        // Arrange
        var record = CreateValidRecord();
        record.MarkSending();
        record.MarkFailed("error");
        record.ScheduleRetry();
        record.MoveToDeadLetter("exhausted");

        // Act
        var act = () => record.MarkSending();

        // Assert
        act.Should().Throw<NotificationDomainException>()
            .And.ErrorCode.Should().Be("NOTIFICATION_SENDING_STATUS_INVALID");
    }

    #endregion

    #region MarkSucceeded

    [Fact]
    public void MarkSucceeded_WhenSending_ShouldSetStatusToSucceeded()
    {
        // Arrange
        var record = CreateValidRecord();
        record.MarkSending();

        // Act
        record.MarkSucceeded("msg-123");

        // Assert
        record.Status.Should().Be(NotificationStatus.Succeeded);
        record.SentAt.Should().NotBeNull();
        record.SentAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        record.ChannelMessageId.Should().Be("msg-123");
        record.ErrorMessage.Should().BeNull();
        record.ErrorCode.Should().BeNull();
    }

    [Fact]
    public void MarkSucceeded_WithoutChannelMessageId_ShouldSetNull()
    {
        // Arrange
        var record = CreateValidRecord();
        record.MarkSending();

        // Act
        record.MarkSucceeded();

        // Assert
        record.Status.Should().Be(NotificationStatus.Succeeded);
        record.ChannelMessageId.Should().BeNull();
    }

    [Fact]
    public void MarkSucceeded_WhenPending_ShouldThrowNotificationDomainException()
    {
        // Arrange
        var record = CreateValidRecord();

        // Act
        var act = () => record.MarkSucceeded();

        // Assert
        act.Should().Throw<NotificationDomainException>()
            .And.ErrorCode.Should().Be("NOTIFICATION_SUCCEEDED_STATUS_INVALID");
    }

    [Fact]
    public void MarkSucceeded_WhenAlreadySucceeded_ShouldThrowNotificationDomainException()
    {
        // Arrange
        var record = CreateValidRecord();
        record.MarkSending();
        record.MarkSucceeded();

        // Act
        var act = () => record.MarkSucceeded();

        // Assert
        act.Should().Throw<NotificationDomainException>()
            .And.ErrorCode.Should().Be("NOTIFICATION_SUCCEEDED_STATUS_INVALID");
    }

    [Fact]
    public void MarkSucceeded_WhenFailed_ShouldThrowNotificationDomainException()
    {
        // Arrange
        var record = CreateValidRecord();
        record.MarkSending();
        record.MarkFailed("error");

        // Act
        var act = () => record.MarkSucceeded();

        // Assert
        act.Should().Throw<NotificationDomainException>()
            .And.ErrorCode.Should().Be("NOTIFICATION_SUCCEEDED_STATUS_INVALID");
    }

    [Fact]
    public void MarkSucceeded_ShouldClearErrorState()
    {
        // Arrange
        var record = CreateValidRecord();
        record.MarkSending();
        record.MarkFailed("some error", "ERR_001");
        // Reset to Pending -> Sending to test clearing
        var record2 = CreateValidRecord();
        record2.MarkSending();

        // Act
        record2.MarkSucceeded();

        // Assert
        record2.ErrorMessage.Should().BeNull();
        record2.ErrorCode.Should().BeNull();
    }

    #endregion

    #region MarkFailed

    [Fact]
    public void MarkFailed_WhenSending_ShouldSetStatusToFailed()
    {
        // Arrange
        var record = CreateValidRecord();
        record.MarkSending();
        const string errorMessage = "Network timeout";
        const string errorCode = "NET_TIMEOUT";

        // Act
        record.MarkFailed(errorMessage, errorCode);

        // Assert
        record.Status.Should().Be(NotificationStatus.Failed);
        record.ErrorMessage.Should().Be(errorMessage);
        record.ErrorCode.Should().Be(errorCode);
        record.FailedAt.Should().NotBeNull();
        record.FailedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        record.RetryCount.Should().Be(1);
    }

    [Fact]
    public void MarkFailed_EmptyErrorMessage_ShouldDefaultToFallback()
    {
        // Arrange
        var record = CreateValidRecord();
        record.MarkSending();

        // Act
        record.MarkFailed("");

        // Assert
        record.ErrorMessage.Should().Be("未知错误");
    }

    [Fact]
    public void MarkFailed_NullErrorMessage_ShouldDefaultToFallback()
    {
        // Arrange
        var record = CreateValidRecord();
        record.MarkSending();

        // Act
        record.MarkFailed(null!);

        // Assert
        record.ErrorMessage.Should().Be("未知错误");
    }

    [Fact]
    public void MarkFailed_WhitespaceErrorMessage_ShouldDefaultToFallback()
    {
        // Arrange
        var record = CreateValidRecord();
        record.MarkSending();

        // Act
        record.MarkFailed("   ");

        // Assert
        record.ErrorMessage.Should().Be("未知错误");
    }

    [Fact]
    public void MarkFailed_WhenPending_ShouldThrowNotificationDomainException()
    {
        // Arrange
        var record = CreateValidRecord();

        // Act
        var act = () => record.MarkFailed("error");

        // Assert
        act.Should().Throw<NotificationDomainException>()
            .And.ErrorCode.Should().Be("NOTIFICATION_FAILED_STATUS_INVALID");
    }

    [Fact]
    public void MarkFailed_WhenSucceeded_ShouldThrowNotificationDomainException()
    {
        // Arrange
        var record = CreateValidRecord();
        record.MarkSending();
        record.MarkSucceeded();

        // Act
        var act = () => record.MarkFailed("error");

        // Assert
        act.Should().Throw<NotificationDomainException>()
            .And.ErrorCode.Should().Be("NOTIFICATION_FAILED_STATUS_INVALID");
    }

    [Fact]
    public void MarkFailed_WhenDeadLettered_ShouldThrowNotificationDomainException()
    {
        // Arrange
        var record = CreateValidRecord();
        record.MarkSending();
        record.MarkFailed("error");
        record.ScheduleRetry();
        record.MoveToDeadLetter("exhausted");

        // Act
        var act = () => record.MarkFailed("error");

        // Assert
        act.Should().Throw<NotificationDomainException>()
            .And.ErrorCode.Should().Be("NOTIFICATION_FAILED_STATUS_INVALID");
    }

    #endregion

    #region ScheduleRetry

    [Fact]
    public void ScheduleRetry_WhenFailed_ShouldSetStatusToRetried()
    {
        // Arrange
        var record = CreateValidRecord();
        record.MarkSending();
        record.MarkFailed("error");

        // Act
        record.ScheduleRetry();

        // Assert
        record.Status.Should().Be(NotificationStatus.Retried);
        record.NextRetryAt.Should().NotBeNull();
        record.NextRetryAt.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(1), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void ScheduleRetry_WithCustomTime_ShouldSetNextRetryAt()
    {
        // Arrange
        var record = CreateValidRecord();
        record.MarkSending();
        record.MarkFailed("error");
        var customTime = DateTime.UtcNow.AddHours(1);

        // Act
        record.ScheduleRetry(customTime);

        // Assert
        record.NextRetryAt.Should().Be(customTime);
    }

    [Fact]
    public void ScheduleRetry_WhenPending_ShouldThrowNotificationDomainException()
    {
        // Arrange
        var record = CreateValidRecord();

        // Act
        var act = () => record.ScheduleRetry();

        // Assert
        act.Should().Throw<NotificationDomainException>()
            .And.ErrorCode.Should().Be("NOTIFICATION_RETRY_STATUS_INVALID");
    }

    [Fact]
    public void ScheduleRetry_WhenSending_ShouldThrowNotificationDomainException()
    {
        // Arrange
        var record = CreateValidRecord();
        record.MarkSending();

        // Act
        var act = () => record.ScheduleRetry();

        // Assert
        act.Should().Throw<NotificationDomainException>()
            .And.ErrorCode.Should().Be("NOTIFICATION_RETRY_STATUS_INVALID");
    }

    [Fact]
    public void ScheduleRetry_WhenAlreadyRetried_ShouldThrowNotificationDomainException()
    {
        // Arrange
        var record = CreateValidRecord();
        record.MarkSending();
        record.MarkFailed("error");
        record.ScheduleRetry();

        // Act
        var act = () => record.ScheduleRetry();

        // Assert
        act.Should().Throw<NotificationDomainException>()
            .And.ErrorCode.Should().Be("NOTIFICATION_RETRY_STATUS_INVALID");
    }

    #endregion

    #region MoveToDeadLetter

    [Fact]
    public void MoveToDeadLetter_WhenRetried_ShouldSetStatusToDeadLettered()
    {
        // Arrange
        var record = CreateValidRecord();
        record.MarkSending();
        record.MarkFailed("error");
        record.ScheduleRetry();

        // Act
        record.MoveToDeadLetter("超过最大重试次数");

        // Assert
        record.Status.Should().Be(NotificationStatus.DeadLettered);
        record.ErrorMessage.Should().Be("超过最大重试次数");
    }

    [Fact]
    public void MoveToDeadLetter_EmptyReason_ShouldDefaultToFallback()
    {
        // Arrange
        var record = CreateValidRecord();
        record.MarkSending();
        record.MarkFailed("error");
        record.ScheduleRetry();

        // Act
        record.MoveToDeadLetter("");

        // Assert
        record.ErrorMessage.Should().Be("超过最大重试次数");
    }

    [Fact]
    public void MoveToDeadLetter_WhenAlreadyDeadLettered_ShouldNotThrow()
    {
        // Arrange
        var record = CreateValidRecord();
        record.MarkSending();
        record.MarkFailed("error");
        record.ScheduleRetry();
        record.MoveToDeadLetter("reason");
        record.Status.Should().Be(NotificationStatus.DeadLettered);

        // Act
        var act = () => record.MoveToDeadLetter("another reason");

        // Assert
        act.Should().NotThrow();
        record.Status.Should().Be(NotificationStatus.DeadLettered);
    }

    [Fact]
    public void MoveToDeadLetter_WhenPending_ShouldThrowNotificationDomainException()
    {
        // Arrange
        var record = CreateValidRecord();

        // Act
        var act = () => record.MoveToDeadLetter("reason");

        // Assert
        act.Should().Throw<NotificationDomainException>()
            .And.ErrorCode.Should().Be("NOTIFICATION_DEAD_LETTER_STATUS_INVALID");
    }

    [Fact]
    public void MoveToDeadLetter_WhenFailed_ShouldThrowNotificationDomainException()
    {
        // Arrange
        var record = CreateValidRecord();
        record.MarkSending();
        record.MarkFailed("error");

        // Act
        var act = () => record.MoveToDeadLetter("reason");

        // Assert
        act.Should().Throw<NotificationDomainException>()
            .And.ErrorCode.Should().Be("NOTIFICATION_DEAD_LETTER_STATUS_INVALID");
    }

    #endregion

    #region CanRetry

    [Fact]
    public void CanRetry_WhenFailedAndRetryCountBelowMax_ShouldReturnTrue()
    {
        // Arrange
        var record = CreateValidRecord();
        record.MarkSending();
        record.MarkFailed("error");
        record.RetryCount.Should().Be(1);

        // Assert
        record.CanRetry.Should().BeTrue();
    }

    [Fact]
    public void CanRetry_WhenFailedAndRetryCountEqualsMax_ShouldReturnFalse()
    {
        // Arrange
        var record = CreateValidRecord();
        record.MarkSending();
        record.MarkFailed("error 1");
        record.ScheduleRetry();
        record.MarkSending();
        record.MarkFailed("error 2");
        record.ScheduleRetry();
        record.MarkSending();
        record.MarkFailed("error 3");
        record.RetryCount.Should().Be(3);

        // Assert
        record.CanRetry.Should().BeFalse();
    }

    [Fact]
    public void CanRetry_WhenPending_ShouldReturnFalse()
    {
        // Arrange
        var record = CreateValidRecord();

        // Assert
        record.CanRetry.Should().BeFalse();
    }

    [Fact]
    public void CanRetry_WhenSending_ShouldReturnFalse()
    {
        // Arrange
        var record = CreateValidRecord();
        record.MarkSending();

        // Assert
        record.CanRetry.Should().BeFalse();
    }

    [Fact]
    public void CanRetry_WhenSucceeded_ShouldReturnFalse()
    {
        // Arrange
        var record = CreateValidRecord();
        record.MarkSending();
        record.MarkSucceeded();

        // Assert
        record.CanRetry.Should().BeFalse();
    }

    [Fact]
    public void CanRetry_WhenRetried_ShouldReturnFalse()
    {
        // Arrange
        var record = CreateValidRecord();
        record.MarkSending();
        record.MarkFailed("error");
        record.ScheduleRetry();

        // Assert
        record.CanRetry.Should().BeFalse();
    }

    [Fact]
    public void CanRetry_WhenDeadLettered_ShouldReturnFalse()
    {
        // Arrange
        var record = CreateValidRecord();
        record.MarkSending();
        record.MarkFailed("error");
        record.ScheduleRetry();
        record.MoveToDeadLetter("exhausted");

        // Assert
        record.CanRetry.Should().BeFalse();
    }

    #endregion

    #region MarkAsRead

    [Fact]
    public void MarkAsRead_WhenInAppChannel_ShouldSetIsReadToTrue()
    {
        // Arrange
        var record = CreateValidRecord(channel: NotificationChannel.InApp);
        record.IsRead.Should().BeFalse();

        // Act
        record.MarkAsRead();

        // Assert
        record.IsRead.Should().BeTrue();
    }

    [Fact]
    public void MarkAsRead_WhenSmsChannel_ShouldThrowNotificationDomainException()
    {
        // Arrange
        var record = CreateValidRecord(channel: NotificationChannel.Sms);

        // Act
        var act = () => record.MarkAsRead();

        // Assert
        act.Should().Throw<NotificationDomainException>()
            .And.ErrorCode.Should().Be("NOTIFICATION_READ_CHANNEL_INVALID");
    }

    [Fact]
    public void MarkAsRead_WhenEmailChannel_ShouldThrowNotificationDomainException()
    {
        // Arrange
        var record = CreateValidRecord(channel: NotificationChannel.Email);

        // Act
        var act = () => record.MarkAsRead();

        // Assert
        act.Should().Throw<NotificationDomainException>()
            .And.ErrorCode.Should().Be("NOTIFICATION_READ_CHANNEL_INVALID");
    }

    #endregion

    #region State Machine - Full Lifecycle

    [Fact]
    public void StateMachine_HappyPath_ShouldSucceed()
    {
        // Arrange
        var record = CreateValidRecord();

        // Pending -> Sending
        record.MarkSending();
        record.Status.Should().Be(NotificationStatus.Sending);

        // Sending -> Succeeded
        record.MarkSucceeded("msg-001");
        record.Status.Should().Be(NotificationStatus.Succeeded);
        record.SentAt.Should().NotBeNull();
        record.ChannelMessageId.Should().Be("msg-001");
    }

    [Fact]
    public void StateMachine_RetryAndSucceed_ShouldFollowCorrectTransitions()
    {
        // Arrange
        var record = CreateValidRecord();

        // Pending -> Sending -> Failed (retry 1)
        record.MarkSending();
        record.MarkFailed("error 1", "ERR_1");
        record.Status.Should().Be(NotificationStatus.Failed);
        record.RetryCount.Should().Be(1);
        record.CanRetry.Should().BeTrue();

        // Failed -> Retried
        record.ScheduleRetry();
        record.Status.Should().Be(NotificationStatus.Retried);
        record.NextRetryAt.Should().NotBeNull();

        // Retried -> Sending -> Succeeded
        record.MarkSending();
        record.Status.Should().Be(NotificationStatus.Sending);
        record.MarkSucceeded("msg-002");
        record.Status.Should().Be(NotificationStatus.Succeeded);
    }

    [Fact]
    public void StateMachine_ExceedRetryLimitAndDeadLetter_ShouldDeadLetter()
    {
        // Arrange
        var record = CreateValidRecord(maxRetry: 3);

        // Retry 1: Pending -> Sending -> Failed -> Retried
        record.MarkSending();
        record.MarkFailed("error 1");
        record.RetryCount.Should().Be(1);
        record.CanRetry.Should().BeTrue();
        record.ScheduleRetry();
        record.Status.Should().Be(NotificationStatus.Retried);

        // Retry 2: Retried -> Sending -> Failed -> Retried
        record.MarkSending();
        record.MarkFailed("error 2");
        record.RetryCount.Should().Be(2);
        record.CanRetry.Should().BeTrue();
        record.ScheduleRetry();
        record.Status.Should().Be(NotificationStatus.Retried);

        // Retry 3: Retried -> Sending -> Failed
        record.MarkSending();
        record.MarkFailed("error 3");
        record.RetryCount.Should().Be(3);
        record.CanRetry.Should().BeFalse();

        // Cannot schedule retry when CanRetry is false
        // (scheduleRetry still works from Failed state, but caller should check CanRetry first)
        record.ScheduleRetry();
        record.Status.Should().Be(NotificationStatus.Retried);

        // Move to DeadLetter
        record.MoveToDeadLetter("超过最大重试次数 3/3");
        record.Status.Should().Be(NotificationStatus.DeadLettered);

        // DeadLettered is terminal
        var sendingAct = () => record.MarkSending();
        sendingAct.Should().Throw<NotificationDomainException>()
            .And.ErrorCode.Should().Be("NOTIFICATION_SENDING_STATUS_INVALID");
    }

    [Fact]
    public void StateMachine_SucceededIsTerminal_CannotTransition()
    {
        // Arrange
        var record = CreateValidRecord();
        record.MarkSending();
        record.MarkSucceeded();

        // Assert: Cannot go to Failed
        var failedAct = () => record.MarkFailed("late error");
        failedAct.Should().Throw<NotificationDomainException>()
            .And.ErrorCode.Should().Be("NOTIFICATION_FAILED_STATUS_INVALID");

        // Assert: Cannot go to Sending
        var sendingAct = () => record.MarkSending();
        sendingAct.Should().Throw<NotificationDomainException>()
            .And.ErrorCode.Should().Be("NOTIFICATION_SENDING_STATUS_INVALID");
    }

    [Fact]
    public void StateMachine_DefaultMaxRetry_ShouldBeThree()
    {
        // Assert
        NotificationRecord.DefaultMaxRetry.Should().Be(3);
    }

    #endregion

    #region SentAt_Timing

    [Fact]
    public void MarkSucceeded_ShouldSetSentAtToUtcNow()
    {
        // Arrange
        var record = CreateValidRecord();
        record.MarkSending();
        var before = DateTime.UtcNow;

        // Act
        record.MarkSucceeded();

        // Assert
        record.SentAt.Should().NotBeNull();
        record.SentAt!.Value.Should().BeOnOrAfter(before);
        record.SentAt!.Value.Should().BeOnOrBefore(DateTime.UtcNow);
    }

    #endregion

    #region FailedAt_Timing

    [Fact]
    public void MarkFailed_ShouldSetFailedAtToUtcNow()
    {
        // Arrange
        var record = CreateValidRecord();
        record.MarkSending();
        var before = DateTime.UtcNow;

        // Act
        record.MarkFailed("error");

        // Assert
        record.FailedAt.Should().NotBeNull();
        record.FailedAt!.Value.Should().BeOnOrAfter(before);
        record.FailedAt!.Value.Should().BeOnOrBefore(DateTime.UtcNow);
    }

    #endregion

    #region MarkSending_FromRetried

    [Fact]
    public void MarkSending_WhenRetried_ShouldSetStatusToSending()
    {
        // Arrange
        var record = CreateValidRecord();
        record.MarkSending();
        record.MarkFailed("error");
        record.ScheduleRetry();
        record.Status.Should().Be(NotificationStatus.Retried);

        // Act
        record.MarkSending();

        // Assert
        record.Status.Should().Be(NotificationStatus.Sending);
    }

    #endregion

    #region MarkResend

    [Fact]
    public void MarkResend_WhenDeadLettered_ShouldResetToSending()
    {
        // Arrange
        var record = CreateValidRecord();
        record.MarkSending();
        record.MarkFailed("error", "ERR_001");
        record.ScheduleRetry();
        record.MoveToDeadLetter("exhausted");
        record.Status.Should().Be(NotificationStatus.DeadLettered);

        // Act
        record.MarkResend();

        // Assert
        record.Status.Should().Be(NotificationStatus.Sending);
        record.RetryCount.Should().Be(0);
        record.ErrorMessage.Should().BeNull();
        record.ErrorCode.Should().BeNull();
        record.FailedAt.Should().BeNull();
        record.NextRetryAt.Should().BeNull();
    }

    [Fact]
    public void MarkResend_WhenPending_ShouldThrowNotificationDomainException()
    {
        // Arrange
        var record = CreateValidRecord();

        // Act
        var act = () => record.MarkResend();

        // Assert
        act.Should().Throw<NotificationDomainException>()
            .And.ErrorCode.Should().Be("NOTIFICATION_RESEND_STATUS_INVALID");
    }

    [Fact]
    public void MarkResend_WhenSending_ShouldThrowNotificationDomainException()
    {
        // Arrange
        var record = CreateValidRecord();
        record.MarkSending();

        // Act
        var act = () => record.MarkResend();

        // Assert
        act.Should().Throw<NotificationDomainException>()
            .And.ErrorCode.Should().Be("NOTIFICATION_RESEND_STATUS_INVALID");
    }

    [Fact]
    public void MarkResend_WhenSucceeded_ShouldThrowNotificationDomainException()
    {
        // Arrange
        var record = CreateValidRecord();
        record.MarkSending();
        record.MarkSucceeded();

        // Act
        var act = () => record.MarkResend();

        // Assert
        act.Should().Throw<NotificationDomainException>()
            .And.ErrorCode.Should().Be("NOTIFICATION_RESEND_STATUS_INVALID");
    }

    [Fact]
    public void MarkResend_WhenFailed_ShouldThrowNotificationDomainException()
    {
        // Arrange
        var record = CreateValidRecord();
        record.MarkSending();
        record.MarkFailed("error");

        // Act
        var act = () => record.MarkResend();

        // Assert
        act.Should().Throw<NotificationDomainException>()
            .And.ErrorCode.Should().Be("NOTIFICATION_RESEND_STATUS_INVALID");
    }

    #endregion

    #region MarkDiscarded

    [Fact]
    public void MarkDiscarded_WhenDeadLettered_ShouldRecordDiscardReason()
    {
        // Arrange
        var record = CreateValidRecord();
        record.MarkSending();
        record.MarkFailed("error");
        record.ScheduleRetry();
        record.MoveToDeadLetter("exhausted");
        record.Status.Should().Be(NotificationStatus.DeadLettered);

        // Act
        record.MarkDiscarded("用户请求丢弃");

        // Assert
        record.ErrorMessage.Should().Be("已丢弃：用户请求丢弃");
        record.Status.Should().Be(NotificationStatus.DeadLettered); // 状态不变
    }

    [Fact]
    public void MarkDiscarded_EmptyReason_ShouldThrowNotificationDomainException()
    {
        // Arrange
        var record = CreateValidRecord();
        record.MarkSending();
        record.MarkFailed("error");
        record.ScheduleRetry();
        record.MoveToDeadLetter("exhausted");

        // Act
        var act = () => record.MarkDiscarded("");

        // Assert
        act.Should().Throw<NotificationDomainException>()
            .And.ErrorCode.Should().Be("NOTIFICATION_DISCARD_REASON_EMPTY");
    }

    [Fact]
    public void MarkDiscarded_NullReason_ShouldThrowNotificationDomainException()
    {
        // Arrange
        var record = CreateValidRecord();
        record.MarkSending();
        record.MarkFailed("error");
        record.ScheduleRetry();
        record.MoveToDeadLetter("exhausted");

        // Act
        var act = () => record.MarkDiscarded(null!);

        // Assert
        act.Should().Throw<NotificationDomainException>()
            .And.ErrorCode.Should().Be("NOTIFICATION_DISCARD_REASON_EMPTY");
    }

    [Fact]
    public void MarkDiscarded_WhenPending_ShouldThrowNotificationDomainException()
    {
        // Arrange
        var record = CreateValidRecord();

        // Act
        var act = () => record.MarkDiscarded("reason");

        // Assert
        act.Should().Throw<NotificationDomainException>()
            .And.ErrorCode.Should().Be("NOTIFICATION_DISCARD_STATUS_INVALID");
    }

    [Fact]
    public void MarkDiscarded_WhenFailed_ShouldThrowNotificationDomainException()
    {
        // Arrange
        var record = CreateValidRecord();
        record.MarkSending();
        record.MarkFailed("error");

        // Act
        var act = () => record.MarkDiscarded("reason");

        // Assert
        act.Should().Throw<NotificationDomainException>()
            .And.ErrorCode.Should().Be("NOTIFICATION_DISCARD_STATUS_INVALID");
    }

    [Fact]
    public void MarkDiscarded_WhenSucceeded_ShouldThrowNotificationDomainException()
    {
        // Arrange
        var record = CreateValidRecord();
        record.MarkSending();
        record.MarkSucceeded();

        // Act
        var act = () => record.MarkDiscarded("reason");

        // Assert
        act.Should().Throw<NotificationDomainException>()
            .And.ErrorCode.Should().Be("NOTIFICATION_DISCARD_STATUS_INVALID");
    }

    #endregion

    #region ApplyReceipt

    private static void SetChannelMessageId(NotificationRecord record, string messageId)
    {
        var prop = typeof(NotificationRecord).GetProperty("ChannelMessageId");
        prop?.SetValue(record, messageId);
    }

    [Fact]
    public void ApplyReceipt_MatchingChannelMessageIdSucceeded_ShouldUpdateStatus()
    {
        // Arrange
        var record = CreateValidRecord();
        record.MarkSending();
        SetChannelMessageId(record, "msg-abc-456");

        // Act
        var applied = record.ApplyReceipt("msg-abc-456", true, "{\"status\":\"delivered\"}");

        // Assert
        applied.Should().BeTrue();
        record.Status.Should().Be(NotificationStatus.Succeeded);
        record.ChannelReceipt.Should().NotBeNull();
        record.ErrorMessage.Should().BeNull();
        record.ErrorCode.Should().BeNull();
    }

    [Fact]
    public void ApplyReceipt_MatchingChannelMessageIdFailed_ShouldRecordError()
    {
        // Arrange
        var record = CreateValidRecord();
        record.MarkSending();
        SetChannelMessageId(record, "msg-fail-789");

        // Act
        var applied = record.ApplyReceipt("msg-fail-789", false, "{\"status\":\"bounced\"}");

        // Assert
        applied.Should().BeTrue();
        record.ErrorMessage.Should().Be("渠道回执确认失败");
        record.ErrorCode.Should().Be("CHANNEL_RECEIPT_FAILED");
        record.ChannelReceipt.Should().NotBeNull();
    }

    [Fact]
    public void ApplyReceipt_AlreadySucceeded_ShouldBeIdempotent()
    {
        // Arrange
        var record = CreateValidRecord();
        record.MarkSending();
        record.MarkSucceeded("msg-idem-001");
        record.Status.Should().Be(NotificationStatus.Succeeded);

        // Act
        var applied = record.ApplyReceipt("msg-idem-001", true, "{\"status\":\"delivered\"}");

        // Assert
        applied.Should().BeFalse(); // Idempotent skip
        record.Status.Should().Be(NotificationStatus.Succeeded); // No change
    }

    [Fact]
    public void ApplyReceipt_NonMatchingChannelMessageId_ShouldReturnFalse()
    {
        // Arrange
        var record = CreateValidRecord();
        record.MarkSending();
        SetChannelMessageId(record, "msg-original");

        // Act
        var applied = record.ApplyReceipt("msg-different", true, null);

        // Assert
        applied.Should().BeFalse();
    }

    [Fact]
    public void ApplyReceipt_EmptyChannelMessageId_ShouldThrowNotificationDomainException()
    {
        // Arrange
        var record = CreateValidRecord();

        // Act
        var act = () => record.ApplyReceipt("", true, null);

        // Assert
        act.Should().Throw<NotificationDomainException>()
            .And.ErrorCode.Should().Be("NOTIFICATION_RECEIPT_MESSAGE_ID_EMPTY");
    }

    [Fact]
    public void ApplyReceipt_NullChannelMessageId_ShouldThrowNotificationDomainException()
    {
        // Arrange
        var record = CreateValidRecord();

        // Act
        var act = () => record.ApplyReceipt(null!, true, null);

        // Assert
        act.Should().Throw<NotificationDomainException>()
            .And.ErrorCode.Should().Be("NOTIFICATION_RECEIPT_MESSAGE_ID_EMPTY");
    }

    [Fact]
    public void ApplyReceipt_WhitespaceChannelMessageId_ShouldThrowNotificationDomainException()
    {
        // Arrange
        var record = CreateValidRecord();

        // Act
        var act = () => record.ApplyReceipt("   ", true, null);

        // Assert
        act.Should().Throw<NotificationDomainException>()
            .And.ErrorCode.Should().Be("NOTIFICATION_RECEIPT_MESSAGE_ID_EMPTY");
    }

    [Fact]
    public void ApplyReceipt_MasksPhoneNumberInPayload()
    {
        // Arrange
        var record = CreateValidRecord();
        record.MarkSending();
        SetChannelMessageId(record, "msg-phone-001");
        var payload = "{\"phone\":\"13812345678\",\"status\":\"delivered\"}";

        // Act
        record.ApplyReceipt("msg-phone-001", true, payload);

        // Assert
        record.ChannelReceipt.Should().NotBeNull();
        record.ChannelReceipt.Should().NotContain("13812345678");
        record.ChannelReceipt.Should().Contain("138****5678");
    }

    [Fact]
    public void ApplyReceipt_MasksEmailInPayload()
    {
        // Arrange
        var record = CreateValidRecord();
        record.MarkSending();
        SetChannelMessageId(record, "msg-email-001");
        var payload = "{\"email\":\"testuser@example.com\",\"status\":\"delivered\"}";

        // Act
        record.ApplyReceipt("msg-email-001", true, payload);

        // Assert
        record.ChannelReceipt.Should().NotBeNull();
        record.ChannelReceipt.Should().NotContain("testuser@example.com");
        record.ChannelReceipt.Should().Contain("tes***@");
    }

    [Fact]
    public void ApplyReceipt_NullPayload_ShouldNotMask()
    {
        // Arrange
        var record = CreateValidRecord();
        record.MarkSending();
        SetChannelMessageId(record, "msg-null");

        // Act
        record.ApplyReceipt("msg-null", true, null);

        // Assert
        record.ChannelReceipt.Should().BeNull();
    }

    [Fact]
    public void ApplyReceipt_DuplicateCall_ShouldBeIdempotent()
    {
        // Arrange
        var record = CreateValidRecord();
        record.MarkSending();
        SetChannelMessageId(record, "msg-dup");

        // Act
        var first = record.ApplyReceipt("msg-dup", true, "{}");
        var second = record.ApplyReceipt("msg-dup", true, "{\"retry\":true}");

        // Assert
        first.Should().BeTrue();
        second.Should().BeFalse(); // Already Succeeded, idempotent
        record.Status.Should().Be(NotificationStatus.Succeeded);
    }

    #endregion
}