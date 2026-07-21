using Leno.Notification.Domain.ValueObjects;

namespace Leno.Notification.Domain.Tests.ValueObjects;

/// <summary>
/// P0-9 修复验证：ChannelSendRequest 应携带 SmsTemplateCode 字段，
/// 由 NotificationService 从 NotificationTemplate.SmsTemplateCode 透传给短信渠道 Provider，
/// 替代 Provider 中硬编码的 "SMS_000000" / "000000"。
/// </summary>
public class ChannelSendRequestTests
{
    [Fact]
    public void ChannelSendRequest_WithSmsTemplateCode_ShouldCarryValue()
    {
        // Arrange
        var recipient = Recipient.Create(Guid.NewGuid(), "test@example.com", "13800138000");

        // Act
        var request = new ChannelSendRequest(
            NotificationChannel.Sms,
            recipient,
            "Subject",
            "Body",
            "idem-key-123",
            "SMS_12345678");

        // Assert
        request.SmsTemplateCode.Should().Be("SMS_12345678");
    }

    [Fact]
    public void ChannelSendRequest_WithoutSmsTemplateCode_ShouldDefaultToNull()
    {
        // Arrange
        var recipient = Recipient.Create(Guid.NewGuid(), "test@example.com", "13800138000");

        // Act — 不传 SmsTemplateCode，使用可选参数默认值
        var request = new ChannelSendRequest(
            NotificationChannel.Email,
            recipient,
            "Subject",
            "Body",
            "idem-key-456");

        // Assert
        request.SmsTemplateCode.Should().BeNull();
    }

    [Fact]
    public void ChannelSendRecord_Equality_WithSameSmsTemplateCode_ShouldBeEqual()
    {
        // Arrange
        var recipient = Recipient.Create(Guid.NewGuid(), "test@example.com", "13800138000");

        // Act
        var request1 = new ChannelSendRequest(
            NotificationChannel.Sms, recipient, "Subject", "Body", "key", "SMS_001");
        var request2 = new ChannelSendRequest(
            NotificationChannel.Sms, recipient, "Subject", "Body", "key", "SMS_001");

        // Assert — record 类型值相等语义
        request1.Should().Be(request2);
    }

    [Fact]
    public void ChannelSendRecord_Equality_WithDifferentSmsTemplateCode_ShouldNotBeEqual()
    {
        // Arrange
        var recipient = Recipient.Create(Guid.NewGuid(), "test@example.com", "13800138000");

        // Act
        var request1 = new ChannelSendRequest(
            NotificationChannel.Sms, recipient, "Subject", "Body", "key", "SMS_001");
        var request2 = new ChannelSendRequest(
            NotificationChannel.Sms, recipient, "Subject", "Body", "key", "SMS_002");

        // Assert
        request1.Should().NotBe(request2);
    }
}
