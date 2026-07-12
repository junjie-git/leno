using Leno.Notification.Application.Services;
using Leno.Notification.Domain.Aggregates;
using Leno.Notification.Domain.Repositories;
using Leno.Notification.Domain.Services;
using Leno.Notification.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;
using Moq;

namespace Leno.Notification.Application.Tests;

public class NotificationServiceTests
{
    private static readonly Guid ValidUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private const string ValidTemplateCode = "OrderCreated";
    private const string ValidIdempotencyKey = "idem-key-123";
    private const string ValidBusinessRef = "ORD-001";

    private readonly Mock<INotificationTemplateRepository> _templateRepoMock;
    private readonly Mock<INotificationRecordRepository> _recordRepoMock;
    private readonly Mock<ITemplateRenderer> _rendererMock;
    private readonly Mock<INotificationChannel> _channelMock;
    private readonly Mock<IUserContactService> _userContactServiceMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ILogger<NotificationService>> _loggerMock;
    private readonly NotificationService _sut;

    public NotificationServiceTests()
    {
        _templateRepoMock = new Mock<INotificationTemplateRepository>(MockBehavior.Strict);
        _recordRepoMock = new Mock<INotificationRecordRepository>(MockBehavior.Strict);
        _rendererMock = new Mock<ITemplateRenderer>(MockBehavior.Strict);
        _channelMock = new Mock<INotificationChannel>(MockBehavior.Strict);
        _userContactServiceMock = new Mock<IUserContactService>(MockBehavior.Strict);
        _unitOfWorkMock = new Mock<IUnitOfWork>(MockBehavior.Strict);
        _loggerMock = new Mock<ILogger<NotificationService>>();

        _sut = new NotificationService(
            _templateRepoMock.Object,
            _recordRepoMock.Object,
            _rendererMock.Object,
            [_channelMock.Object],
            _userContactServiceMock.Object,
            _unitOfWorkMock.Object,
            _loggerMock.Object);
    }

    private static NotificationRequest CreateValidRequest(
        string? templateCode = null,
        Guid? userId = null,
        string? idempotencyKey = null,
        string? businessRef = null,
        Dictionary<string, string>? variables = null)
    {
        return new NotificationRequest
        {
            TemplateCode = templateCode ?? ValidTemplateCode,
            UserId = userId ?? ValidUserId,
            IdempotencyKey = idempotencyKey ?? ValidIdempotencyKey,
            BusinessRef = businessRef ?? ValidBusinessRef,
            Variables = variables ?? new Dictionary<string, string> { { "UserName", "Test" } }
        };
    }

    private static NotificationTemplate CreateEnabledTemplate(
        NotificationChannel channel = NotificationChannel.InApp,
        string code = ValidTemplateCode)
    {
        return NotificationTemplate.Create(
            Guid.NewGuid(), code, "Test Template", channel,
            "Hello {{UserName}}", "Your order {{OrderId}} is confirmed.",
            [TemplateVariable.Create("UserName"), TemplateVariable.Create("OrderId")]);
    }

