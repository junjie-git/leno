using System.Security.Cryptography;
using System.Text;
using Leno.Payment.Domain.Aggregates;
using Leno.Payment.Domain.Repositories;
using Leno.Payment.Domain.Services;
using Leno.Payment.Domain.ValueObjects;
using Leno.Payment.Infrastructure.Channels;
using Leno.Payment.Infrastructure.Channels.WeChatPay;
using Leno.Payment.Infrastructure.Notify;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace Leno.Payment.Infrastructure.Tests.Notify;

/// <summary>
/// P0-1 测试：验证 <see cref="WeChatPayNotifyHandler"/> 不再在验签前调用 <c>ParseXml</c>。
/// 微信 V3 回调为 JSON 格式，<c>ParseXml</c>（XML 解析）在验签前执行会导致 JSON 报文抛 <c>XmlException</c>，
/// 被外层 catch 吞掉返回 <c>FAIL</c>，所有 V3 回调无法处理。
/// 修复后：<c>ParseXml</c> 不再被调用，验签失败直接返回 <c>FAIL</c>，验签成功后使用 <see cref="ChannelNotifyResult"/> 字段。
///
/// 阶段三 3.8 插件化：<see cref="WeChatPayNotifyHandler"/> 改为直接依赖具体 <see cref="WeChatPayAdapter"/>，
/// 不再依赖 <see cref="IPaymentChannelAdapter"/> 单注入（避免多注册歧义）。
/// 本测试构造真实 <see cref="WeChatPayAdapter"/> 实例，通过 RSA 密钥对与 AES-GCM 加密构造 V3 回调报文。
/// </summary>
public class WeChatPayNotifyHandlerParseXmlTests
{
    private const string OutTradeNo = "PAY20260722000001";
    private const string ChannelTradeNo = "4200000000202607220000000001";
    private const string ApiV3Key = "test_v3_key_32chars_long_1234567";
    private const string AppId = "wx1234567890";
    private const string MchId = "1234567890";

