using System.Security.Cryptography;
using Leno.Payment.Domain.Aggregates;
using Leno.Payment.Domain.Repositories;
using Leno.Payment.Domain.Services;
using Leno.Payment.Domain.ValueObjects;
using Leno.Payment.Infrastructure.Channels;
using Leno.Payment.Infrastructure.Channels.Alipay;
using Leno.Payment.Infrastructure.Channels.WeChatPay;
using Leno.Payment.Infrastructure.Notify;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;

namespace Leno.Payment.Infrastructure.Tests;

/// <summary>
/// 支付回调 Redis 故障降级测试（T19）。
/// 验证 <see cref="WeChatPayNotifyHandler"/> 与 <see cref="AlipayNotifyHandler"/> 的
/// <c>MarkCallbackProcessedAsync</c> 在 Redis 故障时不再 fail-open 放行，
/// 而是向上抛出由外层 <c>HandleAsync</c> catch 返回 FAIL/fail 让渠道重试，
/// 由 <see cref="PaymentOrder"/> 聚合状态机兜底幂等。
/// </summary>
public class NotifyHandlerRedisFailoverTests
{
    private static readonly Guid OrderId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private const string OutTradeNo = "PAY20260701000001";

    private static (string privateKey, string publicKey) GenerateKeyPair()
    {
        using var rsa = RSA.Create(2048);
        return (rsa.ExportRSAPrivateKeyPem(), rsa.ExportRSAPublicKeyPem());
    }

    private static PaymentOrder CreateOrder(decimal amount)
    {
        return PaymentOrder.Create(Guid.NewGuid(), OrderId, UserId, amount, "CNY", PaymentChannel.Alipay);
    }

