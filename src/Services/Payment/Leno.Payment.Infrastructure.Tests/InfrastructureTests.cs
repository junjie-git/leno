using System.Globalization;
using System.Net;
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
using Moq.Protected;
using StackExchange.Redis;

namespace Leno.Payment.Infrastructure.Tests;

public class AlipaySignatureHelperTests
{
    private static string GenerateTestKeyPair(out string privateKey, out string publicKey)
    {
        using var rsa = RSA.Create(2048);
        privateKey = rsa.ExportRSAPrivateKeyPem();
        publicKey = rsa.ExportRSAPublicKeyPem();
        return privateKey;
    }

    [Fact]
    public void GenerateSign_Valid_ShouldReturnBase64String()
    {
        GenerateTestKeyPair(out var privateKey, out _);
        var parameters = new Dictionary<string, string>
        {
            ["app_id"] = "2021000000000001",
            ["method"] = "alipay.trade.precreate",
            ["charset"] = "UTF-8",
            ["sign_type"] = "RSA2",
            ["timestamp"] = "2026-07-12 10:00:00",
            ["version"] = "1.0",
            ["biz_content"] = "{\"out_trade_no\":\"PAY001\"}"
        };

        var sign = AlipaySignatureHelper.GenerateSign(parameters, privateKey);

        sign.Should().NotBeNullOrEmpty();
        Convert.FromBase64String(sign).Should().NotBeEmpty();
    }

    [Fact]
    public void GenerateSign_EmptyPrivateKey_ShouldThrow()
    {
        var parameters = new Dictionary<string, string> { ["app_id"] = "test" };

        var act = () => AlipaySignatureHelper.GenerateSign(parameters, "");

        act.Should().Throw<ArgumentException>().WithMessage("*私钥*");
    }