    private static readonly Guid OrderId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    /// <summary>
    /// 构造真实 <see cref="WeChatPayAdapter"/>，使用 RSA 密钥对与 APIv3 密钥。
    /// 平台公钥由 <paramref name="platformPublicKey"/> 传入，供验签使用。
    /// </summary>
    private static WeChatPayAdapter CreateAdapter(
        string platformPublicKey,
        string apiV3Key = ApiV3Key)
    {
        var configProviderMock = new Mock<IChannelConfigProvider>();
        configProviderMock
            .Setup(p => p.GetConfigAsync(PaymentChannel.WeChatPay, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelConfig
            {
                AppId = AppId,
                MchId = MchId,
                ApiKey = apiV3Key,
                PlatformPublicKey = platformPublicKey,
                NotifyUrl = "https://example.com/notify/wechatpay",
                RefundNotifyUrl = "https://example.com/notify/wechatpay/refund"
            });

        var httpClient = new HttpClient();
        var clientLogger = NullLogger<WeChatPayClient>.Instance;
        var options = Options.Create(new WeChatPayOptions
        {
            AppId = AppId,
            MchId = MchId,
            ApiV3Key = apiV3Key,
            PrivateKey = "-----BEGIN PRIVATE KEY-----\ntest\n-----END PRIVATE KEY-----",
            SerialNo = "SERIAL001"
        });
        var client = new WeChatPayClient(httpClient, options, clientLogger);
        var adapterLogger = NullLogger<WeChatPayAdapter>.Instance;

        return new WeChatPayAdapter(client, configProviderMock.Object, adapterLogger);
    }

    /// <summary>
    /// 构造 handler，注入真实 <see cref="WeChatPayAdapter"/>。
    /// </summary>
    private static WeChatPayNotifyHandler CreateHandler(
        WeChatPayAdapter adapter,
        Mock<IPaymentOrderRepository>? orderRepoMock = null,
        Mock<IRefundOrderRepository>? refundRepoMock = null,
        Mock<IUnitOfWork>? uowMock = null,
        IConnectionMultiplexer? redis = null)
    {
        orderRepoMock ??= new Mock<IPaymentOrderRepository>();
        refundRepoMock ??= new Mock<IRefundOrderRepository>();
        uowMock ??= new Mock<IUnitOfWork>();

        return new WeChatPayNotifyHandler(
            adapter,
            orderRepoMock.Object,
            refundRepoMock.Object,
            uowMock.Object,
            redis,
            NullLogger<WeChatPayNotifyHandler>.Instance);
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

        var combined = new byte[ciphertext.Length + tag.Length];
        Buffer.BlockCopy(ciphertext, 0, combined, 0, ciphertext.Length);
        Buffer.BlockCopy(tag, 0, combined, ciphertext.Length, tag.Length);
        return Convert.ToBase64String(combined);
    }

    /// <summary>
    /// 构造 V3 回调 JSON 报文（含加密 resource）与对应的签名头。
    /// </summary>
    private static (string rawBody, Dictionary<string, string> headers) BuildV3Notify(
        string decryptedPayload, string platformPrivateKey, string nonce, string timestamp)
    {
        var associatedData = "";
        var ciphertext = EncryptResource(decryptedPayload, ApiV3Key, nonce, associatedData);

        var rawBody = "{\"id\":\"evt-001\",\"event_type\":\"TRANSACTION.SUCCESS\","
            + "\"resource\":{\"ciphertext\":\"" + ciphertext + "\","
            + "\"nonce\":\"" + nonce + "\","
            + "\"associated_data\":\"" + associatedData + "\"}}";

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

        return (rawBody, headers);
    }

    [Fact]
    public async Task HandleAsync_VerifyFailed_ShouldReturnFail_WithoutThrowingXmlException()
    {
        // Arrange：V3 JSON 报文（非 XML），使用无效签名 → 验签失败
        // 修复前：ParseXml(rawBody) 会因 JSON 报文抛 XmlException，被外层 catch 吞掉返回 FAIL
        // 修复后：不再调用 ParseXml，验签失败直接返回 FAIL
        using var rsa = RSA.Create(2048);
        var platformPublicKey = rsa.ExportRSAPublicKeyPem();

        var rawBody = "{\"id\":\"evt-001\",\"event_type\":\"TRANSACTION.SUCCESS\","
            + "\"resource\":{\"ciphertext\":\"abc\",\"nonce\":\"def\",\"associated_data\":\"\"}}";
        var headers = new Dictionary<string, string>
        {
            ["Wechatpay-Timestamp"] = "1234567890",
            ["Wechatpay-Nonce"] = "nonce123",
            ["Wechatpay-Signature"] = "invalid_sig",
            ["Wechatpay-Serial"] = "SERIAL001"
        };

        var adapter = CreateAdapter(platformPublicKey);
        var sut = CreateHandler(adapter);

        // Act：验签失败应直接返回 FAIL，不应因 ParseXml 抛 XmlException
        var result = await sut.HandleAsync(rawBody, headers);

        // Assert
        Assert.Equal("FAIL", result);
    }

    [Fact]
    public async Task HandleAsync_VerifySucceeded_V3Json_ShouldProcessSuccessfully()
    {
        // Arrange：V3 JSON 报文，验签通过，ChannelNotifyResult 含 OutTradeNo 等字段
        // 修复前：ParseXml(rawBody) 会因 JSON 报文抛 XmlException，即使验签通过也无法处理
        // 修复后：不再调用 ParseXml，验签通过后使用 ChannelNotifyResult 字段处理
        using var rsa = RSA.Create(2048);
        var platformPrivateKey = rsa.ExportRSAPrivateKeyPem();
        var platformPublicKey = rsa.ExportRSAPublicKeyPem();

        var decryptedData = "{\"out_trade_no\":\"" + OutTradeNo + "\","
            + "\"transaction_id\":\"" + ChannelTradeNo + "\","
            + "\"trade_state\":\"SUCCESS\","
            + "\"success_time\":\"2026-07-22T10:00:00+08:00\","
            + "\"amount\":{\"total\":10000,\"payer\":{\"total\":10000}}}";

        var nonce = "nonce1234567";
        var timestamp = "1753166400";
        var (rawBody, headers) = BuildV3Notify(decryptedData, platformPrivateKey, nonce, timestamp);

        var order = PaymentOrder.Create(Guid.NewGuid(), OrderId, UserId, 100m, "CNY", PaymentChannel.WeChatPay);
        var orderRepoMock = new Mock<IPaymentOrderRepository>();
        orderRepoMock
            .Setup(r => r.GetByOutTradeNoAsync(OutTradeNo, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var adapter = CreateAdapter(platformPublicKey);

        // Redis null = 开发环境放行
        var sut = CreateHandler(adapter, orderRepoMock, redis: null);

        // Act：验签通过后应使用 ChannelNotifyResult 字段处理，不依赖 ParseXml
        var result = await sut.HandleAsync(rawBody, headers);

        // Assert
        Assert.Equal("SUCCESS", result);
        Assert.Equal(PaymentStatus.Paid, order.Status);
        Assert.Equal(ChannelTradeNo, order.ChannelTradeNo);
        orderRepoMock.Verify(r => r.GetByOutTradeNoAsync(OutTradeNo, It.IsAny<CancellationToken>()), Times.Once);
    }
}
