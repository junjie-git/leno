using Leno.Notification.Domain.Services;
using Leno.Notification.Infrastructure.Options;
using Leno.Notification.Infrastructure.Services;
using Microsoft.Extensions.Options;
using Moq;

namespace Leno.Notification.Application.Tests;

public class RetryPolicyTests
{
    private readonly RetryPolicy _policy = CreateSutWithDefaults();

    private static RetryPolicy CreateSutWithDefaults()
    {
        var optionsMock = new Mock<IOptionsMonitor<RetryPolicyOptions>>();
        optionsMock.Setup(o => o.CurrentValue).Returns(new RetryPolicyOptions());
        return new RetryPolicy(optionsMock.Object);
    }

    #region ShouldRetry

    [Theory]
    [InlineData("SMTP_RETRYABLE")]
    [InlineData("SMTP_CONNECT_TIMEOUT")]
    [InlineData("SMS_TIMEOUT")]
    [InlineData("EMAIL_EXCEPTION")]
    [InlineData("SMS_EXCEPTION")]
    [InlineData("DISPATCH_EXCEPTION")]
    [InlineData("RETRY_EXCEPTION")]
    [InlineData("SEND_EXCEPTION")]
    [InlineData("ACCEPTED_TIMEOUT")]
    public void ShouldRetry_RetryableErrorCodes_ShouldReturnTrue(string errorCode)
    {
        var result = _policy.ShouldRetry(errorCode);
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("SMTP_NON_RETRYABLE")]
    [InlineData("EMAIL_EMPTY")]
    [InlineData("EMAIL_CONFIG_MISSING")]
    [InlineData("SMS_PHONE_EMPTY")]
    [InlineData("SMS_CONFIG_MISSING")]
    [InlineData("SMS_HTTP_ERROR")]
    [InlineData("TEMPLATE_NOT_FOUND")]
    [InlineData("TEMPLATE_RENDER_FAILED")]
    [InlineData("CHANNEL_NOT_FOUND")]
    public void ShouldRetry_NonRetryableErrorCodes_ShouldReturnFalse(string errorCode)
    {
        var result = _policy.ShouldRetry(errorCode);
        result.Should().BeFalse();
    }

    [Fact]
    public void ShouldRetry_NullErrorCode_ShouldReturnFalse()
    {
        // P2-42：未知/空错误码默认不重试，直接进入死信
        var result = _policy.ShouldRetry(null);
        result.Should().BeFalse();
    }

    [Fact]
    public void ShouldRetry_EmptyErrorCode_ShouldReturnFalse()
    {
        // P2-42：未知/空错误码默认不重试，直接进入死信
        var result = _policy.ShouldRetry("");
        result.Should().BeFalse();
    }

    [Fact]
    public void ShouldRetry_WhitespaceErrorCode_ShouldReturnFalse()
    {
        // P2-42：未知/空错误码默认不重试，直接进入死信
        var result = _policy.ShouldRetry("   ");
        result.Should().BeFalse();
    }

    [Fact]
    public void ShouldRetry_5xxErrorCode_ShouldReturnTrue()
    {
        var result = _policy.ShouldRetry("500");
        result.Should().BeTrue();
    }

    [Fact]
    public void ShouldRetry_UnknownErrorCode_ShouldReturnFalse()
    {
        // P2-42：未在白名单内的未知错误码默认不重试，直接进入死信
        var result = _policy.ShouldRetry("UNKNOWN_ERROR_CODE");
        result.Should().BeFalse();
    }

    #endregion

    #region NextDelay

    [Fact]
    public void NextDelay_RetryCount1_ShouldReturn30Seconds()
    {
        var delay = _policy.NextDelay(1);
        delay.Should().Be(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void NextDelay_RetryCount2_ShouldReturn2Minutes()
    {
        var delay = _policy.NextDelay(2);
        delay.Should().Be(TimeSpan.FromMinutes(2));
    }

    [Fact]
    public void NextDelay_RetryCount3_ShouldReturn10Minutes()
    {
        var delay = _policy.NextDelay(3);
        delay.Should().Be(TimeSpan.FromMinutes(10));
    }

    [Fact]
    public void NextDelay_RetryCount4_ShouldReturn10Minutes()
    {
        var delay = _policy.NextDelay(4);
        delay.Should().Be(TimeSpan.FromMinutes(10));
    }

    [Fact]
    public void NextDelay_RetryCount0_ShouldReturn30Seconds()
    {
        var delay = _policy.NextDelay(0);
        delay.Should().Be(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void NextDelay_RetryCountNegative_ShouldReturn30Seconds()
    {
        var delay = _policy.NextDelay(-1);
        delay.Should().Be(TimeSpan.FromSeconds(30));
    }

    #endregion
}