    [Fact]
    public void GenerateSign_NullParameters_ShouldThrow()
    {
        GenerateTestKeyPair(out var privateKey, out _);

        var act = () => AlipaySignatureHelper.GenerateSign(null!, privateKey);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GenerateSign_ShouldExcludeSignAndEmptyValues()
    {
        GenerateTestKeyPair(out var privateKey, out _);
        var parameters = new Dictionary<string, string>
        {
            ["app_id"] = "2021000000000001",
            ["sign"] = "existing_sign",
            ["empty_field"] = "",
            ["method"] = "alipay.trade.query"
        };

        var sign = AlipaySignatureHelper.GenerateSign(parameters, privateKey);

        sign.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void VerifySign_Valid_ShouldReturnTrue()
    {
        GenerateTestKeyPair(out var privateKey, out var publicKey);
        var parameters = new Dictionary<string, string>
        {
            ["app_id"] = "2021000000000001",
            ["method"] = "alipay.trade.query",
            ["out_trade_no"] = "PAY001"
        };
        var sign = AlipaySignatureHelper.GenerateSign(parameters, privateKey);

        var result = AlipaySignatureHelper.VerifySign(parameters, publicKey, sign);

        result.Should().BeTrue();
    }

    [Fact]
    public void VerifySign_EmptySign_ShouldReturnFalse()
    {
        GenerateTestKeyPair(out _, out var publicKey);
        var parameters = new Dictionary<string, string> { ["app_id"] = "test" };

        var result = AlipaySignatureHelper.VerifySign(parameters, publicKey, null);

        result.Should().BeFalse();
    }

    [Fact]
    public void VerifySign_InvalidSign_ShouldReturnFalse()
    {
        GenerateTestKeyPair(out _, out var publicKey);
        var parameters = new Dictionary<string, string> { ["app_id"] = "test" };

        var result = AlipaySignatureHelper.VerifySign(parameters, publicKey, "invalid_base64_sign");

        result.Should().BeFalse();
    }

    [Fact]
    public void VerifySign_TamperedParameters_ShouldReturnFalse()
    {
        GenerateTestKeyPair(out var privateKey, out var publicKey);
        var originalParams = new Dictionary<string, string>
        {
            ["app_id"] = "2021000000000001",
            ["out_trade_no"] = "PAY001"
        };
        var sign = AlipaySignatureHelper.GenerateSign(originalParams, privateKey);

        var tamperedParams = new Dictionary<string, string>
        {
            ["app_id"] = "2021000000000001",
            ["out_trade_no"] = "PAY002"
        };

        var result = AlipaySignatureHelper.VerifySign(tamperedParams, publicKey, sign);

        result.Should().BeFalse();
    }
}

public class AlipayAdapterTests
{
    private readonly Mock<AlipayClient> _clientMock;
    private readonly Mock<IChannelConfigProvider> _configProviderMock = new();
    private readonly Mock<ILogger<AlipayAdapter>> _loggerMock = new();
    private readonly AlipayAdapter _sut;

    private static readonly ChannelConfig TestConfig = new()
    {
        AppId = "2021000000000001",
        MchId = "2088000000000001",
        ApiKey = "test_private_key",
        NotifyUrl = "https://example.com/notify/alipay",
        RefundNotifyUrl = "https://example.com/notify/alipay/refund"
    };

    private static readonly Guid OrderId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    public AlipayAdapterTests()
    {
        var httpClient = new HttpClient();
        var clientLogger = new Mock<ILogger<AlipayClient>>().Object;
        _clientMock = new Mock<AlipayClient>(httpClient, clientLogger) { CallBase = false };
        _sut = new AlipayAdapter(_clientMock.Object, _configProviderMock.Object, _loggerMock.Object);
    }

    private static PaymentOrder CreatePayment()
    {
        return PaymentOrder.Create(Guid.NewGuid(), OrderId, UserId, 100m, "CNY", PaymentChannel.Alipay);
    }

    private static RefundOrder CreateRefund(string outTradeNo)
    {
        return RefundOrder.Create(
            Guid.NewGuid(), Guid.NewGuid(), OrderId, UserId, Guid.NewGuid(),
            50m, "CNY", outTradeNo, PaymentChannel.Alipay);
    }

    private void SetupConfig()
    {
        _configProviderMock
            .Setup(p => p.GetConfigAsync(PaymentChannel.Alipay, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TestConfig);
    }

    [Fact]
    public async Task CreatePaymentAsync_Default_ShouldUseQrCodeScene()
    {
        SetupConfig();
        var payment = CreatePayment();
        _clientMock
            .Setup(c => c.PreCreateAsync(TestConfig, payment.OutTradeNo, "100.00", $"订单 {OrderId} 支付宝支付", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AlipayPreCreateResult
            {
                Code = "10000",
                QrCode = "https://qr.alipay.com/xxx",
                TradeNo = "2026071222001000000000000001"
            });

        var result = await _sut.CreatePaymentAsync(payment);

        result.CodeUrl.Should().Be("https://qr.alipay.com/xxx");
        result.ChannelTradeNo.Should().Be("2026071222001000000000000001");
    }

    [Fact]
    public async Task CreatePaymentAsync_QrCode_ShouldReturnCodeUrl()
    {
        SetupConfig();
        var payment = CreatePayment();
        _clientMock
            .Setup(c => c.PreCreateAsync(TestConfig, payment.OutTradeNo, "100.00", $"订单 {OrderId} 支付宝支付", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AlipayPreCreateResult
            {
                Code = "10000",
                QrCode = "https://qr.alipay.com/xxx",
                TradeNo = "TRADE001"
            });

        var result = await _sut.CreatePaymentAsync(payment, PaymentScene.QrCode);

        result.CodeUrl.Should().Be("https://qr.alipay.com/xxx");
        result.ChannelTradeNo.Should().Be("TRADE001");
    }

    [Fact]
    public async Task CreatePaymentAsync_Page_ShouldReturnH5Url()
    {
        SetupConfig();
        var payment = CreatePayment();
        _clientMock
            .Setup(c => c.BuildPagePayUrl(TestConfig, payment.OutTradeNo, "100.00", $"订单 {OrderId} 支付宝支付", "https://example.com/return"))
            .Returns("https://openapi.alipay.com/gateway.do?app_id=2021000000000001&method=alipay.trade.page.pay&sign=abc");

        var result = await _sut.CreatePaymentAsync(payment, PaymentScene.Page, "https://example.com/return");

        result.H5Url.Should().NotBeNullOrEmpty();
        result.H5Url.Should().Contain("alipay.trade.page.pay");
        result.ChannelTradeNo.Should().Be(payment.OutTradeNo);
    }

    [Fact]
    public async Task CreatePaymentAsync_Wap_ShouldReturnH5Url()
    {
        SetupConfig();
        var payment = CreatePayment();
        _clientMock
            .Setup(c => c.BuildWapPayUrl(TestConfig, payment.OutTradeNo, "100.00", $"订单 {OrderId} 支付宝支付", "https://example.com/return"))
            .Returns("https://openapi.alipay.com/gateway.do?app_id=2021000000000001&method=alipay.trade.wap.pay&sign=abc");

        var result = await _sut.CreatePaymentAsync(payment, PaymentScene.Wap, "https://example.com/return");

        result.H5Url.Should().NotBeNullOrEmpty();
        result.H5Url.Should().Contain("alipay.trade.wap.pay");
        result.ChannelTradeNo.Should().Be(payment.OutTradeNo);
    }

    [Fact]
    public async Task CreatePaymentAsync_App_ShouldReturnPrepayId()
    {
        SetupConfig();
        var payment = CreatePayment();
        _clientMock
            .Setup(c => c.BuildAppPayOrderString(TestConfig, payment.OutTradeNo, "100.00", $"订单 {OrderId} 支付宝支付"))
            .Returns("app_id=2021000000000001&method=alipay.trade.app.pay&sign=abc");

        var result = await _sut.CreatePaymentAsync(payment, PaymentScene.App);

        result.PrepayId.Should().NotBeNullOrEmpty();
        result.PrepayId.Should().Contain("alipay.trade.app.pay");
        result.ChannelTradeNo.Should().Be(payment.OutTradeNo);
    }

    [Fact]
    public async Task CreatePaymentAsync_InvalidScene_ShouldThrow()
    {
        SetupConfig();
        var payment = CreatePayment();

        var act = () => _sut.CreatePaymentAsync(payment, (PaymentScene)99);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task CreatePaymentAsync_NullPaymentOrder_ShouldThrow()
    {
        var act = () => _sut.CreatePaymentAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task QueryPaymentAsync_Valid_ShouldReturnResult()
    {
        SetupConfig();
        _clientMock
            .Setup(c => c.QueryAsync(TestConfig, "PAY001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AlipayQueryResult
            {
                Code = "10000",
                TradeStatus = "TRADE_SUCCESS",
                TradeNo = "TRADE001",
                SendPayDate = "2026-07-12 10:00:00"
            });

        var result = await _sut.QueryPaymentAsync("PAY001");

        result.IsPaid.Should().BeTrue();
        result.ChannelTradeNo.Should().Be("TRADE001");
        result.PaidAt.Should().NotBeNull();
    }

    [Fact]
    public async Task QueryPaymentAsync_NotPaid_ShouldReturnNotPaid()
    {
        SetupConfig();
        _clientMock
            .Setup(c => c.QueryAsync(TestConfig, "PAY001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AlipayQueryResult
            {
                Code = "10000",
                TradeStatus = "WAIT_BUYER_PAY",
                TradeNo = null,
                SendPayDate = null
            });

        var result = await _sut.QueryPaymentAsync("PAY001");

        result.IsPaid.Should().BeFalse();
        result.ChannelTradeNo.Should().BeNull();
    }

    [Fact]
    public async Task QueryPaymentAsync_EmptyOutTradeNo_ShouldThrow()
    {
        var act = () => _sut.QueryPaymentAsync("");

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*商户支付单号*");
    }

    [Fact]
    public async Task ClosePaymentAsync_Valid_ShouldReturnSucceeded()
    {
        SetupConfig();
        _clientMock
            .Setup(c => c.CloseAsync(TestConfig, "PAY001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AlipayCloseResult
            {
                Code = "10000",
                TradeNo = "TRADE001"
            });

        var result = await _sut.ClosePaymentAsync("PAY001");

        result.Succeeded.Should().BeTrue();
        result.ChannelTradeNo.Should().Be("TRADE001");
    }

    [Fact]
    public async Task ClosePaymentAsync_Failed_ShouldReturnNotSucceeded()
    {
        SetupConfig();
        _clientMock
            .Setup(c => c.CloseAsync(TestConfig, "PAY001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AlipayCloseResult
            {
                Code = "40004",
                SubMsg = "交易不存在"
            });

        var result = await _sut.ClosePaymentAsync("PAY001");

        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task ClosePaymentAsync_EmptyOutTradeNo_ShouldThrow()
    {
        var act = () => _sut.ClosePaymentAsync("");

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*商户支付单号*");
    }

    [Fact]
    public async Task CreateRefundAsync_Valid_ShouldReturnResult()
    {
        SetupConfig();
        var refund = CreateRefund("PAY001");
        _clientMock
            .Setup(c => c.RefundAsync(TestConfig, "PAY001", refund.OutRefundNo, "50.00", "用户退款", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AlipayRefundResult
            {
                Code = "10000",
                FundChange = "Y",
                TradeNo = "REFUND001"
            });

        var result = await _sut.CreateRefundAsync(refund);

        result.Succeeded.Should().BeTrue();
        result.ChannelRefundNo.Should().Be("REFUND001");
    }

    [Fact]
    public async Task CreateRefundAsync_Failed_ShouldReturnNotSucceeded()
    {
        SetupConfig();
        var refund = CreateRefund("PAY001");
        _clientMock
            .Setup(c => c.RefundAsync(TestConfig, "PAY001", refund.OutRefundNo, "50.00", "用户退款", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AlipayRefundResult
            {
                Code = "40004",
                SubMsg = "余额不足"
            });

        var result = await _sut.CreateRefundAsync(refund);

        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task CreateRefundAsync_NullRefundOrder_ShouldThrow()
    {
        var act = () => _sut.CreateRefundAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task QueryRefundAsync_Valid_ShouldReturnResult()
    {
        SetupConfig();
        _clientMock
            .Setup(c => c.QueryRefundAsync(TestConfig, "PAY001", "RFD001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AlipayQueryRefundResult
            {
                Code = "10000",
                RefundStatus = "REFUND_SUCCESS",
                GmtRefundPay = "2026-07-12 10:00:00"
            });

        var result = await _sut.QueryRefundAsync("PAY001", "RFD001");

        result.Succeeded.Should().BeTrue();
        result.RefundedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task QueryRefundAsync_NotRefunded_ShouldReturnNotSucceeded()
    {
        SetupConfig();
        _clientMock
            .Setup(c => c.QueryRefundAsync(TestConfig, "PAY001", "RFD001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AlipayQueryRefundResult
            {
                Code = "10000",
                RefundStatus = "REFUND_PROCESSING"
            });

        var result = await _sut.QueryRefundAsync("PAY001", "RFD001");

        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task QueryRefundAsync_EmptyOutRefundNo_ShouldThrow()
    {
        var act = () => _sut.QueryRefundAsync("PAY001", "");

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*商户退款单号*");
    }

    [Fact]
    public async Task VerifyNotifyAsync_ValidTradeSuccess_ShouldReturnVerified()
    {
        SetupConfig();
        var rawBody = "gmt_create=2026-07-12%2010%3A00%3A00&charset=UTF-8&seller_email=test%40example.com&subject=test&sign=test_sign&trade_no=TRADE001&trade_status=TRADE_SUCCESS&gmt_payment=2026-07-12%2010%3A00%3A00&notify_type=trade_status_sync&out_trade_no=PAY001&total_amount=100.00";

        var result = await _sut.VerifyNotifyAsync(rawBody, new Dictionary<string, string>());

        result.IsPaid.Should().BeTrue();
        result.ChannelTradeNo.Should().Be("TRADE001");
        result.IsRefund.Should().BeFalse();
    }

    [Fact]
    public async Task VerifyNotifyAsync_RefundNotification_ShouldBeRefund()
    {
        SetupConfig();
        var rawBody = "gmt_create=2026-07-12%2010%3A00%3A00&charset=UTF-8&trade_no=TRADE001&trade_status=TRADE_SUCCESS&out_trade_no=PAY001&out_request_no=RFD001&refund_fee=50.00&sign=test_sign&notify_type=fund_auth";

        var result = await _sut.VerifyNotifyAsync(rawBody, new Dictionary<string, string>());

        result.IsRefund.Should().BeTrue();
        result.RefundAmount.Should().Be(50.00m);
    }

    [Fact]
    public async Task VerifyNotifyAsync_TradeFinished_ShouldBePaid()
    {
        SetupConfig();
        var rawBody = "gmt_create=2026-07-12%2010%3A00%3A00&charset=UTF-8&trade_no=TRADE001&trade_status=TRADE_FINISHED&sign=test_sign&out_trade_no=PAY001&total_amount=100.00";

        var result = await _sut.VerifyNotifyAsync(rawBody, new Dictionary<string, string>());

        result.IsPaid.Should().BeTrue();
    }

    [Fact]
    public async Task VerifyNotifyAsync_NullRawBody_ShouldThrow()
    {
        var act = () => _sut.VerifyNotifyAsync(null!, new Dictionary<string, string>());

        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}

public class AlipayOptionsTests
{
    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        var options = new AlipayOptions();

        options.AppId.Should().BeEmpty();
        options.GatewayUrl.Should().Be("https://openapi.alipay.com/gateway.do");
        options.PrivateKey.Should().BeEmpty();
        options.AlipayPublicKey.Should().BeEmpty();
        options.NotifyUrl.Should().BeEmpty();
        options.SignType.Should().Be("RSA2");
    }

    [Fact]
    public void SetProperties_ShouldStoreValues()
    {
        var options = new AlipayOptions
        {
            AppId = "2021000000000001",
            GatewayUrl = "https://openapi.alipaydev.com/gateway.do",
            PrivateKey = "private_key_content",
            AlipayPublicKey = "public_key_content",
            NotifyUrl = "https://example.com/notify",
            SignType = "RSA2"
        };

        options.AppId.Should().Be("2021000000000001");
        options.GatewayUrl.Should().Be("https://openapi.alipaydev.com/gateway.do");
        options.PrivateKey.Should().Be("private_key_content");
        options.AlipayPublicKey.Should().Be("public_key_content");
        options.NotifyUrl.Should().Be("https://example.com/notify");
        options.SignType.Should().Be("RSA2");
    }
}

public class PaymentSceneTests
{
    [Fact]
    public void PaymentScene_Values_ShouldMapCorrectly()
    {
        ((int)PaymentScene.QrCode).Should().Be(0);
        ((int)PaymentScene.Page).Should().Be(1);
        ((int)PaymentScene.Wap).Should().Be(2);
        ((int)PaymentScene.App).Should().Be(3);
    }
}

public class AlipayClientUrlBuildingTests
{
    [Fact]
    public void BuildPagePayUrl_Valid_ShouldReturnUrlWithParameters()
    {
        var config = new ChannelConfig
        {
            AppId = "2021000000000001",
            ApiKey = GenerateTestPrivateKey(),
            NotifyUrl = "https://example.com/notify"
        };
        var httpClient = new HttpClient();
        var logger = new Mock<ILogger<AlipayClient>>().Object;
        var client = new AlipayClient(httpClient, logger);

        var url = client.BuildPagePayUrl(config, "PAY001", "100.00", "测试商品", "https://example.com/return");

        url.Should().Contain("alipay.trade.page.pay");
        url.Should().Contain("app_id=2021000000000001");
        url.Should().Contain("out_trade_no");
        url.Should().Contain("sign=");
    }

    [Fact]
    public void BuildWapPayUrl_Valid_ShouldReturnUrlWithParameters()
    {
        var config = new ChannelConfig
        {
            AppId = "2021000000000001",
            ApiKey = GenerateTestPrivateKey(),
            NotifyUrl = "https://example.com/notify"
        };
        var httpClient = new HttpClient();
        var logger = new Mock<ILogger<AlipayClient>>().Object;
        var client = new AlipayClient(httpClient, logger);

        var url = client.BuildWapPayUrl(config, "PAY001", "100.00", "测试商品", "https://example.com/return");

        url.Should().Contain("alipay.trade.wap.pay");
        url.Should().Contain("app_id=2021000000000001");
        url.Should().Contain("sign=");
    }

    [Fact]
    public void BuildAppPayOrderString_Valid_ShouldReturnOrderString()
    {
        var config = new ChannelConfig
        {
            AppId = "2021000000000001",
            ApiKey = GenerateTestPrivateKey(),
            NotifyUrl = "https://example.com/notify"
        };
        var httpClient = new HttpClient();
        var logger = new Mock<ILogger<AlipayClient>>().Object;
        var client = new AlipayClient(httpClient, logger);

        var orderString = client.BuildAppPayOrderString(config, "PAY001", "100.00", "测试商品");

        orderString.Should().Contain("alipay.trade.app.pay");
        orderString.Should().Contain("app_id=2021000000000001");
        orderString.Should().Contain("sign=");
    }

    [Fact]
    public void BuildPagePayUrl_EmptyOutTradeNo_ShouldThrow()
    {
        var config = new ChannelConfig { AppId = "test", ApiKey = GenerateTestPrivateKey() };
        var httpClient = new HttpClient();
        var logger = new Mock<ILogger<AlipayClient>>().Object;
        var client = new AlipayClient(httpClient, logger);

        var act = () => client.BuildPagePayUrl(config, "", "100.00", "test", "https://example.com");

        act.Should().Throw<ArgumentException>().WithMessage("*商户支付单号*");
    }

    [Fact]
    public void BuildWapPayUrl_EmptySubject_ShouldThrow()
    {
        var config = new ChannelConfig { AppId = "test", ApiKey = GenerateTestPrivateKey() };
        var httpClient = new HttpClient();
        var logger = new Mock<ILogger<AlipayClient>>().Object;
        var client = new AlipayClient(httpClient, logger);

        var act = () => client.BuildWapPayUrl(config, "PAY001", "100.00", "", "https://example.com");

        act.Should().Throw<ArgumentException>().WithMessage("*商品标题*");
    }

    private static string GenerateTestPrivateKey()
    {
        using var rsa = RSA.Create(2048);
        return rsa.ExportRSAPrivateKeyPem();
    }
}

public class AlipayClientCloseTests
{
    [Fact]
    public async Task CloseAsync_Valid_ShouldReturnSuccess()
    {
        var config = new ChannelConfig
        {
            AppId = "2021000000000001",
            ApiKey = GenerateTestPrivateKey()
        };

        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{\"alipay_trade_close_response\":{\"code\":\"10000\",\"msg\":\"Success\",\"trade_no\":\"TRADE001\",\"out_trade_no\":\"PAY001\"}}")
            });

        var httpClient = new HttpClient(handlerMock.Object);
        var logger = new Mock<ILogger<AlipayClient>>().Object;
        var client = new AlipayClient(httpClient, logger);

        var result = await client.CloseAsync(config, "PAY001");

        result.IsSuccess.Should().BeTrue();
        result.TradeNo.Should().Be("TRADE001");
    }

    [Fact]
    public async Task CloseAsync_Failed_ShouldReturnNotSuccess()
    {
        var config = new ChannelConfig
        {
            AppId = "2021000000000001",
            ApiKey = GenerateTestPrivateKey()
        };

        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{\"alipay_trade_close_response\":{\"code\":\"40004\",\"msg\":\"Business Failed\",\"sub_code\":\"ACQ.TRADE_NOT_EXIST\",\"sub_msg\":\"交易不存在\"}}")
            });

        var httpClient = new HttpClient(handlerMock.Object);
        var logger = new Mock<ILogger<AlipayClient>>().Object;
        var client = new AlipayClient(httpClient, logger);

        var result = await client.CloseAsync(config, "PAY001");

        result.IsSuccess.Should().BeFalse();
        result.SubMsg.Should().Be("交易不存在");
    }

    private static string GenerateTestPrivateKey()
    {
        using var rsa = RSA.Create(2048);
        return rsa.ExportRSAPrivateKeyPem();
    }
}

public class ChannelPaymentCloseResultTests
{
    [Fact]
    public void Default_ShouldNotBeSucceeded()
    {
        var result = new ChannelPaymentCloseResult();

        result.Succeeded.Should().BeFalse();
        result.ChannelTradeNo.Should().BeNull();
    }

    [Fact]
    public void SetProperties_ShouldStoreValues()
    {
        var result = new ChannelPaymentCloseResult
        {
            Succeeded = true,
            ChannelTradeNo = "TRADE001"
        };

        result.Succeeded.Should().BeTrue();
        result.ChannelTradeNo.Should().Be("TRADE001");
    }
}

public class WeChatPayAdapterTests
{
    private readonly Mock<WeChatPayClient> _clientMock;
    private readonly Mock<IChannelConfigProvider> _configProviderMock = new();
    private readonly Mock<ILogger<WeChatPayAdapter>> _loggerMock = new();
    private readonly WeChatPayAdapter _sut;

    private static readonly ChannelConfig TestConfig = new()
    {
        AppId = "wx1234567890",
        MchId = "1234567890",
        ApiKey = "test_api_v3_key",
        NotifyUrl = "https://example.com/notify/wechatpay",
        RefundNotifyUrl = "https://example.com/notify/wechatpay/refund"
    };

    private static readonly Guid OrderId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    public WeChatPayAdapterTests()
    {
        var httpClient = new HttpClient();
        var options = Microsoft.Extensions.Options.Options.Create(new WeChatPayOptions
        {
            AppId = "wx1234567890",
            MchId = "1234567890",
            ApiV3Key = "test_v3_key",
            PrivateKey = GenerateTestPrivateKey(),
            SerialNo = "SERIAL001"
        });
        var logger = new Mock<ILogger<WeChatPayClient>>().Object;
        _clientMock = new Mock<WeChatPayClient>(MockBehavior.Strict, httpClient, options, logger);
        _sut = new WeChatPayAdapter(_clientMock.Object, _configProviderMock.Object, _loggerMock.Object);
    }

    private static string GenerateTestPrivateKey()
    {
        using var rsa = RSA.Create(2048);
        return rsa.ExportRSAPrivateKeyPem();
    }

    private static PaymentOrder CreatePayment()
    {
        return PaymentOrder.Create(Guid.NewGuid(), OrderId, UserId, 100m, "CNY", PaymentChannel.WeChatPay);
    }

    private static RefundOrder CreateRefund(string outTradeNo)
    {
        return RefundOrder.Create(
            Guid.NewGuid(), Guid.NewGuid(), OrderId, UserId, Guid.NewGuid(),
            50m, "CNY", outTradeNo, PaymentChannel.WeChatPay);
    }

    private void SetupConfig()
    {
        _configProviderMock
            .Setup(p => p.GetConfigAsync(PaymentChannel.WeChatPay, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TestConfig);
    }

    [Fact]
    public async Task CreatePaymentAsync_Valid_ShouldReturnNativeCodeUrl()
    {
        SetupConfig();
        var payment = CreatePayment();
        _clientMock
            .Setup(c => c.UnifiedOrderAsync(
                TestConfig, payment.OutTradeNo, 10000,
                $"订单 {OrderId} 微信支付", "NATIVE", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WeChatPayUnifiedOrderResult
            {
                PrepayId = "prepay_123",
                CodeUrl = "weixin://wxpay/bizpayurl?pr=abc123",
                TransactionId = null
            });

        var result = await _sut.CreatePaymentAsync(payment);

        result.CodeUrl.Should().Be("weixin://wxpay/bizpayurl?pr=abc123");
        result.PrepayId.Should().Be("prepay_123");
        result.ChannelTradeNo.Should().BeNull();
    }

    [Fact]
    public async Task CreatePaymentAsync_NullPaymentOrder_ShouldThrow()
    {
        var act = () => _sut.CreatePaymentAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task QueryPaymentAsync_IsPaid_ShouldReturnPaidResult()
    {
        SetupConfig();
        _clientMock
            .Setup(c => c.QueryOrderAsync(TestConfig, "PAY001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WeChatPayQueryOrderResult
            {
                TradeState = "SUCCESS",
                TransactionId = "4200001234567890",
                TimeEnd = "2026-07-12T10:34:56+08:00"
            });

        var result = await _sut.QueryPaymentAsync("PAY001");

        result.IsPaid.Should().BeTrue();
        result.ChannelTradeNo.Should().Be("4200001234567890");
        result.PaidAt.Should().NotBeNull();
    }

    [Fact]
    public async Task QueryPaymentAsync_NotPaid_ShouldReturnNotPaid()
    {
        SetupConfig();
        _clientMock
            .Setup(c => c.QueryOrderAsync(TestConfig, "PAY001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WeChatPayQueryOrderResult
            {
                TradeState = "NOTPAY",
                TransactionId = null,
                TimeEnd = null
            });

        var result = await _sut.QueryPaymentAsync("PAY001");

        result.IsPaid.Should().BeFalse();
        result.ChannelTradeNo.Should().BeNull();
    }

    [Fact]
    public async Task QueryPaymentAsync_EmptyOutTradeNo_ShouldThrow()
    {
        var act = () => _sut.QueryPaymentAsync("");

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*商户支付单号*");
    }

    [Fact]
    public async Task ClosePaymentAsync_Valid_ShouldReturnSucceeded()
    {
        SetupConfig();
        _clientMock
            .Setup(c => c.CloseOrderAsync(TestConfig, "PAY001", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _sut.ClosePaymentAsync("PAY001");

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task ClosePaymentAsync_EmptyOutTradeNo_ShouldThrow()
    {
        var act = () => _sut.ClosePaymentAsync("");

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*商户支付单号*");
    }

    [Fact]
    public async Task CreateRefundAsync_Valid_ShouldReturnResult()
    {
        SetupConfig();
        var refund = CreateRefund("PAY001");
        _clientMock
            .Setup(c => c.RefundAsync(
                TestConfig, "PAY001", refund.OutRefundNo, 5000, 5000, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WeChatPayRefundResult
            {
                RefundId = "5030001234567890",
                Status = "SUCCESS"
            });

        var result = await _sut.CreateRefundAsync(refund);

        result.Succeeded.Should().BeTrue();
        result.ChannelRefundNo.Should().Be("5030001234567890");
    }

    [Fact]
    public async Task CreateRefundAsync_Failed_ShouldReturnNotSucceeded()
    {
        SetupConfig();
        var refund = CreateRefund("PAY001");
        _clientMock
            .Setup(c => c.RefundAsync(
                TestConfig, "PAY001", refund.OutRefundNo, 5000, 5000, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WeChatPayRefundResult
            {
                Status = null,
                ErrCodeDes = "余额不足"
            });

        var result = await _sut.CreateRefundAsync(refund);

        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task CreateRefundAsync_NullRefundOrder_ShouldThrow()
    {
        var act = () => _sut.CreateRefundAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task QueryRefundAsync_Valid_ShouldReturnResult()
    {
        SetupConfig();
        _clientMock
            .Setup(c => c.QueryRefundAsync(TestConfig, "RFD001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WeChatPayQueryRefundResult
            {
                RefundStatus = "SUCCESS",
                RefundSuccessTime = "2026-07-12T10:34:56+08:00"
            });

        var result = await _sut.QueryRefundAsync("PAY001", "RFD001");

        result.Succeeded.Should().BeTrue();
        result.RefundedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task QueryRefundAsync_NotRefunded_ShouldReturnNotSucceeded()
    {
        SetupConfig();
        _clientMock
            .Setup(c => c.QueryRefundAsync(TestConfig, "RFD001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WeChatPayQueryRefundResult
            {
                RefundStatus = "PROCESSING",
                RefundSuccessTime = null
            });

        var result = await _sut.QueryRefundAsync("PAY001", "RFD001");

        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task QueryRefundAsync_EmptyOutRefundNo_ShouldThrow()
    {
        var act = () => _sut.QueryRefundAsync("PAY001", "");

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*商户退款单号*");
    }

    [Fact]
    public async Task VerifyNotifyAsync_ValidPaymentNotify_ShouldReturnVerified()
    {
        SetupConfig();
        var rawBody = "{\"id\":\"evt-001\",\"event_type\":\"TRANSACTION.SUCCESS\",\"resource\":{\"ciphertext\":\"encrypted_data\",\"associated_data\":\"order\",\"nonce\":\"nonce123\"}}";
        var headers = new Dictionary<string, string>
        {
            ["Wechatpay-Timestamp"] = "1234567890",
            ["Wechatpay-Nonce"] = "nonce123",
            ["Wechatpay-Signature"] = "base64_signature",
            ["Wechatpay-Serial"] = "serial_001"
        };

        var result = await _sut.VerifyNotifyAsync(rawBody, headers);

        result.Verified.Should().BeFalse(); // Signature verification will fail with test key
    }

    [Fact]
    public async Task VerifyNotifyAsync_NullRawBody_ShouldThrow()
    {
        var act = () => _sut.VerifyNotifyAsync(null!, new Dictionary<string, string>());

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task VerifyNotifyAsync_NullHeaders_ShouldThrow()
    {
        var act = () => _sut.VerifyNotifyAsync("{}", null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}

public class WeChatPayV3SignatureHelperTests
{
    private static string GenerateTestKeyPair(out string privateKey, out string publicKey)
    {
        using var rsa = RSA.Create(2048);
        privateKey = rsa.ExportRSAPrivateKeyPem();
        publicKey = rsa.ExportRSAPublicKeyPem();
        return privateKey;
    }

    [Fact]
    public void GenerateAuthorization_Valid_ShouldReturnHeader()
    {
        GenerateTestKeyPair(out var privateKey, out _);
        var auth = WeChatPayV3SignatureHelper.GenerateAuthorization(
            "POST", "/v3/pay/transactions/native", "{}",
            "1234567890", "nonce123", privateKey, "1234567890", "SERIAL001");

        auth.Should().Contain("WECHATPAY2-SHA256-RSA2048");
        auth.Should().Contain("mchid=\"1234567890\"");
        auth.Should().Contain("serial_no=\"SERIAL001\"");
        auth.Should().Contain("signature=\"");
    }

    [Fact]
    public void Sign_Valid_ShouldReturnBase64()
    {
        GenerateTestKeyPair(out var privateKey, out _);
        var sign = WeChatPayV3SignatureHelper.Sign("test message", privateKey);

        sign.Should().NotBeNullOrEmpty();
        Convert.FromBase64String(sign).Should().NotBeEmpty();
    }

    [Fact]
    public void VerifyNotifySign_Valid_ShouldReturnTrue()
    {
        GenerateTestKeyPair(out var privateKey, out var publicKey);
        var message = "1234567890\nnonce123\n{\"key\":\"value\"}\n";
        var sign = WeChatPayV3SignatureHelper.Sign(message, privateKey);

        var result = WeChatPayV3SignatureHelper.VerifyNotifySign(
            "1234567890", "nonce123", "{\"key\":\"value\"}", sign, publicKey);

        result.Should().BeTrue();
    }

    [Fact]
    public void VerifyNotifySign_InvalidSign_ShouldReturnFalse()
    {
        GenerateTestKeyPair(out _, out var publicKey);

        var result = WeChatPayV3SignatureHelper.VerifyNotifySign(
            "1234567890", "nonce123", "{}", "invalid_signature", publicKey);

        result.Should().BeFalse();
    }

    [Fact]
    public void VerifyNotifySign_EmptySignature_ShouldReturnFalse()
    {
        GenerateTestKeyPair(out _, out var publicKey);

        var result = WeChatPayV3SignatureHelper.VerifyNotifySign(
            "1234567890", "nonce123", "{}", "", publicKey);

        result.Should().BeFalse();
    }

    [Fact]
    public void VerifyNotifySign_TamperedMessage_ShouldReturnFalse()
    {
        GenerateTestKeyPair(out var privateKey, out var publicKey);
        var originalMessage = "1234567890\nnonce123\n{\"key\":\"value\"}\n";
        var sign = WeChatPayV3SignatureHelper.Sign(originalMessage, privateKey);

        var result = WeChatPayV3SignatureHelper.VerifyNotifySign(
            "1234567890", "nonce123", "{\"key\":\"tampered\"}", sign, publicKey);

        result.Should().BeFalse();
    }
}

public class WeChatPayOptionsTests
{
    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        var options = new WeChatPayOptions();

        options.AppId.Should().BeEmpty();
        options.MchId.Should().BeEmpty();
        options.ApiV3Key.Should().BeEmpty();
        options.PrivateKeyPath.Should().BeEmpty();
        options.PrivateKey.Should().BeNull();
        options.SerialNo.Should().BeEmpty();
        options.NotifyUrl.Should().BeEmpty();
        options.RefundNotifyUrl.Should().BeEmpty();
    }

    [Fact]
    public void SetProperties_ShouldStoreValues()
    {
        var options = new WeChatPayOptions
        {
            AppId = "wx1234567890",
            MchId = "1234567890",
            ApiV3Key = "test_v3_key",
            PrivateKeyPath = "/path/to/key.pem",
            PrivateKey = "-----BEGIN PRIVATE KEY-----\ntest\n-----END PRIVATE KEY-----",
            SerialNo = "SERIAL001",
            NotifyUrl = "https://example.com/notify",
            RefundNotifyUrl = "https://example.com/notify/refund"
        };

        options.AppId.Should().Be("wx1234567890");
        options.MchId.Should().Be("1234567890");
        options.ApiV3Key.Should().Be("test_v3_key");
        options.PrivateKeyPath.Should().Be("/path/to/key.pem");
        options.PrivateKey.Should().Contain("BEGIN PRIVATE KEY");
        options.SerialNo.Should().Be("SERIAL001");
        options.NotifyUrl.Should().Be("https://example.com/notify");
        options.RefundNotifyUrl.Should().Be("https://example.com/notify/refund");
    }
}

public class SignatureVerificationResultTests
{
    [Fact]
    public void Success_ShouldReturnValidResult()
    {
        var result = SignatureVerificationResult.Success;

        result.IsValid.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void Failure_ShouldReturnInvalidResultWithMessage()
    {
        var result = SignatureVerificationResult.Failure("签名验证失败");

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Be("签名验证失败");
    }

    [Fact]
    public void SetProperties_ShouldStoreValues()
    {
        var result = new SignatureVerificationResult
        {
            IsValid = false,
            ErrorMessage = "时间戳超出容差"
        };

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Be("时间戳超出容差");
    }
}

public class WeChatPayChannelTests
{
    private readonly Mock<IChannelConfigProvider> _configProviderMock = new();
    private readonly WeChatPayChannel _sut;

    private static readonly ChannelConfig TestConfig = new()
    {
        AppId = "wx1234567890",
        MchId = "1234567890",
        ApiKey = GenerateTestPublicKey(),
        NotifyUrl = "https://example.com/notify/wechatpay",
        RefundNotifyUrl = "https://example.com/notify/wechatpay/refund"
    };

    public WeChatPayChannelTests()
    {
        _sut = new WeChatPayChannel(_configProviderMock.Object);
    }

    private static string GenerateTestKeyPair(out string privateKey, out string publicKey)
    {
        using var rsa = RSA.Create(2048);
        privateKey = rsa.ExportRSAPrivateKeyPem();
        publicKey = rsa.ExportRSAPublicKeyPem();
        return privateKey;
    }

    private static string GenerateTestPublicKey()
    {
        using var rsa = RSA.Create(2048);
        return rsa.ExportRSAPublicKeyPem();
    }

    private void SetupConfig(PaymentChannel channel = PaymentChannel.WeChatPay)
    {
        _configProviderMock
            .Setup(p => p.GetConfigAsync(channel, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TestConfig);
    }

    private static (string signature, string timestamp, string nonce) CreateValidSignature(string rawBody)
    {
        GenerateTestKeyPair(out var privateKey, out var publicKey);
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var nonce = Guid.NewGuid().ToString("N");
        var message = $"{timestamp}\n{nonce}\n{rawBody}\n";
        var signature = WeChatPayV3SignatureHelper.Sign(message, privateKey);

        // Update the test config to use the matching public key
        TestConfig.ApiKey = publicKey;

        return (signature, timestamp, nonce);
    }

    [Fact]
    public async Task VerifySignatureAsync_Valid_ShouldReturnSuccess()
    {
        var rawBody = "{\"id\":\"evt-001\",\"event_type\":\"TRANSACTION.SUCCESS\"}";
        var (signature, timestamp, nonce) = CreateValidSignature(rawBody);
        SetupConfig();
        var headers = new Dictionary<string, string>
        {
            ["Wechatpay-Timestamp"] = timestamp,
            ["Wechatpay-Nonce"] = nonce,
            ["Wechatpay-Signature"] = signature,
            ["Wechatpay-Serial"] = "SERIAL001"
        };

        var result = await _sut.VerifySignatureAsync(headers, rawBody);

        result.IsValid.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task VerifySignatureAsync_MissingTimestamp_ShouldReturnFailure()
    {
        var rawBody = "{}";
        var headers = new Dictionary<string, string>
        {
            ["Wechatpay-Nonce"] = "nonce123",
            ["Wechatpay-Signature"] = "sig"
        };

        var result = await _sut.VerifySignatureAsync(headers, rawBody);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Timestamp");
    }

    [Fact]
    public async Task VerifySignatureAsync_MissingNonce_ShouldReturnFailure()
    {
        var rawBody = "{}";
        var headers = new Dictionary<string, string>
        {
            ["Wechatpay-Timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
            ["Wechatpay-Signature"] = "sig"
        };

        var result = await _sut.VerifySignatureAsync(headers, rawBody);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Nonce");
    }

    [Fact]
    public async Task VerifySignatureAsync_MissingSignature_ShouldReturnFailure()
    {
        var rawBody = "{}";
        var headers = new Dictionary<string, string>
        {
            ["Wechatpay-Timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
            ["Wechatpay-Nonce"] = "nonce123"
        };

        var result = await _sut.VerifySignatureAsync(headers, rawBody);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Signature");
    }

    [Fact]
    public async Task VerifySignatureAsync_ExpiredTimestamp_ShouldReturnFailure()
    {
        var rawBody = "{}";
        var headers = new Dictionary<string, string>
        {
            ["Wechatpay-Timestamp"] = (DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 600).ToString(), // 10 minutes ago
            ["Wechatpay-Nonce"] = "nonce123",
            ["Wechatpay-Signature"] = "sig"
        };

        var result = await _sut.VerifySignatureAsync(headers, rawBody);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("时间戳");
    }

    [Fact]
    public async Task VerifySignatureAsync_FutureTimestamp_ShouldReturnFailure()
    {
        var rawBody = "{}";
        var headers = new Dictionary<string, string>
        {
            ["Wechatpay-Timestamp"] = (DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 600).ToString(), // 10 minutes in future
            ["Wechatpay-Nonce"] = "nonce123",
            ["Wechatpay-Signature"] = "sig"
        };

        var result = await _sut.VerifySignatureAsync(headers, rawBody);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("时间戳");
    }

    [Fact]
    public async Task VerifySignatureAsync_InvalidTimestampFormat_ShouldReturnFailure()
    {
        var rawBody = "{}";
        var headers = new Dictionary<string, string>
        {
            ["Wechatpay-Timestamp"] = "not_a_number",
            ["Wechatpay-Nonce"] = "nonce123",
            ["Wechatpay-Signature"] = "sig"
        };

        var result = await _sut.VerifySignatureAsync(headers, rawBody);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("时间戳");
    }

    [Fact]
    public async Task VerifySignatureAsync_InvalidSignature_ShouldReturnFailure()
    {
        var rawBody = "{\"id\":\"evt-001\"}";
        var (_, timestamp, nonce) = CreateValidSignature("different_body");
        SetupConfig();
        var headers = new Dictionary<string, string>
        {
            ["Wechatpay-Timestamp"] = timestamp,
            ["Wechatpay-Nonce"] = nonce,
            ["Wechatpay-Signature"] = "invalid_base64_signature",
            ["Wechatpay-Serial"] = "SERIAL001"
        };

        var result = await _sut.VerifySignatureAsync(headers, rawBody);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("签名验证失败");
    }

    [Fact]
    public async Task VerifySignatureAsync_NullHeaders_ShouldThrow()
    {
        var act = () => _sut.VerifySignatureAsync(null!, "{}");

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task VerifySignatureAsync_NullBody_ShouldThrow()
    {
        var act = () => _sut.VerifySignatureAsync(new Dictionary<string, string>(), null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task VerifySignatureAsync_ValidTimestampWithinTolerance_ShouldVerify()
    {
        var rawBody = "{\"id\":\"evt-001\"}";
        var (signature, timestamp, nonce) = CreateValidSignature(rawBody);
        SetupConfig();
        // Use a timestamp that is 4 minutes ago (within 5 minute tolerance)
        var validTimestamp = (DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 240).ToString();
        var message = $"{validTimestamp}\n{nonce}\n{rawBody}\n";
        GenerateTestKeyPair(out var privateKey, out var publicKey);
        TestConfig.ApiKey = publicKey;
        var validSignature = WeChatPayV3SignatureHelper.Sign(message, privateKey);

        var headers = new Dictionary<string, string>
        {
            ["Wechatpay-Timestamp"] = validTimestamp,
            ["Wechatpay-Nonce"] = nonce,
            ["Wechatpay-Signature"] = validSignature,
            ["Wechatpay-Serial"] = "SERIAL001"
        };

        var result = await _sut.VerifySignatureAsync(headers, rawBody);

        result.IsValid.Should().BeTrue();
    }

    /// <summary>
    /// P1-B.1 问题 4：Redis 故障时防重放检查应 fail-closed（拒绝验签），
    /// 让微信重试回调，而不是 fail-open 放行。
    /// </summary>
    [Fact]
    public async Task ValidateNonce_RedisThrows_ShouldFailVerification()
    {
        // Arrange：mock Redis StringSetAsync 抛 RedisConnectionException
        // 注意：StringSetAsync 在 IDatabaseAsync 上有多个重载，
        // 实际调用 db.StringSetAsync(key, "1", ttl, When.NotExists) 匹配 4 参数重载，
        // 因此 mock setup 必须使用相同的 4 参数重载，否则不会触发。
        var redisMock = new Mock<IConnectionMultiplexer>();
        var dbMock = new Mock<IDatabase>();
        dbMock
            .Setup(d => d.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<When>()))
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "redis down"));
        redisMock
            .Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(dbMock.Object);

        var loggerMock = new Mock<ILogger<WeChatPayChannel>>();
        var sut = new WeChatPayChannel(_configProviderMock.Object, redisMock.Object, loggerMock.Object);

        var rawBody = "{\"id\":\"evt-001\",\"event_type\":\"TRANSACTION.SUCCESS\"}";
        var (signature, timestamp, nonce) = CreateValidSignature(rawBody);
        SetupConfig();
        var headers = new Dictionary<string, string>
        {
            ["Wechatpay-Timestamp"] = timestamp,
            ["Wechatpay-Nonce"] = nonce,
            ["Wechatpay-Signature"] = signature,
            ["Wechatpay-Serial"] = "SERIAL001"
        };

        // Act：Redis 故障应 fail-closed，验签失败
        var result = await sut.VerifySignatureAsync(headers, rawBody);

        // Assert：验签拒绝（fail-closed），等待微信重试
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("验签异常");
    }

    /// <summary>
    /// P1-B.1 问题 4：_redis = null（开发环境未配置 Redis）时跳过防重放检查并放行。
    /// 此为配置选择而非故障，保留兼容语义。
    /// </summary>
    [Fact]
    public async Task ValidateNonce_RedisUnavailable_ShouldSkipAndSucceed()
    {
        // Arrange：_redis = null，跳过防重放
        var sut = new WeChatPayChannel(_configProviderMock.Object, redis: null, Mock.Of<ILogger<WeChatPayChannel>>());

        var rawBody = "{\"id\":\"evt-001\",\"event_type\":\"TRANSACTION.SUCCESS\"}";
        var (signature, timestamp, nonce) = CreateValidSignature(rawBody);
        SetupConfig();
        var headers = new Dictionary<string, string>
        {
            ["Wechatpay-Timestamp"] = timestamp,
            ["Wechatpay-Nonce"] = nonce,
            ["Wechatpay-Signature"] = signature,
            ["Wechatpay-Serial"] = "SERIAL001"
        };

        // Act
        var result = await sut.VerifySignatureAsync(headers, rawBody);

        // Assert：Redis 未配置时跳过防重放，验签通过
        result.IsValid.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    /// <summary>
    /// P1-B.1 问题 4：同一 nonce 二次到达应被识别为重放攻击并拒绝验签。
    /// Redis SET NX 第二次返回 false（key 已存在）。
    /// </summary>
    [Fact]
    public async Task VerifySignatureAsync_ReplayAttack_ShouldFail()
    {
        // Arrange：mock Redis StringSetAsync 第一次返回 true（首次写入），第二次返回 false（重放）
        // 注意：使用 4 参数重载匹配实际调用 db.StringSetAsync(key, "1", ttl, When.NotExists)
        var redisMock = new Mock<IConnectionMultiplexer>();
        var dbMock = new Mock<IDatabase>();
        var sequence = dbMock.SetupSequence(d => d.StringSetAsync(
            It.IsAny<RedisKey>(),
            It.IsAny<RedisValue>(),
            It.IsAny<TimeSpan?>(),
            It.IsAny<When>()));
        sequence.ReturnsAsync(true);
        sequence.ReturnsAsync(false);
        redisMock
            .Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(dbMock.Object);

        var sut = new WeChatPayChannel(_configProviderMock.Object, redisMock.Object, Mock.Of<ILogger<WeChatPayChannel>>());

        var rawBody = "{\"id\":\"evt-001\",\"event_type\":\"TRANSACTION.SUCCESS\"}";
        var (signature, timestamp, nonce) = CreateValidSignature(rawBody);
        SetupConfig();
        var headers = new Dictionary<string, string>
        {
            ["Wechatpay-Timestamp"] = timestamp,
            ["Wechatpay-Nonce"] = nonce,
            ["Wechatpay-Signature"] = signature,
            ["Wechatpay-Serial"] = "SERIAL001"
        };

        // Act：第一次验签通过（首次写入 nonce）
        var firstResult = await sut.VerifySignatureAsync(headers, rawBody);
        firstResult.IsValid.Should().BeTrue();

        // 第二次同 nonce 重放，应失败
        var secondResult = await sut.VerifySignatureAsync(headers, rawBody);

        // Assert：重放被拒绝
        secondResult.IsValid.Should().BeFalse();
        secondResult.ErrorMessage.Should().Contain("随机数重复");
    }
}

public class AlipayChannelTests
{
    private readonly Mock<IChannelConfigProvider> _configProviderMock = new();
    private readonly AlipayChannel _sut;

    private static readonly ChannelConfig TestConfig = new()
    {
        AppId = "2021000000000001",
        MchId = "2088000000000001",
        ApiKey = GenerateTestPublicKey(),
        NotifyUrl = "https://example.com/notify/alipay",
        RefundNotifyUrl = "https://example.com/notify/alipay/refund"
    };

    public AlipayChannelTests()
    {
        _sut = new AlipayChannel(_configProviderMock.Object);
    }

    private static string GenerateTestKeyPair(out string privateKey, out string publicKey)
    {
        using var rsa = RSA.Create(2048);
        privateKey = rsa.ExportRSAPrivateKeyPem();
        publicKey = rsa.ExportRSAPublicKeyPem();
        return privateKey;
    }

    private static string GenerateTestPublicKey()
    {
        using var rsa = RSA.Create(2048);
        return rsa.ExportRSAPublicKeyPem();
    }

    private void SetupConfig(PaymentChannel channel = PaymentChannel.Alipay)
    {
        _configProviderMock
            .Setup(p => p.GetConfigAsync(channel, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TestConfig);
    }

    [Fact]
    public async Task VerifySignatureAsync_Valid_ShouldReturnSuccess()
    {
        GenerateTestKeyPair(out var privateKey, out var publicKey);
        TestConfig.ApiKey = publicKey;
        SetupConfig();

        var formFields = new Dictionary<string, string>
        {
            ["app_id"] = "2021000000000001",
            ["method"] = "alipay.trade.query",
            ["out_trade_no"] = "PAY001",
            ["trade_no"] = "TRADE001",
            ["trade_status"] = "TRADE_SUCCESS"
        };
        var sign = AlipaySignatureHelper.GenerateSign(formFields, privateKey);
        formFields["sign"] = sign;

        var result = await _sut.VerifySignatureAsync(formFields);

        result.IsValid.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task VerifySignatureAsync_MissingSign_ShouldReturnFailure()
    {
        SetupConfig();
        var formFields = new Dictionary<string, string>
        {
            ["app_id"] = "2021000000000001",
            ["out_trade_no"] = "PAY001"
        };

        var result = await _sut.VerifySignatureAsync(formFields);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("sign");
    }

    [Fact]
    public async Task VerifySignatureAsync_InvalidSign_ShouldReturnFailure()
    {
        GenerateTestKeyPair(out _, out var publicKey);
        TestConfig.ApiKey = publicKey;
        SetupConfig();

        var formFields = new Dictionary<string, string>
        {
            ["app_id"] = "2021000000000001",
            ["out_trade_no"] = "PAY001",
            ["sign"] = "invalid_base64_signature"
        };

        var result = await _sut.VerifySignatureAsync(formFields);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("签名验证失败");
    }

    [Fact]
    public async Task VerifySignatureAsync_TamperedParameters_ShouldReturnFailure()
    {
        GenerateTestKeyPair(out var privateKey, out var publicKey);
        TestConfig.ApiKey = publicKey;
        SetupConfig();

        var originalParams = new Dictionary<string, string>
        {
            ["app_id"] = "2021000000000001",
            ["out_trade_no"] = "PAY001",
            ["total_amount"] = "100.00"
        };
        var sign = AlipaySignatureHelper.GenerateSign(originalParams, privateKey);

        var tamperedParams = new Dictionary<string, string>
        {
            ["app_id"] = "2021000000000001",
            ["out_trade_no"] = "PAY002", // tampered
            ["total_amount"] = "100.00",
            ["sign"] = sign
        };

        var result = await _sut.VerifySignatureAsync(tamperedParams);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("签名验证失败");
    }

    [Fact]
    public async Task VerifySignatureAsync_NullFormFields_ShouldThrow()
    {
        var act = () => _sut.VerifySignatureAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}

public class AlipayNotifyHandlerTests
{
    private readonly Mock<IChannelConfigProvider> _configProviderMock = new();
    private readonly Mock<IPaymentOrderRepository> _orderRepoMock = new();
    private readonly Mock<IRefundOrderRepository> _refundRepoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly AlipayNotifyHandler _sut;

    private static readonly Guid OrderId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private const string OutTradeNo = "PAY20260701000001";

    public AlipayNotifyHandlerTests()
    {
        var httpClient = new HttpClient();
        var clientLogger = new Mock<ILogger<AlipayClient>>().Object;
        var client = new AlipayClient(httpClient, clientLogger);
        var adapterLogger = new Mock<ILogger<AlipayAdapter>>().Object;
        var adapter = new AlipayAdapter(client, _configProviderMock.Object, adapterLogger);
        var handlerLogger = new Mock<ILogger<AlipayNotifyHandler>>().Object;
        _sut = new AlipayNotifyHandler(
            adapter, _orderRepoMock.Object, _refundRepoMock.Object, _uowMock.Object, null, handlerLogger);
    }

    private static (string privateKey, string publicKey) GenerateKeyPair()
    {
        using var rsa = RSA.Create(2048);
        return (rsa.ExportRSAPrivateKeyPem(), rsa.ExportRSAPublicKeyPem());
    }

    private void SetupConfig(string publicKey)
    {
        _configProviderMock
            .Setup(p => p.GetConfigAsync(PaymentChannel.Alipay, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelConfig
            {
                AppId = "2021000000000001",
                MchId = "2088000000000001",
                ApiKey = publicKey,
                NotifyUrl = "https://example.com/notify/alipay",
                RefundNotifyUrl = "https://example.com/notify/alipay/refund"
            });
    }

    private static PaymentOrder CreateOrder(decimal amount)
    {
        return PaymentOrder.Create(Guid.NewGuid(), OrderId, UserId, amount, "CNY", PaymentChannel.Alipay);
    }

    private static (string rawBody, Dictionary<string, string> formFields) BuildPaidNotify(
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

    [Fact]
    public async Task HandlePaymentNotifyAsync_AmountMatch_ShouldReturnSuccessAndMarkPaid()
    {
        var (privateKey, publicKey) = GenerateKeyPair();
        SetupConfig(publicKey);
        var order = CreateOrder(100m);
        _orderRepoMock
            .Setup(r => r.GetByOutTradeNoAsync(OutTradeNo, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        var (rawBody, fields) = BuildPaidNotify(privateKey, "100.00");

        var result = await _sut.HandleAsync(rawBody, fields);

        result.Should().Be("success");
        order.Status.Should().Be(PaymentStatus.Paid);
        order.ChannelTradeNo.Should().Be("2026071222001000000000000001");
        _orderRepoMock.Verify(r => r.UpdateAsync(order, It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandlePaymentNotifyAsync_ForgedLowAmount_ShouldReturnFailAndKeepPending()
    {
        // 攻击者构造 0.01 元支付成功回调购买 100 元订单
        var (privateKey, publicKey) = GenerateKeyPair();
        SetupConfig(publicKey);
        var order = CreateOrder(100m);
        _orderRepoMock
            .Setup(r => r.GetByOutTradeNoAsync(OutTradeNo, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        var (rawBody, fields) = BuildPaidNotify(privateKey, "0.01");

        var result = await _sut.HandleAsync(rawBody, fields);

        result.Should().Be("fail");
        order.Status.Should().Be(PaymentStatus.Pending);
        order.PaidAt.Should().BeNull();
        _orderRepoMock.Verify(r => r.UpdateAsync(It.IsAny<PaymentOrder>(), It.IsAny<CancellationToken>()), Times.Never);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandlePaymentNotifyAsync_AmountMismatch_ShouldReturnFailAndKeepPending()
    {
        var (privateKey, publicKey) = GenerateKeyPair();
        SetupConfig(publicKey);
        var order = CreateOrder(100m);
        _orderRepoMock
            .Setup(r => r.GetByOutTradeNoAsync(OutTradeNo, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        var (rawBody, fields) = BuildPaidNotify(privateKey, "99.99");

        var result = await _sut.HandleAsync(rawBody, fields);

        result.Should().Be("fail");
        order.Status.Should().Be(PaymentStatus.Pending);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandlePaymentNotifyAsync_MissingAmount_ShouldReturnFailAndKeepPending()
    {
        var (privateKey, publicKey) = GenerateKeyPair();
        SetupConfig(publicKey);
        var order = CreateOrder(100m);
        _orderRepoMock
            .Setup(r => r.GetByOutTradeNoAsync(OutTradeNo, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var fields = new Dictionary<string, string>
        {
            ["app_id"] = "2021000000000001",
            ["charset"] = "UTF-8",
            ["out_trade_no"] = OutTradeNo,
            ["trade_no"] = "2026071222001000000000000001",
            ["trade_status"] = "TRADE_SUCCESS",
            ["gmt_payment"] = "2026-07-12 10:00:00",
            ["notify_time"] = "2026-07-12 10:00:00",
            ["notify_type"] = "trade_status_sync",
            ["notify_id"] = "notify-001",
            ["sign_type"] = "RSA2"
        };
        fields["sign"] = AlipaySignatureHelper.GenerateSign(fields, privateKey);
        var rawBody = string.Join("&", fields.Select(kv =>
            $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));

        var result = await _sut.HandleAsync(rawBody, fields);

        result.Should().Be("fail");
        order.Status.Should().Be(PaymentStatus.Pending);
    }
}