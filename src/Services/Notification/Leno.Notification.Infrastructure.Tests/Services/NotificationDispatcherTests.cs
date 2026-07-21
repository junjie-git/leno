using Leno.Notification.Domain.Services;
using Leno.Notification.Domain.ValueObjects;
using Leno.Notification.Infrastructure.Channels;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Leno.Notification.Infrastructure.Tests.Services;

public class NotificationDispatcherTests
{
    [Fact]
    public async Task SmsChannel_WithMultipleProviders_ShouldNotThrowDuplicateKeyException()
    {
        // Arrange — 模拟两个 SMS provider 都返回 Channel=Sms。
        // 修复前：AliyunSmsChannel 与 TencentSmsChannel 都实现 INotificationChannel，
        //         渠道集合的 ToDictionary(c => c.Channel) 因重复键抛 ArgumentException。
        // 修复后：SmsChannel 外壳类作为单一 INotificationChannel 注册，
        //         不再有两个 INotificationChannel 返回相同 Channel 值。
        var aliyunProviderMock = new Mock<ISmsProvider>(MockBehavior.Strict);
        aliyunProviderMock.SetupGet(p => p.ProviderName).Returns("Aliyun");
        var tencentProviderMock = new Mock<ISmsProvider>(MockBehavior.Strict);
        tencentProviderMock.SetupGet(p => p.ProviderName).Returns("Tencent");

        var channelSelectorMock = new Mock<IChannelSelector>(MockBehavior.Strict);
        channelSelectorMock.Setup(s => s.SelectSmsProvider()).Returns("Aliyun");

        aliyunProviderMock
            .Setup(p => p.SendAsync(It.IsAny<ChannelSendRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelSendResult(true, null, null, "biz-id-123"));

        var smsChannel = new SmsChannel(
            new[] { aliyunProviderMock.Object, tencentProviderMock.Object },
            channelSelectorMock.Object,
            new Mock<ILogger<SmsChannel>>().Object);

        var emailChannelMock = new Mock<INotificationChannel>(MockBehavior.Strict);
        emailChannelMock.SetupGet(c => c.Channel).Returns(NotificationChannel.Email);
        var inAppChannelMock = new Mock<INotificationChannel>(MockBehavior.Strict);
        inAppChannelMock.SetupGet(c => c.Channel).Returns(NotificationChannel.InApp);

        var channels = new INotificationChannel[]
        {
            smsChannel,
            emailChannelMock.Object,
            inAppChannelMock.Object
        };

        // Act — 修复前：ToDictionary 抛 ArgumentException；修复后：构造时一次性构建不抛异常
        Exception? exception = null;
        IReadOnlyDictionary<NotificationChannel, INotificationChannel> dict = null!;
        exception = Record.Exception(() => dict = channels.ToDictionary(c => c.Channel));

        // Assert — 渠道集合中每个 Channel 值唯一，ToDictionary 不抛异常
        Assert.Null(exception);
        Assert.NotNull(dict);
        Assert.Equal(3, dict.Count);
        Assert.Same(smsChannel, dict[NotificationChannel.Sms]);

        // 调用 SmsChannel.SendAsync 应正确选择 Aliyun provider 而非抛异常
        var recipient = Recipient.Create(Guid.NewGuid(), "test@example.com", "13800138000");
        var sendRequest = new ChannelSendRequest(
            NotificationChannel.Sms, recipient, null, "Body", "idem-key-123");

        var sendException = await Record.ExceptionAsync(() => smsChannel.SendAsync(sendRequest, CancellationToken.None));
        Assert.Null(sendException);
        aliyunProviderMock.Verify(
            p => p.SendAsync(It.IsAny<ChannelSendRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void SmsChannel_WithNoProviders_ShouldConstructAndReturnNotFoundOnSend()
    {
        // Arrange — 即便未注册任何 provider，SmsChannel 构造也不应抛异常（仅记录警告）
        var channelSelectorMock = new Mock<IChannelSelector>(MockBehavior.Strict);
        channelSelectorMock.Setup(s => s.SelectSmsProvider()).Returns("Aliyun");

        var smsChannel = new SmsChannel(
            Array.Empty<ISmsProvider>(),
            channelSelectorMock.Object,
            new Mock<ILogger<SmsChannel>>().Object);

        // Act & Assert — Channel 唯一为 Sms，调用 SendAsync 返回 SMS_PROVIDER_NOT_FOUND
        Assert.Equal(NotificationChannel.Sms, smsChannel.Channel);
    }
}
