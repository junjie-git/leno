using Leno.Notification.Domain.Aggregates;
using Leno.Notification.Domain.Exceptions;
using Leno.Notification.Domain.ValueObjects;

namespace Leno.Notification.Domain.Tests;

public class NotificationConfigTests
{
    private static readonly Guid ValidConfigId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private const NotificationChannel ValidChannel = NotificationChannel.Email;
    private const string ValidConfigKey = "Host";
    private const string ValidConfigValue = "smtp.example.com";

    #region Create - Happy Path

    [Fact]
    public void Create_ValidParameters_ShouldCreateConfig()
    {
        // Act
        var config = NotificationConfig.Create(
            ValidConfigId, ValidChannel, ValidConfigKey, ValidConfigValue,
            "SMTP 主机地址", isSensitive: false);

        // Assert
        config.Id.Should().Be(ValidConfigId);
        config.Channel.Should().Be(ValidChannel);
        config.ConfigKey.Should().Be(ValidConfigKey);
        config.ConfigValue.Should().Be(ValidConfigValue);
        config.Description.Should().Be("SMTP 主机地址");
        config.IsSensitive.Should().BeFalse();
    }

    [Fact]
    public void Create_WithSmsChannel_ShouldCreateConfig()
    {
        // Act
        var config = NotificationConfig.Create(
            ValidConfigId, NotificationChannel.Sms, "AccessKeyId", "AKID123");

        // Assert
        config.Channel.Should().Be(NotificationChannel.Sms);
    }

    [Fact]
    public void Create_WithSensitiveFlag_ShouldSetIsSensitive()
    {
        // Act
        var config = NotificationConfig.Create(
            ValidConfigId, ValidChannel, "Password", "secret", isSensitive: true);

        // Assert
        config.IsSensitive.Should().BeTrue();
    }

    [Fact]
    public void Create_EmptyDescription_ShouldAllowNullDescription()
    {
        // Act
        var config = NotificationConfig.Create(
            ValidConfigId, ValidChannel, ValidConfigKey, ValidConfigValue);

        // Assert
        config.Description.Should().BeNull();
    }

    [Fact]
    public void Create_EmptyConfigValue_ShouldAllowEmptyString()
    {
        // Act - 空字符串视为清空配置
        var config = NotificationConfig.Create(
            ValidConfigId, ValidChannel, ValidConfigKey, "");

        // Assert
        config.ConfigValue.Should().Be("");
    }

    #endregion

    #region Create - Validation

    [Fact]
    public void Create_EmptyId_ShouldThrowNotificationDomainException()
    {
        var act = () => NotificationConfig.Create(
            Guid.Empty, ValidChannel, ValidConfigKey, ValidConfigValue);

        act.Should().Throw<NotificationDomainException>()
            .And.ErrorCode.Should().Be("NOTIFICATION_CONFIG_ID_EMPTY");
    }

    [Fact]
    public void Create_InvalidChannel_ShouldThrowNotificationDomainException()
    {
        var act = () => NotificationConfig.Create(
            ValidConfigId, (NotificationChannel)999, ValidConfigKey, ValidConfigValue);

        act.Should().Throw<NotificationDomainException>()
            .And.ErrorCode.Should().Be("NOTIFICATION_CONFIG_CHANNEL_INVALID");
    }

    [Fact]
    public void Create_NullConfigKey_ShouldThrowNotificationDomainException()
    {
        var act = () => NotificationConfig.Create(
            ValidConfigId, ValidChannel, null!, ValidConfigValue);

        act.Should().Throw<NotificationDomainException>()
            .And.ErrorCode.Should().Be("NOTIFICATION_CONFIG_KEY_EMPTY");
    }

    [Fact]
    public void Create_EmptyConfigKey_ShouldThrowNotificationDomainException()
    {
        var act = () => NotificationConfig.Create(
            ValidConfigId, ValidChannel, "", ValidConfigValue);

        act.Should().Throw<NotificationDomainException>()
            .And.ErrorCode.Should().Be("NOTIFICATION_CONFIG_KEY_EMPTY");
    }

    [Fact]
    public void Create_WhitespaceConfigKey_ShouldThrowNotificationDomainException()
    {
        var act = () => NotificationConfig.Create(
            ValidConfigId, ValidChannel, "   ", ValidConfigValue);

        act.Should().Throw<NotificationDomainException>()
            .And.ErrorCode.Should().Be("NOTIFICATION_CONFIG_KEY_EMPTY");
    }

    [Fact]
    public void Create_NullConfigValue_ShouldThrowNotificationDomainException()
    {
        var act = () => NotificationConfig.Create(
            ValidConfigId, ValidChannel, ValidConfigKey, null!);

        act.Should().Throw<NotificationDomainException>()
            .And.ErrorCode.Should().Be("NOTIFICATION_CONFIG_VALUE_NULL");
    }

    #endregion

    #region UpdateValue

    [Fact]
    public void UpdateValue_ValidValue_ShouldUpdateConfigValue()
    {
        // Arrange
        var config = NotificationConfig.Create(
            ValidConfigId, ValidChannel, ValidConfigKey, ValidConfigValue);

        // Act
        config.UpdateValue("new-smtp.example.com");

        // Assert
        config.ConfigValue.Should().Be("new-smtp.example.com");
    }

    [Fact]
    public void UpdateValue_WithDescription_ShouldUpdateDescription()
    {
        // Arrange
        var config = NotificationConfig.Create(
            ValidConfigId, ValidChannel, ValidConfigKey, ValidConfigValue);

        // Act
        config.UpdateValue("new-smtp.example.com", "新描述");

        // Assert
        config.ConfigValue.Should().Be("new-smtp.example.com");
        config.Description.Should().Be("新描述");
    }

    [Fact]
    public void UpdateValue_NullDescription_ShouldKeepOriginalDescription()
    {
        // Arrange
        var config = NotificationConfig.Create(
            ValidConfigId, ValidChannel, ValidConfigKey, ValidConfigValue, "原描述");

        // Act
        config.UpdateValue("new-smtp.example.com");

        // Assert - description 为 null 时不修改原描述
        config.Description.Should().Be("原描述");
    }

    [Fact]
    public void UpdateValue_EmptyString_ShouldAllowClearingValue()
    {
        // Arrange
        var config = NotificationConfig.Create(
            ValidConfigId, ValidChannel, ValidConfigKey, ValidConfigValue);

        // Act
        config.UpdateValue("");

        // Assert - 空字符串视为清空配置
        config.ConfigValue.Should().Be("");
    }

    [Fact]
    public void UpdateValue_NullValue_ShouldThrowNotificationDomainException()
    {
        // Arrange
        var config = NotificationConfig.Create(
            ValidConfigId, ValidChannel, ValidConfigKey, ValidConfigValue);

        // Act
        var act = () => config.UpdateValue(null!);

        // Assert
        act.Should().Throw<NotificationDomainException>()
            .And.ErrorCode.Should().Be("NOTIFICATION_CONFIG_VALUE_NULL");
    }

    #endregion
}