    private void SetupTemplateLookup(NotificationTemplate? template)
    {
        _templateRepoMock
            .Setup(r => r.GetEnabledByCodeAsync(ValidTemplateCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);
    }

    private void SetupIdempotencyCheck(NotificationRecord? existing)
    {
        _recordRepoMock
            .Setup(r => r.GetByIdempotencyKeyAsync(ValidIdempotencyKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
    }

    private void SetupRenderSuccess(string title = "Hello Test", string content = "Your order ORD-001 is confirmed.")
    {
        _rendererMock
            .Setup(r => r.Render(It.IsAny<NotificationTemplate>(), It.IsAny<Dictionary<string, string>>()))
            .Returns((title, content));
    }

    private void SetupRecordAdd()
    {
        _recordRepoMock
            .Setup(r => r.AddAsync(It.IsAny<NotificationRecord>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
    }

    private void SetupChannel(NotificationChannel channelType = NotificationChannel.InApp)
    {
        _channelMock.Setup(c => c.Channel).Returns(channelType);
    }

    private void SetupUserContactService()
    {
        _userContactServiceMock
            .Setup(s => s.GetContactsAsync(ValidUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserContactInfo
            {
                UserId = ValidUserId,
                Email = "test@example.com",
                PhoneNumber = "13800138000"
            });
    }

    private void SetupChannelSendSuccess(string? channelMessageId = null)
    {
        _channelMock
            .Setup(c => c.SendAsync(It.IsAny<ChannelSendRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelSendResult(true, null, null, channelMessageId));
    }

    private void SetupRecordUpdate()
    {
        _recordRepoMock
            .Setup(r => r.UpdateAsync(It.IsAny<NotificationRecord>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    #region SendAsync - Success Path

    [Fact]
    public async Task SendAsync_ValidRequest_ShouldSucceed()
    {
        // Arrange
        var template = CreateEnabledTemplate();
        SetupTemplateLookup(template);
        SetupIdempotencyCheck(null);
        SetupRenderSuccess();
        SetupRecordAdd();
        SetupChannel(NotificationChannel.InApp);
        SetupUserContactService();
        SetupChannelSendSuccess("msg-001");
        SetupRecordUpdate();

        // Second SaveChangesAsync call (after status update)
        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var request = CreateValidRequest();

        // Act
        var result = await _sut.SendAsync(request);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.RecordId.Should().NotBeNull();
        result.RecordId.Should().NotBe(Guid.Empty);
        result.ErrorCode.Should().BeNull();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task SendAsync_ValidRequest_ShouldCreateRecordWithCorrectFields()
    {
        // Arrange
        var template = CreateEnabledTemplate();
        SetupTemplateLookup(template);
        SetupIdempotencyCheck(null);
        SetupRenderSuccess();
        SetupChannel(NotificationChannel.InApp);
        SetupUserContactService();
        SetupChannelSendSuccess("msg-001");

        NotificationRecord? capturedRecord = null;
        _recordRepoMock
            .Setup(r => r.AddAsync(It.IsAny<NotificationRecord>(), It.IsAny<CancellationToken>()))
            .Callback<NotificationRecord, CancellationToken>((r, _) => capturedRecord = r)
            .Returns(Task.CompletedTask);

        SetupRecordUpdate();
        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var request = CreateValidRequest();

        // Act
        await _sut.SendAsync(request);

        // Assert
        capturedRecord.Should().NotBeNull();
        capturedRecord!.UserId.Should().Be(ValidUserId);
        capturedRecord.TemplateCode.Should().Be(ValidTemplateCode);
        capturedRecord.Channel.Should().Be(NotificationChannel.InApp);
        capturedRecord.BusinessRef.Should().Be(ValidBusinessRef);
        capturedRecord.IdempotencyKey.Should().Be(ValidIdempotencyKey);
        capturedRecord.Status.Should().Be(NotificationStatus.Succeeded);
    }

    [Fact]
    public async Task SendAsync_WithoutIdempotencyKey_ShouldStillSucceed()
    {
        // Arrange
        var template = CreateEnabledTemplate();
        SetupTemplateLookup(template);
        // No idempotency check setup needed since key is empty
        SetupRenderSuccess();
        SetupRecordAdd();
        SetupChannel(NotificationChannel.InApp);
        SetupUserContactService();
        SetupChannelSendSuccess();
        SetupRecordUpdate();
        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var request = CreateValidRequest(idempotencyKey: "");

        // Act
        var result = await _sut.SendAsync(request);

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    #endregion

    #region SendAsync - Idempotency

    [Fact]
    public async Task SendAsync_DuplicateIdempotencyKey_ShouldReturnExistingRecord()
    {
        // Arrange
        var existingRecord = NotificationRecord.Create(
            Guid.NewGuid(), ValidUserId, ValidTemplateCode, null,
            NotificationChannel.InApp, "Hello", "Content",
            idempotencyKey: ValidIdempotencyKey);
        existingRecord.MarkSending();
        existingRecord.MarkSucceeded("msg-existing");

        SetupIdempotencyCheck(existingRecord);

        var request = CreateValidRequest();

        // Act
        var result = await _sut.SendAsync(request);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.RecordId.Should().Be(existingRecord.Id);
        result.ErrorCode.Should().BeNull();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task SendAsync_DuplicateIdempotencyKey_FailedRecord_ShouldReturnExistingInfo()
    {
        // Arrange
        var existingRecord = NotificationRecord.Create(
            Guid.NewGuid(), ValidUserId, ValidTemplateCode, null,
            NotificationChannel.InApp, "Hello", "Content",
            idempotencyKey: ValidIdempotencyKey);
        existingRecord.MarkSending();
        existingRecord.MarkFailed("Send failed", "SEND_FAILED");

        SetupIdempotencyCheck(existingRecord);

        var request = CreateValidRequest();

        // Act
        var result = await _sut.SendAsync(request);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.RecordId.Should().Be(existingRecord.Id);
        result.ErrorCode.Should().Be("SEND_FAILED");
        result.ErrorMessage.Should().Be("Send failed");
    }

    [Fact]
    public async Task SendAsync_EmptyIdempotencyKey_ShouldSkipIdempotencyCheck()
    {
        // Arrange
        var template = CreateEnabledTemplate();
        SetupTemplateLookup(template);
        SetupRenderSuccess();
        SetupRecordAdd();
        SetupChannel(NotificationChannel.InApp);
        SetupUserContactService();
        SetupChannelSendSuccess();
        SetupRecordUpdate();
        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var request = CreateValidRequest(idempotencyKey: "");

        // Act
        var result = await _sut.SendAsync(request);

        // Assert
        result.Succeeded.Should().BeTrue();
        // Verify idempotency check was NOT called
        _recordRepoMock.Verify(
            r => r.GetByIdempotencyKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region SendAsync - Template Not Found

    [Fact]
    public async Task SendAsync_TemplateNotFound_ShouldReturnError()
    {
        // Arrange
        SetupTemplateLookup(null);
        SetupIdempotencyCheck(null);

        var request = CreateValidRequest();

        // Act
        var result = await _sut.SendAsync(request);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be("TEMPLATE_NOT_FOUND");
        result.ErrorMessage.Should().Contain(ValidTemplateCode);
    }

    #endregion

    #region SendAsync - Render Failure

    [Fact]
    public async Task SendAsync_RenderThrows_ShouldReturnError()
    {
        // Arrange
        var template = CreateEnabledTemplate();
        SetupTemplateLookup(template);
        SetupIdempotencyCheck(null);

        _rendererMock
            .Setup(r => r.Render(It.IsAny<NotificationTemplate>(), It.IsAny<Dictionary<string, string>>()))
            .Throws(new InvalidOperationException("Variable 'OrderId' is required but not provided"));

        var request = CreateValidRequest();

        // Act
        var result = await _sut.SendAsync(request);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be("TEMPLATE_RENDER_FAILED");
        result.ErrorMessage.Should().Contain("Variable 'OrderId' is required");
    }

    #endregion

    #region SendAsync - Channel Send Failure

    [Fact]
    public async Task SendAsync_ChannelSendFails_ShouldReturnFailure()
    {
        // Arrange
        var template = CreateEnabledTemplate();
        SetupTemplateLookup(template);
        SetupIdempotencyCheck(null);
        SetupRenderSuccess();
        SetupRecordAdd();
        SetupChannel(NotificationChannel.InApp);
        SetupUserContactService();
        SetupRecordUpdate();

        _channelMock
            .Setup(c => c.SendAsync(It.IsAny<ChannelSendRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelSendResult(false, "Network error", "NET_ERR", null));

        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var request = CreateValidRequest();

        // Act
        var result = await _sut.SendAsync(request);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.RecordId.Should().NotBeNull();
        result.ErrorCode.Should().Be("NET_ERR");
        result.ErrorMessage.Should().Be("Network error");
    }

    [Fact]
    public async Task SendAsync_ChannelThrowsException_ShouldReturnFailure()
    {
        // Arrange
        var template = CreateEnabledTemplate();
        SetupTemplateLookup(template);
        SetupIdempotencyCheck(null);
        SetupRenderSuccess();
        SetupRecordAdd();
        SetupChannel(NotificationChannel.InApp);
        SetupUserContactService();
        SetupRecordUpdate();

        _channelMock
            .Setup(c => c.SendAsync(It.IsAny<ChannelSendRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SMTP connection failed"));

        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var request = CreateValidRequest();

        // Act
        var result = await _sut.SendAsync(request);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.RecordId.Should().NotBeNull();
        result.ErrorCode.Should().Be("SEND_EXCEPTION");
        result.ErrorMessage.Should().Contain("SMTP connection failed");
    }

    #endregion

    #region SendAsync - Timeout

    [Fact]
    public async Task SendAsync_ChannelSendTimesOut_ShouldReturnAccepted()
    {
        // Arrange
        var template = CreateEnabledTemplate();
        SetupTemplateLookup(template);
        SetupIdempotencyCheck(null);
        SetupRenderSuccess();
        SetupRecordAdd();
        SetupChannel(NotificationChannel.InApp);
        SetupUserContactService();
        SetupRecordUpdate();

        // Simulate timeout by delaying beyond the 3s timeout
        _channelMock
            .Setup(c => c.SendAsync(It.IsAny<ChannelSendRequest>(), It.IsAny<CancellationToken>()))
            .Returns(async (ChannelSendRequest r, CancellationToken ct) =>
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(10), ct);
                    return new ChannelSendResult(true, null, null, null);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
            });

        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var request = CreateValidRequest();

        // Act
        var result = await _sut.SendAsync(request);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.RecordId.Should().NotBeNull();
        result.ErrorCode.Should().Be("ACCEPTED_TIMEOUT");
        result.ErrorMessage.Should().Contain("异步处理");
    }

    #endregion

    #region SendAsync - Channel Not Found

    [Fact]
    public async Task SendAsync_NoMatchingChannel_ShouldReturnError()
    {
        // Arrange
        var template = CreateEnabledTemplate(NotificationChannel.Sms);
        SetupTemplateLookup(template);
        SetupIdempotencyCheck(null);
        SetupRenderSuccess();
        SetupRecordAdd();

        // Only InApp channel registered, template requires Sms
        SetupChannel(NotificationChannel.InApp);

        var request = CreateValidRequest();

        // Act
        var result = await _sut.SendAsync(request);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.RecordId.Should().NotBeNull();
        result.ErrorCode.Should().Be("CHANNEL_NOT_FOUND");
        result.ErrorMessage.Should().Contain("Sms");
    }

    #endregion

    #region SendAsync - Edge Cases

    [Fact]
    public async Task SendAsync_ChannelSendResultNullMessage_ShouldDefaultMessage()
    {
        // Arrange
        var template = CreateEnabledTemplate();
        SetupTemplateLookup(template);
        SetupIdempotencyCheck(null);
        SetupRenderSuccess();
        SetupRecordAdd();
        SetupChannel(NotificationChannel.InApp);
        SetupUserContactService();
        SetupRecordUpdate();

        _channelMock
            .Setup(c => c.SendAsync(It.IsAny<ChannelSendRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelSendResult(false, null, null, null));

        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var request = CreateValidRequest();

        // Act
        var result = await _sut.SendAsync(request);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.ErrorMessage.Should().Be("发送失败");
    }

    [Fact]
    public async Task SendAsync_WithEmptyVariables_ShouldStillRender()
    {
        // Arrange
        var template = CreateEnabledTemplate();
        SetupTemplateLookup(template);
        SetupIdempotencyCheck(null);

        _rendererMock
            .Setup(r => r.Render(It.IsAny<NotificationTemplate>(), It.IsAny<Dictionary<string, string>>()))
            .Returns(("Hello", "Your order is confirmed."));

        SetupRecordAdd();
        SetupChannel(NotificationChannel.InApp);
        SetupUserContactService();
        SetupChannelSendSuccess();
        SetupRecordUpdate();
        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var request = CreateValidRequest(variables: []);

        // Act
        var result = await _sut.SendAsync(request);

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    #endregion
}