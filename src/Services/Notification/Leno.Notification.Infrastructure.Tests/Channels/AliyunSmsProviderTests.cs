using Leno.Notification.Domain.Services;
using Leno.Notification.Domain.ValueObjects;
using Leno.Notification.Infrastructure.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq.Protected;
using System.Net;
using System.Text;
using Options = Microsoft.Extensions.Options.Options;

namespace Leno.Notification.Infrastructure.Tests.Channels;

public class AliyunSmsProviderTests
{
    [Fact]
    public async Task SendAsync_Success_ShouldReturnBizIdAsChannelMessageId()
    {
        // Arrange
        var options = Microsoft.Extensions.Options.Options.Create(new SmsChannelOptions
        {
            Provider = "Aliyun",
            AccessKeyId = "AKID123",
            AccessKeySecret = "SK456",
            SignName = "Leno"
        });

        // 阿里云成功响应格式：{"Code":"OK","Message":"OK","RequestId":"xxx","BizId":"123456789012345678^0"}
        var responseBody = """{"Code":"OK","Message":"OK","RequestId":"req-abc","BizId":"123456789012345678^0"}""";
        var httpMessage = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
        };

        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        // 使用受保护的 SendAsync
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpMessage);

        var httpClient = new HttpClient(handlerMock.Object);
        var loggerMock = new Mock<ILogger<AliyunSmsProvider>>();

        var provider = new AliyunSmsProvider(options, httpClient, loggerMock.Object);

        var recipient = Recipient.Create(Guid.NewGuid(), "test@example.com", "13800138000");
        var request = new ChannelSendRequest(
            NotificationChannel.Sms, recipient, null, "Body", "idem-key", "SMS_12345678");

        // Act
        var result = await provider.SendAsync(request, CancellationToken.None);

        // Assert — 修复后：ChannelMessageId 应为解析出的 BizId，而非整个响应体
        Assert.True(result.Succeeded);
        Assert.Equal("123456789012345678^0", result.ChannelMessageId);
        Assert.NotEqual(responseBody, result.ChannelMessageId);
    }

    [Fact]
    public async Task SendAsync_SuccessButNoBizId_ShouldReturnNullChannelMessageId()
    {
        // Arrange — 响应中无 BizId 字段
        var options = Microsoft.Extensions.Options.Options.Create(new SmsChannelOptions
        {
            Provider = "Aliyun",
            AccessKeyId = "AKID123",
            AccessKeySecret = "SK456",
            SignName = "Leno"
        });

        var responseBody = """{"Code":"OK","Message":"OK","RequestId":"req-abc"}""";
        var httpMessage = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
        };

        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpMessage);

        var httpClient = new HttpClient(handlerMock.Object);
        var loggerMock = new Mock<ILogger<AliyunSmsProvider>>();

        var provider = new AliyunSmsProvider(options, httpClient, loggerMock.Object);
        var recipient = Recipient.Create(Guid.NewGuid(), "test@example.com", "13800138000");
        var request = new ChannelSendRequest(
            NotificationChannel.Sms, recipient, null, "Body", "idem-key", "SMS_12345678");

        // Act
        var result = await provider.SendAsync(request, CancellationToken.None);

        // Assert — 无 BizId 时 ChannelMessageId 为 null
        Assert.True(result.Succeeded);
        Assert.Null(result.ChannelMessageId);
    }
}
