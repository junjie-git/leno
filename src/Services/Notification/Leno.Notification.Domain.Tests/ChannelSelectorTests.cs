using Leno.Notification.Domain.Exceptions;
using Leno.Notification.Domain.Services;
using Leno.Notification.Domain.ValueObjects;

namespace Leno.Notification.Domain.Tests;

public class ChannelSelectorTests
{
    #region SelectProvider

    [Fact]
    public void SelectProvider_EmailChannel_ShouldReturnSmtp()
    {
        // Arrange
        var selector = new ChannelSelector();

        // Act
        var provider = selector.SelectProvider(NotificationChannel.Email);

        // Assert
        provider.Should().Be("SMTP");
    }

    [Fact]
    public void SelectProvider_SmsChannelDefaultProvider_ShouldReturnAliyun()
    {
        // Arrange
        var selector = new ChannelSelector();

        // Act
        var provider = selector.SelectProvider(NotificationChannel.Sms);

        // Assert
        provider.Should().Be("Aliyun");
    }

    [Fact]
    public void SelectProvider_SmsChannelAliyunProvider_ShouldReturnAliyun()
    {
        // Arrange
        var selector = new ChannelSelector("Aliyun");

        // Act
        var provider = selector.SelectProvider(NotificationChannel.Sms);

        // Assert
        provider.Should().Be("Aliyun");
    }

    [Fact]
    public void SelectProvider_SmsChannelTencentProvider_ShouldReturnTencent()
    {
        // Arrange
        var selector = new ChannelSelector("Tencent");

        // Act
        var provider = selector.SelectProvider(NotificationChannel.Sms);

        // Assert
        provider.Should().Be("Tencent");
    }

    [Fact]
    public void SelectProvider_InAppChannel_ShouldReturnInApp()
    {
        // Arrange
        var selector = new ChannelSelector();

        // Act
        var provider = selector.SelectProvider(NotificationChannel.InApp);

        // Assert
        provider.Should().Be("InApp");
    }

    [Fact]
    public void SelectProvider_InvalidChannel_ShouldThrowNotificationDomainException()
    {
        // Arrange
        var selector = new ChannelSelector();

        // Act
        var act = () => selector.SelectProvider((NotificationChannel)999);

        // Assert
        act.Should().Throw<NotificationDomainException>()
            .And.ErrorCode.Should().Be("CHANNEL_SELECTOR_UNKNOWN_CHANNEL");
    }

    #endregion

    #region SelectFallbackProvider

    [Fact]
    public void SelectFallbackProvider_EmailChannel_ShouldReturnNull()
    {
        // Arrange
        var selector = new ChannelSelector();

        // Act
        var fallback = selector.SelectFallbackProvider(NotificationChannel.Email);

        // Assert
        fallback.Should().BeNull();
    }

    [Fact]
    public void SelectFallbackProvider_SmsWithAliyunPrimary_ShouldReturnTencent()
    {
        // Arrange
        var selector = new ChannelSelector("Aliyun");

        // Act
        var fallback = selector.SelectFallbackProvider(NotificationChannel.Sms);

        // Assert
        fallback.Should().Be("Tencent");
    }

    [Fact]
    public void SelectFallbackProvider_SmsWithTencentPrimary_ShouldReturnAliyun()
    {
        // Arrange
        var selector = new ChannelSelector("Tencent");

        // Act
        var fallback = selector.SelectFallbackProvider(NotificationChannel.Sms);

        // Assert
        fallback.Should().Be("Aliyun");
    }

    [Fact]
    public void SelectFallbackProvider_InAppChannel_ShouldReturnNull()
    {
        // Arrange
        var selector = new ChannelSelector();

        // Act
        var fallback = selector.SelectFallbackProvider(NotificationChannel.InApp);

        // Assert
        fallback.Should().BeNull();
    }

    #endregion

    #region IsRetryableError