    private static (string rawBody, Dictionary<string, string> formFields) BuildAlipayPaidNotify(
        string privateKey, string totalAmount, string outTradeNo = OutTradeNo)
    {
        var fields = new Dictionary<string, string>
        {
            ["app_id"] = "2021000000000001",
            ["charset"] = "UTF-8",
            ["out_trade_no"] = outTradeNo,
            ["trade_no"] = "2026071222001000000000000001",
            ["trade_status"] = "TRADE_SUCCESS",
            ["total_amount"] = totalAmount,
            ["gmt_payment"] = "2026-07-12 10:00:00",
            ["notify_time"] = "2026-07-12 10:00:00",
            ["notify_type"] = "trade_status_sync",
            ["notify_id"] = "notify-001",
            ["sign_type"] = "RSA2"
        };
        fields["sign"] = AlipaySignatureHelper.GenerateSign(fields, privateKey);

        var rawBody = string.Join("&", fields.Select(kv =>
            $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
        return (rawBody, fields);
    }

    private static AlipayNotifyHandler CreateAlipayHandler(
        Mock<IPaymentOrderRepository> orderRepoMock,
        Mock<IUnitOfWork> uowMock,
        IConnectionMultiplexer? redis,
        string publicKey)
    {
        var configProviderMock = new Mock<IChannelConfigProvider>();
        configProviderMock
            .Setup(p => p.GetConfigAsync(PaymentChannel.Alipay, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelConfig
            {
                AppId = "2021000000000001",
                MchId = "2088000000000001",
                ApiKey = publicKey,
                NotifyUrl = "https://example.com/notify/alipay",
                RefundNotifyUrl = "https://example.com/notify/alipay/refund"
            });

        var httpClient = new HttpClient();
        var clientLogger = new Mock<ILogger<AlipayClient>>().Object;
        var client = new AlipayClient(httpClient, clientLogger);
        var adapterLogger = new Mock<ILogger<AlipayAdapter>>().Object;
        var adapter = new AlipayAdapter(client, configProviderMock.Object, adapterLogger);
        var handlerLogger = new Mock<ILogger<AlipayNotifyHandler>>().Object;
        var refundRepoMock = new Mock<IRefundOrderRepository>();

        return new AlipayNotifyHandler(
            adapter, orderRepoMock.Object, refundRepoMock.Object, uowMock.Object, redis, handlerLogger);
    }

    private static Mock<IConnectionMultiplexer> CreateThrowingRedisMock()
    {
        var redisMock = new Mock<IConnectionMultiplexer>();
        // 模拟 Redis 连接故障：GetDatabase 即抛异常，避免 StringSetAsync 重载匹配歧义
        redisMock
            .Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Throws(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "redis down"));
        return redisMock;
    }

    #region Alipay: Redis 故障返回 fail 让渠道重试

    [Fact]
    public async Task Alipay_RedisFailure_ShouldReturnFailAndNotMarkPaid()
    {
        // Arrange：金额一致、验签通过，但 Redis 幂等检查抛异常
        var (privateKey, publicKey) = GenerateKeyPair();
        var order = CreateOrder(100m);
        var orderRepoMock = new Mock<IPaymentOrderRepository>();
        orderRepoMock
            .Setup(r => r.GetByOutTradeNoAsync(OutTradeNo, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        var uowMock = new Mock<IUnitOfWork>();
        var redisMock = CreateThrowingRedisMock();

        var sut = CreateAlipayHandler(orderRepoMock, uowMock, redisMock.Object, publicKey);
        var (rawBody, fields) = BuildAlipayPaidNotify(privateKey, "100.00");

        // Act：T19 — Redis 故障不再 fail-open 放行，应返回 fail 让渠道重试
        var result = await sut.HandleAsync(rawBody, fields);

        // Assert
        result.Should().Be("fail");
        order.Status.Should().Be(PaymentStatus.Pending);
        orderRepoMock.Verify(r => r.UpdateAsync(It.IsAny<PaymentOrder>(), It.IsAny<CancellationToken>()), Times.Never);
        uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Alipay_RedisNull_ShouldProceedAndMarkPaid()
    {
        // Arrange：Redis 未配置（开发环境），应放行继续处理（T19 保留 null 放行语义）
        var (privateKey, publicKey) = GenerateKeyPair();
        var order = CreateOrder(100m);
        var orderRepoMock = new Mock<IPaymentOrderRepository>();
        orderRepoMock
            .Setup(r => r.GetByOutTradeNoAsync(OutTradeNo, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        var uowMock = new Mock<IUnitOfWork>();

        var sut = CreateAlipayHandler(orderRepoMock, uowMock, redis: null, publicKey);
        var (rawBody, fields) = BuildAlipayPaidNotify(privateKey, "100.00");

        // Act
        var result = await sut.HandleAsync(rawBody, fields);

        // Assert：Redis null 时放行，订单正常标记成功
        result.Should().Be("success");
        order.Status.Should().Be(PaymentStatus.Paid);
        uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region WeChatPay: Redis null 保留放行语义

    [Fact]
    public async Task WeChatPay_RedisNull_ShouldNotCrashOnIdempotencyCheck()
    {
        // Arrange：Redis 未配置（开发环境），MarkCallbackProcessedAsync 返回 true（放行）。
        // 验签会失败（测试密钥），但关键是不应因 Redis null 抛异常。
        var configProviderMock = new Mock<IChannelConfigProvider>();
        configProviderMock
            .Setup(p => p.GetConfigAsync(PaymentChannel.WeChatPay, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelConfig
            {
                AppId = "wx1234567890",
                MchId = "1234567890",
                ApiKey = "test_key",
                NotifyUrl = "https://example.com/notify/wechatpay",
                RefundNotifyUrl = "https://example.com/notify/wechatpay/refund"
            });

        var httpClient = new HttpClient();
        var clientLogger = new Mock<ILogger<WeChatPayClient>>().Object;
        var options = Microsoft.Extensions.Options.Options.Create(new WeChatPayOptions
        {
            AppId = "wx1234567890",
            MchId = "1234567890",
            ApiV3Key = "test_v3_key",
            PrivateKey = "-----BEGIN PRIVATE KEY-----\ntest\n-----END PRIVATE KEY-----",
            SerialNo = "SERIAL001"
        });
        var client = new WeChatPayClient(httpClient, options, clientLogger);
        var adapterLogger = new Mock<ILogger<WeChatPayAdapter>>().Object;
        var adapter = new WeChatPayAdapter(client, configProviderMock.Object, adapterLogger);

        var orderRepoMock = new Mock<IPaymentOrderRepository>();
        var refundRepoMock = new Mock<IRefundOrderRepository>();
        var uowMock = new Mock<IUnitOfWork>();
        var handlerLogger = new Mock<ILogger<WeChatPayNotifyHandler>>().Object;

        var sut = new WeChatPayNotifyHandler(
            adapter, orderRepoMock.Object, refundRepoMock.Object, uowMock.Object, redis: null, handlerLogger);

        var rawBody = "{\"id\":\"evt-001\",\"event_type\":\"TRANSACTION.SUCCESS\"}";
        var headers = new Dictionary<string, string>
        {
            ["Wechatpay-Timestamp"] = "1234567890",
            ["Wechatpay-Nonce"] = "nonce123",
            ["Wechatpay-Signature"] = "sig",
            ["Wechatpay-Serial"] = "serial_001"
        };

        // Act：验签失败返回 FAIL（Redis null 不影响——验签在幂等检查之前）
        var result = await sut.HandleAsync(rawBody, headers);

        // Assert：验签失败返回 FAIL，不崩溃
        result.Should().Be("FAIL");
        uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion
}
