using System.Security.Cryptography;
using Leno.Payment.Domain.Services;
using Leno.Payment.Domain.ValueObjects;
using Leno.Payment.Infrastructure.Channels;
using Leno.Payment.Infrastructure.Channels.Alipay;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Leno.Payment.Infrastructure.Tests.Channels;

/// <summary>
/// P0-3 测试：验证 AlipayAdapter.VerifyNotifyAsync 使用 PublicKey 而非 ApiKey（私钥）验签。
/// 支付宝 RSA2 验签应使用支付宝公钥验证签名，而非商户私钥。
/// </summary>
public class AlipayAdapterPublicKeyTests
{
    private static (string privateKey, string publicKey) GenerateKeyPair()
    {
        using var rsa = RSA.Create(2048);
        return (rsa.ExportRSAPrivateKeyPem(), rsa.ExportRSAPublicKeyPem());
    }

    private static AlipayAdapter CreateAdapter(ChannelConfig config)
    {
        var configProviderMock = new Mock<IChannelConfigProvider>();
        configProviderMock
            .Setup(p => p.GetConfigAsync(PaymentChannel.Alipay, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        var httpClient = new HttpClient();
        var clientLogger = NullLogger<AlipayClient>.Instance;
        var client = new AlipayClient(httpClient, clientLogger);
        var adapterLogger = NullLogger<AlipayAdapter>.Instance;

        return new AlipayAdapter(client, configProviderMock.Object, adapterLogger);
    }

    private static Dictionary<string, string> BuildNotifyFields(string privateKey, string totalAmount = "100.00")
    {
        var fields = new Dictionary<string, string>
        {
            ["app_id"] = "2021000000000001",
            ["charset"] = "UTF-8",
            ["out_trade_no"] = "PAY20260722000001",
            ["trade_no"] = "2026071222001000000000000001",
            ["trade_status"] = "TRADE_SUCCESS",
            ["total_amount"] = totalAmount,
            ["gmt_payment"] = "2026-07-22 10:00:00",
            ["notify_time"] = "2026-07-22 10:00:00",
            ["notify_type"] = "trade_status_sync",
            ["notify_id"] = "notify-001",
            ["sign_type"] = "RSA2"
        };
        fields["sign"] = AlipaySignatureHelper.GenerateSign(fields, privateKey);
        return fields;
    }

    [Fact]
    public async Task VerifyNotifyAsync_ShouldUsePublicKey_NotPrivateKey()
    {
        // Arrange：生成 RSA 密钥对，私钥签名模拟支付宝签名，公钥验签
        var (privateKey, publicKey) = GenerateKeyPair();

        var fields = BuildNotifyFields(privateKey);
        var rawBody = string.Join("&", fields.Select(kv =>
            $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));

        var config = new ChannelConfig
        {
            AppId = "2021000000000001",
            MchId = "2088000000000001",
            ApiKey = privateKey,
            PublicKey = publicKey,
            NotifyUrl = "https://example.com/notify/alipay",
            RefundNotifyUrl = "https://example.com/notify/alipay/refund"
        };

        var sut = CreateAdapter(config);

        // Act
        var result = await sut.VerifyNotifyAsync(rawBody, fields);

        // Assert：使用正确的公钥验签应通过
        Assert.True(result.Verified);
        Assert.True(result.IsPaid);
    }

    [Fact]
    public async Task VerifyNotifyAsync_WithPrivateKeyAsPublicKey_ShouldFailVerification()
    {
        // Arrange：用私钥作为公钥验签，应失败
        var (privateKey, _) = GenerateKeyPair();

        var fields = BuildNotifyFields(privateKey);
        var rawBody = string.Join("&", fields.Select(kv =>
            $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));

        var config = new ChannelConfig
        {
            AppId = "2021000000000001",
            MchId = "2088000000000001",
            ApiKey = privateKey,
            // 不设置 PublicKey，模拟旧配置（回退到 ApiKey 即私钥）
            NotifyUrl = "https://example.com/notify/alipay",
            RefundNotifyUrl = "https://example.com/notify/alipay/refund"
        };

        var sut = CreateAdapter(config);

        // Act
        var result = await sut.VerifyNotifyAsync(rawBody, fields);

        // Assert：用私钥验签应失败
        Assert.False(result.Verified);
    }
}
