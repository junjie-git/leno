using System.Security.Cryptography;
using System.Text;
using Leno.Payment.Domain.Services;
using Leno.Payment.Domain.ValueObjects;
using Leno.Payment.Infrastructure.Channels;
using Leno.Payment.Infrastructure.Channels.WeChatPay;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Leno.Payment.Infrastructure.Tests.Channels;

/// <summary>
/// P0-5 测试：验证 WeChatPayAdapter.VerifyNotifyAsync 解析 OutTradeNo 并填入 ChannelNotifyResult。
/// 修复前 ChannelNotifyResult 无 OutTradeNo 字段，NotifyHandler 只能依赖 ParseXml 从原始报文提取，
/// 但 V3 回调为 JSON 格式，ParseXml 抛 XmlException 导致所有 V3 回调无法处理。
/// </summary>
public class WeChatPayAdapterOutTradeNoTests
{
    private const string OutTradeNo = "PAY20260722000001";
    private const string ChannelTradeNo = "4200000000202607220000000001";
    private const string ApiV3Key = "test_v3_key_32chars_long_1234567890";

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
            ApiV3Key = ApiV3Key,
            PrivateKey = "-----BEGIN PRIVATE KEY-----\ntest\n-----END PRIVATE KEY-----",
            SerialNo = "SERIAL001"
        });
        var client = new WeChatPayClient(httpClient, options, clientLogger);
        var adapterLogger = NullLogger<WeChatPayAdapter>.Instance;

        return new WeChatPayAdapter(client, configProviderMock.Object, adapterLogger);
    }

    /// <summary>
    /// 使用 AES-GCM 加密构造微信 V3 回调 resource.ciphertext。
    /// </summary>
    private static string EncryptResource(string plaintext, string key, string nonce, string associatedData)
    {
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var nonceBytes = Encoding.UTF8.GetBytes(nonce);
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var associatedBytes = string.IsNullOrEmpty(associatedData) ? Array.Empty<byte>() : Encoding.UTF8.GetBytes(associatedData);

        using var aes = new AesGcm(keyBytes, 16);
        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[16];
        aes.Encrypt(nonceBytes, plaintextBytes, ciphertext, tag, associatedBytes);

        // 微信格式：ciphertext + tag，Base64 编码
        var combined = new byte[ciphertext.Length + tag.Length];
        Buffer.BlockCopy(ciphertext, 0, combined, 0, ciphertext.Length);
        Buffer.BlockCopy(tag, 0, combined, ciphertext.Length, tag.Length);
        return Convert.ToBase64String(combined);
    }

    [Fact]
    public async Task VerifyNotifyAsync_ShouldPopulateOutTradeNo_FromDecryptedData()
    {
        // Arrange：构造 V3 回调 JSON，包含加密的 resource
        var decryptedData = "{\"out_trade_no\":\"" + OutTradeNo + "\","
            + "\"transaction_id\":\"" + ChannelTradeNo + "\","
            + "\"trade_state\":\"SUCCESS\","
            + "\"success_time\":\"2026-07-22T10:00:00+08:00\","
            + "\"amount\":{\"total\":10000,\"payer\":{\"total\":10000}}}";

        var nonce = "nonce12345";
        var associatedData = "";
        var ciphertext = EncryptResource(decryptedData, ApiV3Key, nonce, associatedData);

        var rawBody = "{\"id\":\"evt-001\",\"event_type\":\"TRANSACTION.SUCCESS\","
            + "\"resource\":{\"ciphertext\":\"" + ciphertext + "\","
            + "\"nonce\":\"" + nonce + "\","
            + "\"associated_data\":\"" + associatedData + "\"}}";

        // 生成平台密钥对并签名
        using var rsa = RSA.Create(2048);
        var platformPrivateKey = rsa.ExportRSAPrivateKeyPem();
        var platformPublicKey = rsa.ExportRSAPublicKeyPem();

        var timestamp = "1753166400";
        var signMessage = $"{timestamp}\n{nonce}\n{rawBody}\n";
        using var signRsa = RSA.Create();
        signRsa.ImportFromPem(platformPrivateKey);
        var signatureBytes = signRsa.SignData(
            Encoding.UTF8.GetBytes(signMessage),
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
            ApiKey = ApiV3Key,
            PlatformPublicKey = platformPublicKey,
            NotifyUrl = "https://example.com/notify/wechatpay",
            RefundNotifyUrl = "https://example.com/notify/wechatpay/refund"
        };

        var sut = CreateAdapter(config);

        // Act
        var result = await sut.VerifyNotifyAsync(rawBody, headers);

        // Assert
        Assert.True(result.Verified);
        Assert.Equal(OutTradeNo, result.OutTradeNo);
        Assert.Equal(ChannelTradeNo, result.ChannelTradeNo);
        Assert.True(result.IsPaid);
        Assert.Equal(100m, result.Amount);
    }
}