    [Fact]
    public void IsRetryableError_SmtpRetryable_ShouldReturnTrue()
    {
        // Arrange
        var selector = new ChannelSelector();

        // Act
        var result = selector.IsRetryableError("SMTP_RETRYABLE");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsRetryableError_SmtpConnectTimeout_ShouldReturnTrue()
    {
        // Arrange
        var selector = new ChannelSelector();

        // Act
        var result = selector.IsRetryableError("SMTP_CONNECT_TIMEOUT");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsRetryableError_SmsTimeout_ShouldReturnTrue()
    {
        // Arrange
        var selector = new ChannelSelector();

        // Act
        var result = selector.IsRetryableError("SMS_TIMEOUT");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsRetryableError_EmailException_ShouldReturnTrue()
    {
        // Arrange
        var selector = new ChannelSelector();

        // Act
        var result = selector.IsRetryableError("EMAIL_EXCEPTION");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsRetryableError_SmsException_ShouldReturnTrue()
    {
        // Arrange
        var selector = new ChannelSelector();

        // Act
        var result = selector.IsRetryableError("SMS_EXCEPTION");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsRetryableError_SendException_ShouldReturnTrue()
    {
        // Arrange
        var selector = new ChannelSelector();

        // Act
        var result = selector.IsRetryableError("SEND_EXCEPTION");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsRetryableError_AcceptedTimeout_ShouldReturnTrue()
    {
        // Arrange
        var selector = new ChannelSelector();

        // Act
        var result = selector.IsRetryableError("ACCEPTED_TIMEOUT");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsRetryableError_FiveXxError_ShouldReturnTrue()
    {
        // Arrange
        var selector = new ChannelSelector();

        // Act
        var result = selector.IsRetryableError("500");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsRetryableError_SmtpNonRetryable_ShouldReturnFalse()
    {
        // Arrange
        var selector = new ChannelSelector();

        // Act
        var result = selector.IsRetryableError("SMTP_NON_RETRYABLE");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsRetryableError_EmailEmpty_ShouldReturnFalse()
    {
        // Arrange
        var selector = new ChannelSelector();

        // Act
        var result = selector.IsRetryableError("EMAIL_EMPTY");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsRetryableError_EmailConfigMissing_ShouldReturnFalse()
    {
        // Arrange
        var selector = new ChannelSelector();

        // Act
        var result = selector.IsRetryableError("EMAIL_CONFIG_MISSING");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsRetryableError_SmsPhoneEmpty_ShouldReturnFalse()
    {
        // Arrange
        var selector = new ChannelSelector();

        // Act
        var result = selector.IsRetryableError("SMS_PHONE_EMPTY");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsRetryableError_SmsConfigMissing_ShouldReturnFalse()
    {
        // Arrange
        var selector = new ChannelSelector();

        // Act
        var result = selector.IsRetryableError("SMS_CONFIG_MISSING");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsRetryableError_SmsHttpError_ShouldReturnFalse()
    {
        // Arrange
        var selector = new ChannelSelector();

        // Act
        var result = selector.IsRetryableError("SMS_HTTP_ERROR");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsRetryableError_TemplateNotFound_ShouldReturnFalse()
    {
        // Arrange
        var selector = new ChannelSelector();

        // Act
        var result = selector.IsRetryableError("TEMPLATE_NOT_FOUND");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsRetryableError_ChannelNotFound_ShouldReturnFalse()
    {
        // Arrange
        var selector = new ChannelSelector();

        // Act
        var result = selector.IsRetryableError("CHANNEL_NOT_FOUND");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsRetryableError_NullErrorCode_ShouldReturnFalse()
    {
        // P2-42：未知/空错误码默认不可重试，与 RetryPolicy.ShouldRetry 行为对齐
        // Arrange
        var selector = new ChannelSelector();

        // Act
        var result = selector.IsRetryableError(null);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsRetryableError_EmptyErrorCode_ShouldReturnFalse()
    {
        // P2-42：未知/空错误码默认不可重试，与 RetryPolicy.ShouldRetry 行为对齐
        // Arrange
        var selector = new ChannelSelector();

        // Act
        var result = selector.IsRetryableError("");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsRetryableError_WhitespaceErrorCode_ShouldReturnFalse()
    {
        // P2-42：未知/空错误码默认不可重试，与 RetryPolicy.ShouldRetry 行为对齐
        // Arrange
        var selector = new ChannelSelector();

        // Act
        var result = selector.IsRetryableError("   ");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsRetryableError_UnknownErrorCode_ShouldReturnFalse()
    {
        // P2-42：未在白名单内的未知错误码默认不可重试，不触发 failover
        // Arrange
        var selector = new ChannelSelector();

        // Act
        var result = selector.IsRetryableError("UNKNOWN_ERROR_CODE");

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region ShouldFailover

    [Fact]
    public void ShouldFailover_SmsWithRetryableError_ShouldReturnTrue()
    {
        // Arrange
        var selector = new ChannelSelector("Aliyun");

        // Act
        var result = selector.ShouldFailover(NotificationChannel.Sms, "SMS_TIMEOUT");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ShouldFailover_SmsWithNonRetryableError_ShouldReturnFalse()
    {
        // Arrange
        var selector = new ChannelSelector("Aliyun");

        // Act
        var result = selector.ShouldFailover(NotificationChannel.Sms, "SMS_PHONE_EMPTY");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ShouldFailover_EmailWithRetryableError_ShouldReturnFalse_NoFallback()
    {
        // Arrange
        var selector = new ChannelSelector();

        // Act
        var result = selector.ShouldFailover(NotificationChannel.Email, "SMTP_RETRYABLE");

        // Assert
        result.Should().BeFalse(); // Email has no fallback provider
    }

    [Fact]
    public void ShouldFailover_EmailWithNonRetryableError_ShouldReturnFalse()
    {
        // Arrange
        var selector = new ChannelSelector();

        // Act
        var result = selector.ShouldFailover(NotificationChannel.Email, "SMTP_NON_RETRYABLE");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ShouldFailover_InAppWithRetryableError_ShouldReturnFalse_NoFallback()
    {
        // Arrange
        var selector = new ChannelSelector();

        // Act
        var result = selector.ShouldFailover(NotificationChannel.InApp, "ACCEPTED_TIMEOUT");

        // Assert
        result.Should().BeFalse(); // InApp has no fallback
    }

    [Fact]
    public void ShouldFailover_CrossChannelFails_EmailCannotFailoverToSms()
    {
        // Email channel never has an SMS fallback, so cross-channel failover is impossible
        // Arrange
        var selector = new ChannelSelector();

        // Act
        var result = selector.ShouldFailover(NotificationChannel.Email, "SMTP_CONNECT_TIMEOUT");

        // Assert
        result.Should().BeFalse(); // Email cannot failover to anything
    }

    [Fact]
    public void ShouldFailover_SmsCannotFailoverToEmail()
    {
        // SMS fallback is always another SMS provider, never Email
        // Arrange
        var selector = new ChannelSelector("Aliyun");

        // Act
        var fallback = selector.SelectFallbackProvider(NotificationChannel.Sms);

        // Assert
        fallback.Should().NotBe("SMTP");
        fallback.Should().NotBe("InApp");
        fallback.Should().Be("Tencent"); // Only another SMS provider
    }

    #endregion

    #region Constructor Validation

    [Fact]
    public void Constructor_NullSmsProvider_ShouldThrowNotificationDomainException()
    {
        // Act
        var act = () => new ChannelSelector(null!);

        // Assert
        act.Should().Throw<NotificationDomainException>()
            .And.ErrorCode.Should().Be("CHANNEL_SELECTOR_SMS_PROVIDER_EMPTY");
    }

    [Fact]
    public void Constructor_EmptySmsProvider_ShouldThrowNotificationDomainException()
    {
        // Act
        var act = () => new ChannelSelector("");

        // Assert
        act.Should().Throw<NotificationDomainException>()
            .And.ErrorCode.Should().Be("CHANNEL_SELECTOR_SMS_PROVIDER_EMPTY");
    }

    [Fact]
    public void Constructor_WhitespaceSmsProvider_ShouldThrowNotificationDomainException()
    {
        // Act
        var act = () => new ChannelSelector("   ");

        // Assert
        act.Should().Throw<NotificationDomainException>()
            .And.ErrorCode.Should().Be("CHANNEL_SELECTOR_SMS_PROVIDER_EMPTY");
    }

    #endregion

    #region AllProvidersUnavailable

    [Fact]
    public void SelectFallbackProvider_WhenAllProvidersUnavailable_EmailReturnsNull()
    {
        // For Email, there is no fallback, so when SMTP fails, all providers are unavailable
        // Arrange
        var selector = new ChannelSelector();

        // Act
        var fallback = selector.SelectFallbackProvider(NotificationChannel.Email);

        // Assert
        fallback.Should().BeNull(); // All providers unavailable for Email
    }

    [Fact]
    public void SelectProvider_UnknownSmsProvider_ShouldReturnProviderAsIs()
    {
        // When an unknown provider is configured, it's returned as the primary
        // and the fallback will be null
        // Arrange
        var selector = new ChannelSelector("UnknownProvider");

        // Act
        var primary = selector.SelectProvider(NotificationChannel.Sms);
        var fallback = selector.SelectFallbackProvider(NotificationChannel.Sms);

        // Assert
        primary.Should().Be("UnknownProvider");
        fallback.Should().BeNull(); // No known fallback
    }

    #endregion
}