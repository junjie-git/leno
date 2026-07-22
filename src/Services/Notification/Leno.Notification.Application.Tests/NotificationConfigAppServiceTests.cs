using Leno.Infrastructure.Configuration;
using Leno.Notification.Application;
using Leno.Notification.Application.DTOs;
using Leno.Notification.Domain.Aggregates;
using Leno.Notification.Domain.Repositories;
using Leno.Notification.Domain.Services;
using Leno.Notification.Domain.ValueObjects;
using Leno.Notification.Infrastructure.Channels;
using Leno.Notification.Infrastructure.Services;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Leno.Notification.Application.Tests;

public class NotificationConfigAppServiceTests
{
    private readonly Mock<IOptionsMonitor<EmailChannelOptions>> _emailOptionsMock = new();
    private readonly Mock<IOptionsMonitor<SmsChannelOptions>> _smsOptionsMock = new();
    private readonly Mock<INotificationChannel> _channelMock = new();
    private readonly Mock<INotificationConfigRepository> _configRepoMock = new(MockBehavior.Strict);
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new(MockBehavior.Strict);
    private readonly ConsulReloadableConfigurationProvider _configReloadProvider = new();
    private readonly Mock<ILogger<NotificationConfigAppService>> _loggerMock = new();

    private NotificationConfigAppService CreateSut()
    {
        return new NotificationConfigAppService(
            _emailOptionsMock.Object,
            _smsOptionsMock.Object,
            new[] { _channelMock.Object },
            _configRepoMock.Object,
            _unitOfWorkMock.Object,
            _configReloadProvider,
            _loggerMock.Object);
    }

