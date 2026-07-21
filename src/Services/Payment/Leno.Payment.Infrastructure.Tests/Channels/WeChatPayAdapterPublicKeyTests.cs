using System.Security.Cryptography;
using Leno.Payment.Domain.Services;
using Leno.Payment.Domain.ValueObjects;
using Leno.Payment.Infrastructure.Channels;
using Leno.Payment.Infrastructure.Channels.WeChatPay;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Leno.Payment.Infrastructure.Tests.Channels;

/// <summary>
/// P0-2 测试：验证 WeChatPayAdapter.VerifyNotifyAsync 使用 PlatformPublicKey 而非 ApiKey 验签。
/// 微信 V3 回调验签应使用微信支付平台公钥（RSA 公钥 PEM），而非 APIv3 对称密钥。
/// </summary>
public class WeChatPayAdapterPublicKeyTests
{
    private static (string privateKey, string publicKey) GenerateKeyPair()
    {
        using var rsa = RSA.Create(2048);
        return (rsa.ExportRSAPrivateKeyPem(), rsa.ExportRSAPublicKeyPem());
    }

    private static string BuildCallbackBody()
    {
        return "{\"id\":\"evt-001\",\"create_time\":\"2026-07-22T10:00:00+08:00\","
            + "\"event_type\":\"TRANSACTION.SUCCESS\","
            + "\"resource\":{\"ciphertext\":\"abc\",\"nonce\":\"def\",\"associated_data\":\"\"}}";
    }

    private static WeChatPayAdapter CreateAdapter(ChannelConfig config)
    {
        var configProviderMock = new Mock<IChannelConfigProvider>();
        configProviderMock
            .Setup(p => p.GetConfigAsync(PaymentChannel.WeChatPay, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        var httpClient = new HttpClient();
        var clientLogger = NullLogger<WeChatPayClient>.Instance;
        var options = Microsoft.Extensions.Options.Options.Create(new WeChatPayOptions
        {
            AppId = "wx1234567890",
            MchId = "1234567890",
            ApiV3Key = "test_v3_key_32chars_long_1234567890",
            PrivateKey = "-----BEGIN PRIVATE KEY-----\ntest\n-----END PRIVATE KEY-----",
            SerialNo = "SERIAL001"
        });
        var client = new WeChatPayClient(httpClient, options, clientLogger);
        var adapterLogger = NullLogger<WeChatPayAdapter>.Instance;

        return new WeChatPayAdapter(client, configProviderMock.Object, adapterLogger);
    }

    [Fact]
    public async Task VerifyNotifyAsync_ShouldUsePlatformPublicKey_NotApiKey()
    {
        // Arrange：生成 RSA 密钥对，用私钥签名模拟微信平台签名
        var (platformPrivateKey, platformPublicKey) = GenerateKeyPair();
        var apiV3Key = "test_v3_key_32chars_long_1234567890";

        var body = BuildCallbackBody();
        var timestamp = "1753166400";
        var nonce = "nonce123";

        // 用平台私钥生成正确签名
        var message = $"{timestamp}\n{nonce}\n{body}\n";
        using var rsa = RSA.Create();
        rsa.ImportFromPem(platformPrivateKey);
        var signatureBytes = rsa.SignData(
            System.Text.Encoding.UTF8.GetBytes(message),
            HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var signature = Convert.ToBase64String(signatureBytes);

        var headers = new Dictionary<string, string>
        {
            ["Wechatpay-Timestamp"] = timestamp,
            ["Wechatpay-Nonce"] = nonce,
            ["Wechatpay-Signature"] = signature,
            ["Wechatpay-Serial"] = "SERIAL001"
        };

        var config = new ChannelConfig
        {
            AppId = "wx1234567890",
            MchId = "1234567890",
            ApiKey = apiV3Key,
            PlatformPublicKey = platformPublicKey,
            NotifyUrl = "https://example.com/notify/wechatpay",
            RefundNotifyUrl = "https://example.com/notify/wechatpay/refund"
        };

        var sut = CreateAdapter(config);

        // Act
        var result = await sut.VerifyNotifyAsync(body, headers);

        // Assert：使用正确的平台公钥验签应通过
        Assert.True(result.Verified);
    }

    [Fact]
    public async Task VerifyNotifyAsync_WithApiKeyAsPublicKey_ShouldFailVerification()
    {
        // Arrange：ApiKey 不是合法 PEM 公钥，验签应失败
        var apiV3Key = "test_v3_key_32chars_long_1234567890";
        var body = BuildCallbackBody();
        var headers = new Dictionary<string, string>
        {
            ["Wechatpay-Timestamp"] = "1753166400",
            ["Wechatpay-Nonce"] = "nonce123",
            ["Wechatpay-Signature"] = "invalid_sig",
            ["Wechatpay-Serial"] = "SERIAL001"
        };

        var config = new ChannelConfig
        {
            AppId = "wx1234567890",
            MchId = "1234567890",
            ApiKey = apiV3Key,
            // 不设置 PlatformPublicKey，模拟旧配置
            NotifyUrl = "https://example.com/notify/wechatpay",
            RefundNotifyUrl = "https://example.com/notify/wechatpay/refund"
        };

        var sut = CreateAdapter(config);

        // Act
        var result = await sut.VerifyNotifyAsync(body, headers);

        // Assert：PlatformPublicKey 为空时验签应失败（不应回退到 ApiKey）
        Assert.False(result.Verified);
    }
}
