using Leno.Payment.Domain.Aggregates;
using Leno.Payment.Domain.Services;
using Leno.Payment.Domain.ValueObjects;
using Leno.Payment.Infrastructure.Channels;
using Leno.Payment.Infrastructure.Channels.WeChatPay;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Leno.Payment.Infrastructure.Tests.Channels;

/// <summary>
/// P2-19 测试：验证 WeChatPayAdapter.CreatePaymentAsync 根据 PaymentOrder.TradeType
/// 将枚举正确映射为微信支付 V3 API 的 trade_type 字符串，并透传到 WeChatPayClient.UnifiedOrderAsync。
/// 修复前 tradeType 硬编码为常量 "NATIVE"，导致 H5/JSAPI/APP 场景无法下单。
/// </summary>
public class WeChatPayAdapterTradeTypeTests
{
    private static readonly Guid OrderId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static PaymentOrder CreateOrder(TradeType tradeType)
    {
        return PaymentOrder.Create(
            Guid.NewGuid(),
            OrderId,
            UserId,
            100m,
            "CNY",
            PaymentChannel.WeChatPay,
            tradeType);
    }

    private static (WeChatPayAdapter adapter, Mock<WeChatPayClient> clientMock) CreateAdapterWithMockClient(
        WeChatPayUnifiedOrderResult result)
    {
        var clientMock = new Mock<WeChatPayClient>(
            new HttpClient(),
            Microsoft.Extensions.Options.Options.Create(new WeChatPayOptions
            {
                AppId = "wx1234567890",
                MchId = "1234567890",
                ApiV3Key = "test_v3_key_32chars_long_1234567890",
                PrivateKey = "-----BEGIN PRIVATE KEY-----\ntest\n-----END PRIVATE KEY-----",
                SerialNo = "SERIAL001"
            }),
            NullLogger<WeChatPayClient>.Instance);

        clientMock
            .Setup(c => c.UnifiedOrderAsync(
                It.IsAny<ChannelConfig>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        var configProviderMock = new Mock<IChannelConfigProvider>();
        configProviderMock
            .Setup(p => p.GetConfigAsync(PaymentChannel.WeChatPay, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelConfig
            {
                AppId = "wx1234567890",
                MchId = "1234567890",
                ApiKey = "test_v3_key_32chars_long_1234567890",
                NotifyUrl = "https://example.com/notify/wechatpay",
                RefundNotifyUrl = "https://example.com/notify/wechatpay/refund"
            });

        var adapter = new WeChatPayAdapter(
            clientMock.Object,
            configProviderMock.Object,
            NullLogger<WeChatPayAdapter>.Instance);

        return (adapter, clientMock);
    }

    [Fact]
    public async Task CreatePaymentAsync_NativeTradeType_ShouldPassNATIVE()
    {
        // Arrange
        var order = CreateOrder(TradeType.Native);
        var (sut, clientMock) = CreateAdapterWithMockClient(new WeChatPayUnifiedOrderResult
        {
            PrepayId = "wx_prepay_001",
            CodeUrl = "weixin://wxpay/bizpayurl?pr=001"
        });

        // Act
        var result = await sut.CreatePaymentAsync(order);

        // Assert
        clientMock.Verify(c => c.UnifiedOrderAsync(
            It.IsAny<ChannelConfig>(),
            order.OutTradeNo,
            It.IsAny<int>(),
            It.IsAny<string>(),
            "NATIVE",
            It.IsAny<CancellationToken>()), Times.Once);
        result.CodeUrl.Should().NotBeNull();
        result.H5Url.Should().BeNull();
    }

    [Fact]
    public async Task CreatePaymentAsync_H5TradeType_ShouldPassH5AndReturnH5Url()
    {
        // Arrange
        var order = CreateOrder(TradeType.H5);
        var (sut, clientMock) = CreateAdapterWithMockClient(new WeChatPayUnifiedOrderResult
        {
            H5Url = "https://wx.tenpay.com/cgi-bin/mmpayweb-bin/checkmweb?prepay_id=wx_h5_001"
        });

        // Act
        var result = await sut.CreatePaymentAsync(order);

        // Assert
        clientMock.Verify(c => c.UnifiedOrderAsync(
            It.IsAny<ChannelConfig>(),
            order.OutTradeNo,
            It.IsAny<int>(),
            It.IsAny<string>(),
            "H5",
            It.IsAny<CancellationToken>()), Times.Once);
        result.H5Url.Should().NotBeNull();
        result.H5Url.Should().StartWith("https://wx.tenpay.com");
        result.CodeUrl.Should().BeNull();
    }

    [Fact]
    public async Task CreatePaymentAsync_JsApiTradeType_ShouldPassJSAPI()
    {
        // Arrange
        var order = CreateOrder(TradeType.JsApi);
        var (sut, clientMock) = CreateAdapterWithMockClient(new WeChatPayUnifiedOrderResult
        {
            PrepayId = "wx_prepay_jsapi_001"
        });

        // Act
        var result = await sut.CreatePaymentAsync(order);

        // Assert
        clientMock.Verify(c => c.UnifiedOrderAsync(
            It.IsAny<ChannelConfig>(),
            order.OutTradeNo,
            It.IsAny<int>(),
            It.IsAny<string>(),
            "JSAPI",
            It.IsAny<CancellationToken>()), Times.Once);
        result.PrepayId.Should().Be("wx_prepay_jsapi_001");
    }

    [Fact]
    public async Task CreatePaymentAsync_AppTradeType_ShouldPassAPP()
    {
        // Arrange
        var order = CreateOrder(TradeType.App);
        var (sut, clientMock) = CreateAdapterWithMockClient(new WeChatPayUnifiedOrderResult
        {
            PrepayId = "wx_prepay_app_001"
        });

        // Act
        var result = await sut.CreatePaymentAsync(order);

        // Assert
        clientMock.Verify(c => c.UnifiedOrderAsync(
            It.IsAny<ChannelConfig>(),
            order.OutTradeNo,
            It.IsAny<int>(),
            It.IsAny<string>(),
            "APP",
            It.IsAny<CancellationToken>()), Times.Once);
        result.PrepayId.Should().Be("wx_prepay_app_001");
    }

    [Fact]
    public async Task CreatePaymentAsync_DefaultTradeType_ShouldUseNATIVE()
    {
        // Arrange：不显式传入 tradeType，PaymentOrder.Create 默认 Native
        var order = PaymentOrder.Create(
            Guid.NewGuid(), OrderId, UserId, 100m, "CNY", PaymentChannel.WeChatPay);
        var (sut, clientMock) = CreateAdapterWithMockClient(new WeChatPayUnifiedOrderResult
        {
            CodeUrl = "weixin://wxpay/bizpayurl?pr=default"
        });

        // Act
        await sut.CreatePaymentAsync(order);

        // Assert：默认行为应与显式 Native 一致，保持向后兼容
        clientMock.Verify(c => c.UnifiedOrderAsync(
            It.IsAny<ChannelConfig>(),
            It.IsAny<string>(),
            It.IsAny<int>(),
            It.IsAny<string>(),
            "NATIVE",
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