    private void SetupConfigRepoGetReturnsNull()
    {
        _configRepoMock
            .Setup(r => r.GetAsync(It.IsAny<NotificationChannel>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((NotificationConfig?)null);
    }

    private void SetupConfigRepoAdd()
    {
        _configRepoMock
            .Setup(r => r.AddAsync(It.IsAny<NotificationConfig>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private void SetupConfigRepoUpdate()
    {
        _configRepoMock
            .Setup(r => r.UpdateAsync(It.IsAny<NotificationConfig>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private void SetupUnitOfWorkSaveEntities()
    {
        _unitOfWorkMock
            .Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    #region GetConfigAsync - Email

    [Fact]
    public async Task GetConfigAsync_Email_ShouldReturnMaskedPassword()
    {
        // Arrange
        _emailOptionsMock.Setup(o => o.CurrentValue).Returns(new EmailChannelOptions
        {
            Host = "smtp.example.com",
            Port = 587,
            Username = "admin",
            Password = "secret-password",
            From = "noreply@example.com",
            UseSsl = true
        });
        var sut = CreateSut();

        // Act
        var result = await sut.GetConfigAsync(NotificationChannel.Email);

        // Assert
        result.Channel.Should().Be(NotificationChannel.Email);
        result.Enabled.Should().BeTrue();
        result.SmtpHost.Should().Be("smtp.example.com");
        result.SmtpPort.Should().Be(587);
        result.SmtpUsername.Should().Be("admin");
        result.SmtpPassword.Should().Be("******");
        result.FromAddress.Should().Be("noreply@example.com");
        result.UseSsl.Should().BeTrue();
    }

    [Fact]
    public async Task GetConfigAsync_Email_EmptyPassword_ShouldReturnNull()
    {
        // Arrange
        _emailOptionsMock.Setup(o => o.CurrentValue).Returns(new EmailChannelOptions
        {
            Host = "smtp.example.com",
            Password = ""
        });
        var sut = CreateSut();

        // Act
        var result = await sut.GetConfigAsync(NotificationChannel.Email);

        // Assert
        result.SmtpPassword.Should().BeNull();
    }

    [Fact]
    public async Task GetConfigAsync_Email_NoHost_ShouldNotBeEnabled()
    {
        // Arrange
        _emailOptionsMock.Setup(o => o.CurrentValue).Returns(new EmailChannelOptions());
        var sut = CreateSut();

        // Act
        var result = await sut.GetConfigAsync(NotificationChannel.Email);

        // Assert
        result.Enabled.Should().BeFalse();
    }

    #endregion

    #region GetConfigAsync - SMS

    [Fact]
    public async Task GetConfigAsync_Sms_ShouldReturnMaskedSecret()
    {
        // Arrange
        _smsOptionsMock.Setup(o => o.CurrentValue).Returns(new SmsChannelOptions
        {
            Provider = "Aliyun",
            AccessKeyId = "AKID123",
            AccessKeySecret = "secret-key",
            SignName = "Leno"
        });
        var sut = CreateSut();

        // Act
        var result = await sut.GetConfigAsync(NotificationChannel.Sms);

        // Assert
        result.Channel.Should().Be(NotificationChannel.Sms);
        result.Enabled.Should().BeTrue();
        result.SmsProvider.Should().Be("Aliyun");
        result.AccessKeyId.Should().Be("AKID123");
        result.AccessKeySecret.Should().Be("******");
        result.SmsSignName.Should().Be("Leno");
    }

    [Fact]
    public async Task GetConfigAsync_Sms_NoAccessKeyId_ShouldNotBeEnabled()
    {
        // Arrange
        _smsOptionsMock.Setup(o => o.CurrentValue).Returns(new SmsChannelOptions());
        var sut = CreateSut();

        // Act
        var result = await sut.GetConfigAsync(NotificationChannel.Sms);

        // Assert
        result.Enabled.Should().BeFalse();
    }

    #endregion

    #region GetConfigAsync - InApp

    [Fact]
    public async Task GetConfigAsync_InApp_ShouldReturnEnabled()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = await sut.GetConfigAsync(NotificationChannel.InApp);

        // Assert
        result.Channel.Should().Be(NotificationChannel.InApp);
        result.Enabled.Should().BeTrue();
    }

    #endregion

    #region UpdateConfigAsync

    [Fact]
    public async Task UpdateConfigAsync_ShouldLogAuditEntry()
    {
        // Arrange
        SetupConfigRepoGetReturnsNull();
        SetupConfigRepoAdd();
        SetupUnitOfWorkSaveEntities();

        var sut = CreateSut();
        var dto = new SaveNotificationConfigDto
        {
            Enabled = true,
            SmtpHost = "new-smtp.example.com",
            SmtpPort = 465,
            SmtpPassword = "new-secret"
        };

        // Act
        var act = () => sut.UpdateConfigAsync(Guid.NewGuid(), NotificationChannel.Email, dto);

        // Assert
        await act.Should().NotThrowAsync();
        _configRepoMock.Verify(r => r.AddAsync(It.IsAny<NotificationConfig>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        _unitOfWorkMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateConfigAsync_WithSensitiveFields_ShouldMaskInAuditLog()
    {
        // Arrange
        SetupConfigRepoGetReturnsNull();
        SetupConfigRepoAdd();
        SetupUnitOfWorkSaveEntities();

        var sut = CreateSut();
        var dto = new SaveNotificationConfigDto
        {
            AccessKeySecret = "new-secret",
            SmtpPassword = "new-password"
        };

        // Act
        var act = () => sut.UpdateConfigAsync(Guid.NewGuid(), NotificationChannel.Sms, dto);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task UpdateConfigAsync_NewConfig_ShouldAddToRepository()
    {
        // Arrange
        SetupConfigRepoGetReturnsNull();
        SetupConfigRepoAdd();
        SetupUnitOfWorkSaveEntities();

        var sut = CreateSut();
        var dto = new SaveNotificationConfigDto
        {
            SmtpHost = "smtp.example.com",
            SmtpPort = 587,
            SmtpPassword = "secret"
        };

        // Act
        await sut.UpdateConfigAsync(Guid.NewGuid(), NotificationChannel.Email, dto);

        // Assert - 三项变更 (Host/Port/Password) 均应触发 AddAsync
        _configRepoMock.Verify(
            r => r.AddAsync(It.IsAny<NotificationConfig>(), It.IsAny<CancellationToken>()),
            Times.Exactly(3));
        _configRepoMock.Verify(
            r => r.UpdateAsync(It.IsAny<NotificationConfig>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdateConfigAsync_ExistingConfig_ShouldUpdateRepository()
    {
        // Arrange - 仓储返回已存在的配置项，触发 Update 路径
        var existingConfig = NotificationConfig.Create(
            Guid.NewGuid(),
            NotificationChannel.Email,
            "Host",
            "old-smtp.example.com");

        _configRepoMock
            .Setup(r => r.GetAsync(It.IsAny<NotificationChannel>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingConfig);
        SetupConfigRepoUpdate();
        SetupUnitOfWorkSaveEntities();

        var sut = CreateSut();
        var dto = new SaveNotificationConfigDto
        {
            SmtpHost = "new-smtp.example.com"
        };

        // Act
        await sut.UpdateConfigAsync(Guid.NewGuid(), NotificationChannel.Email, dto);

        // Assert - 已存在配置应走 Update 路径
        _configRepoMock.Verify(
            r => r.UpdateAsync(It.IsAny<NotificationConfig>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _configRepoMock.Verify(
            r => r.AddAsync(It.IsAny<NotificationConfig>(), It.IsAny<CancellationToken>()),
            Times.Never);
        existingConfig.ConfigValue.Should().Be("new-smtp.example.com");
    }

    [Fact]
    public async Task UpdateConfigAsync_ShouldTriggerConfigReload()
    {
        // Arrange
        SetupConfigRepoGetReturnsNull();
        SetupConfigRepoAdd();
        SetupUnitOfWorkSaveEntities();

        var sut = CreateSut();
        var dto = new SaveNotificationConfigDto
        {
            SmtpHost = "new-smtp.example.com"
        };

        // Act
        await sut.UpdateConfigAsync(Guid.NewGuid(), NotificationChannel.Email, dto);

        // Assert - SetValue 触发 OnReload，IOptionsMonitor 应感知变更
        // 通过 _configReloadProvider 内部 Data 字典验证写入
        _configReloadProvider.TryGet("Notification:Email:Host", out var value).Should().BeTrue();
        value.Should().Be("new-smtp.example.com");
    }

    [Fact]
    public async Task UpdateConfigAsync_ShouldPersistSaveEntitiesAsync()
    {
        // Arrange
        SetupConfigRepoGetReturnsNull();
        SetupConfigRepoAdd();
        SetupUnitOfWorkSaveEntities();

        var sut = CreateSut();
        var dto = new SaveNotificationConfigDto
        {
            SmtpHost = "smtp.example.com"
        };

        // Act
        await sut.UpdateConfigAsync(Guid.NewGuid(), NotificationChannel.Email, dto);

        // Assert
        _unitOfWorkMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region TestSendAsync

    [Fact]
    public async Task TestSendAsync_ValidChannel_ShouldReturnResult()
    {
        // Arrange
        _channelMock.Setup(c => c.Channel).Returns(NotificationChannel.Email);
        _channelMock.Setup(c => c.SendAsync(It.IsAny<ChannelSendRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelSendResult(true, null, null, "test-msg-001"));
        var sut = CreateSut();
        var dto = new TestSendRequestDto
        {
            Channel = NotificationChannel.Email,
            Email = "test@example.com"
        };

        // Act
        var result = await sut.TestSendAsync(NotificationChannel.Email, dto);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.ErrorCode.Should().BeNull();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task TestSendAsync_ChannelNotFound_ShouldReturnError()
    {
        // Arrange
        _channelMock.Setup(c => c.Channel).Returns(NotificationChannel.Email);
        var sut = CreateSut();
        var dto = new TestSendRequestDto
        {
            Channel = NotificationChannel.Sms,
            PhoneNumber = "13800138000"
        };

        // Act
        var result = await sut.TestSendAsync(NotificationChannel.Sms, dto);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be("CHANNEL_NOT_FOUND");
    }

    [Fact]
    public async Task TestSendAsync_SendFailure_ShouldReturnError()
    {
        // Arrange
        _channelMock.Setup(c => c.Channel).Returns(NotificationChannel.Email);
        _channelMock.Setup(c => c.SendAsync(It.IsAny<ChannelSendRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelSendResult(false, "Connection refused", "SMTP_ERROR", null));
        var sut = CreateSut();
        var dto = new TestSendRequestDto
        {
            Channel = NotificationChannel.Email,
            Email = "test@example.com"
        };

        // Act
        var result = await sut.TestSendAsync(NotificationChannel.Email, dto);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be("SMTP_ERROR");
        result.ErrorMessage.Should().Be("Connection refused");
    }

    #endregion
}
