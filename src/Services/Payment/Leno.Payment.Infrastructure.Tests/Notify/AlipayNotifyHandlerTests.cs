using System.Security.Cryptography;
using Leno.Payment.Domain.Aggregates;
using Leno.Payment.Domain.Repositories;
using Leno.Payment.Domain.Services;
using Leno.Payment.Domain.ValueObjects;
using Leno.Payment.Infrastructure.Channels;
using Leno.Payment.Infrastructure.Channels.Alipay;
using Leno.Payment.Infrastructure.Notify;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Leno.Payment.Infrastructure.Tests.Notify;

/// <summary>
/// P1-15 测试：验证支付宝退款通知字段映射与 Handler 处理流程。
/// 根因：原 <see cref="AlipayAdapter.VerifyNotifyAsync"/> 对所有通知统一将 out_trade_no 映射到
/// <see cref="ChannelNotifyResult.OutTradeNo"/>，但退款通知中应使用 out_request_no（商户退款单号）
/// 供 Handler 查找退款单；原 <see cref="AlipayNotifyHandler.HandleRefundNotifyAsync"/> 直接读取表单字段
/// 而非使用 <see cref="ChannelNotifyResult"/>，绕过了适配器抽象。
/// 修复后：退款通知时 OutTradeNo = out_request_no，ChannelTradeNo = trade_no（退款交易号）；
/// Handler 统一使用 result 字段，与 <see cref="WeChatPayNotifyHandler"/> 模式对齐。
/// </summary>
public class AlipayNotifyHandlerTests
{
    private const string AppId = "2021000000000001";
    private const string OutTradeNo = "PAY20260722000001";
    private const string OutRefundNo = "RFD20260722000001";
    private const string PaymentTradeNo = "20260722000000000001";
    private const string RefundTradeNo = "2026072299900000000001";

    private static readonly Guid OrderId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid PaymentId = Guid.NewGuid();
    private static readonly Guid AfterSalesId = Guid.NewGuid();

    private static (string privateKey, string publicKey) GenerateKeyPair()
    {
        using var rsa = RSA.Create(2048);
        return (rsa.ExportRSAPrivateKeyPem(), rsa.ExportRSAPublicKeyPem());
    }

    private static AlipayAdapter CreateAdapter(string publicKey)
    {
        var configProviderMock = new Mock<IChannelConfigProvider>();
        configProviderMock
            .Setup(p => p.GetConfigAsync(PaymentChannel.Alipay, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelConfig
            {
                AppId = AppId,
                MchId = "2088000000000001",
                ApiKey = publicKey,
                PublicKey = publicKey,
                NotifyUrl = "https://example.com/notify/alipay",
                RefundNotifyUrl = "https://example.com/notify/alipay/refund"
            });

        var httpClient = new HttpClient();
        var clientLogger = NullLogger<AlipayClient>.Instance;
        var client = new AlipayClient(httpClient, clientLogger);
        var adapterLogger = NullLogger<AlipayAdapter>.Instance;
        return new AlipayAdapter(client, configProviderMock.Object, adapterLogger);
    }

    /// <summary>
    /// 构建支付宝退款通知表单字段（含合法 RSA2 签名）。
    /// 关键字段：out_request_no（商户退款单号）、trade_no（退款交易号）、refund_fee（退款金额）。
    /// 注意：退款通知中 out_trade_no 仍为原支付单商户单号，但修复后不应映射到 OutTradeNo。
    /// </summary>
    private static (string rawBody, Dictionary<string, string> fields) BuildRefundNotify(
        string privateKey, string refundFee = "50.00")
    {
        var fields = new Dictionary<string, string>
        {
            ["app_id"] = AppId,
            ["charset"] = "UTF-8",
            ["out_trade_no"] = OutTradeNo,
            ["out_request_no"] = OutRefundNo,
            ["trade_no"] = RefundTradeNo,
            ["refund_fee"] = refundFee,
            ["gmt_refund_pay"] = "2026-07-22 10:00:00",
            ["notify_time"] = "2026-07-22 10:00:00",
            ["notify_type"] = "trade_status_sync",
            ["notify_id"] = "notify-refund-001",
            ["sign_type"] = "RSA2"
        };
        fields["sign"] = AlipaySignatureHelper.GenerateSign(fields, privateKey);

        var rawBody = string.Join("&", fields.Select(kv =>
            $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
        return (rawBody, fields);
    }

    /// <summary>
    /// 构建支付宝支付成功通知表单字段（含合法 RSA2 签名），用于回归测试。
    /// </summary>
    private static (string rawBody, Dictionary<string, string> fields) BuildPaidNotify(
        string privateKey, string totalAmount = "100.00")
    {
        var fields = new Dictionary<string, string>
        {
            ["app_id"] = AppId,
            ["charset"] = "UTF-8",
            ["out_trade_no"] = OutTradeNo,
            ["trade_no"] = PaymentTradeNo,
            ["trade_status"] = "TRADE_SUCCESS",
            ["total_amount"] = totalAmount,
            ["gmt_payment"] = "2026-07-22 10:00:00",
            ["notify_time"] = "2026-07-22 10:00:00",
            ["notify_type"] = "trade_status_sync",
            ["notify_id"] = "notify-paid-001",
            ["sign_type"] = "RSA2"
        };
        fields["sign"] = AlipaySignatureHelper.GenerateSign(fields, privateKey);

        var rawBody = string.Join("&", fields.Select(kv =>
            $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
        return (rawBody, fields);
    }

    private static RefundOrder CreateRefundOrder(decimal amount)
    {
        // 通过工厂方法创建退款中态的退款单。工厂方法内部生成 OutRefundNo，
        // 测试中通过反射覆盖为固定值以便 Mock 仓储按 OutRefundNo 匹配。
        var refund = RefundOrder.Create(
            Guid.NewGuid(), PaymentId, OrderId, UserId, AfterSalesId,
            amount, "CNY", OutTradeNo, PaymentChannel.Alipay);

        var field = typeof(RefundOrder).GetProperty(nameof(RefundOrder.OutRefundNo))!;
        field.SetValue(refund, OutRefundNo);
        return refund;
    }

    private static AlipayNotifyHandler CreateHandler(
        AlipayAdapter adapter,
        Mock<IPaymentOrderRepository>? orderRepoMock = null,
        Mock<IRefundOrderRepository>? refundRepoMock = null,
        Mock<IUnitOfWork>? uowMock = null)
    {
        orderRepoMock ??= new Mock<IPaymentOrderRepository>();
        refundRepoMock ??= new Mock<IRefundOrderRepository>();
        uowMock ??= new Mock<IUnitOfWork>();

        return new AlipayNotifyHandler(
            adapter,
            orderRepoMock.Object,
            refundRepoMock.Object,
            uowMock.Object,
            redis: null,
            NullLogger<AlipayNotifyHandler>.Instance);
    }

    [Fact]
    public async Task VerifyNotifyAsync_RefundNotify_ShouldMapOutRequestNoToOutTradeNo()
    {
        // 安排：退款通知含 out_request_no 与 out_trade_no，修复后 OutTradeNo 应为 out_request_no
        var (privateKey, publicKey) = GenerateKeyPair();
        var adapter = CreateAdapter(publicKey);
        var (rawBody, fields) = BuildRefundNotify(privateKey);

        // 行动
        var result = await adapter.VerifyNotifyAsync(rawBody, fields);

        // 断言：OutTradeNo 为 out_request_no（商户退款单号），而非 out_trade_no（原支付单号）
        Assert.True(result.Verified);
        Assert.True(result.IsRefund);
        Assert.False(result.IsPaid);
        Assert.Equal(OutRefundNo, result.OutTradeNo);
        Assert.NotEqual(OutTradeNo, result.OutTradeNo);
    }

    [Fact]
    public async Task VerifyNotifyAsync_RefundNotify_ShouldMapTradeNoToChannelTradeNo()
    {
        // 安排：退款通知中 trade_no 为退款交易号
        var (privateKey, publicKey) = GenerateKeyPair();
        var adapter = CreateAdapter(publicKey);
        var (rawBody, fields) = BuildRefundNotify(privateKey);

        // 行动
        var result = await adapter.VerifyNotifyAsync(rawBody, fields);

        // 断言：ChannelTradeNo 为退款交易号 trade_no
        Assert.True(result.Verified);
        Assert.Equal(RefundTradeNo, result.ChannelTradeNo);
    }

    [Fact]
    public async Task VerifyNotifyAsync_RefundNotify_ShouldParseRefundFeeAsRefundAmount()
    {
        // 安排：退款通知含 refund_fee = "50.00"
        var (privateKey, publicKey) = GenerateKeyPair();
        var adapter = CreateAdapter(publicKey);
        var (rawBody, fields) = BuildRefundNotify(privateKey, refundFee: "50.00");

        // 行动
        var result = await adapter.VerifyNotifyAsync(rawBody, fields);

        // 断言：RefundAmount 解析为 50.00m
        Assert.True(result.Verified);
        Assert.Equal(50.00m, result.RefundAmount);
    }

    [Fact]
    public async Task VerifyNotifyAsync_PaymentNotify_ShouldStillMapOutTradeNoCorrectly()
    {
        // 安排：支付通知（无 out_request_no），回归测试确保支付通知映射不受影响
        var (privateKey, publicKey) = GenerateKeyPair();
        var adapter = CreateAdapter(publicKey);
        var (rawBody, fields) = BuildPaidNotify(privateKey, "100.00");

        // 行动
        var result = await adapter.VerifyNotifyAsync(rawBody, fields);

        // 断言：支付通知 OutTradeNo 仍为 out_trade_no，IsPaid = true，IsRefund = false
        Assert.True(result.Verified);
        Assert.True(result.IsPaid);
        Assert.False(result.IsRefund);
        Assert.Equal(OutTradeNo, result.OutTradeNo);
        Assert.Equal(PaymentTradeNo, result.ChannelTradeNo);
        Assert.Equal(100.00m, result.Amount);
    }

    [Fact]
    public async Task HandleAsync_RefundNotify_ShouldMarkRefundSucceededWithChannelTradeNo()
    {
        // 安排：退款通知端到端处理，验证 Handler 使用 result.OutTradeNo 查找退款单、
        // result.ChannelTradeNo 作为渠道退款单号
        var (privateKey, publicKey) = GenerateKeyPair();
        var adapter = CreateAdapter(publicKey);
        var (rawBody, fields) = BuildRefundNotify(privateKey, "50.00");

        var refund = CreateRefundOrder(50m);
        var refundRepoMock = new Mock<IRefundOrderRepository>();
        refundRepoMock
            .Setup(r => r.GetByOutRefundNoAsync(OutRefundNo, It.IsAny<CancellationToken>()))
            .ReturnsAsync(refund);

        var uowMock = new Mock<IUnitOfWork>();
        var sut = CreateHandler(adapter, refundRepoMock: refundRepoMock, uowMock: uowMock);

        // 行动
        var result = await sut.HandleAsync(rawBody, fields);

        // 断言：退款单被标记成功，ChannelRefundNo 为退款通知中的 trade_no（退款交易号）
        Assert.Equal("success", result);
        Assert.Equal(RefundStatus.Succeeded, refund.Status);
        Assert.Equal(RefundTradeNo, refund.ChannelRefundNo);
        refundRepoMock.Verify(r => r.GetByOutRefundNoAsync(OutRefundNo, It.IsAny<CancellationToken>()), Times.Once);
        refundRepoMock.Verify(r => r.UpdateAsync(refund, It.IsAny<CancellationToken>()), Times.Once);
        uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_RefundNotify_RefundNotFound_ShouldReturnFail()
    {
        // 安排：退款通知但退款单不存在（out_request_no 无法匹配）
        var (privateKey, publicKey) = GenerateKeyPair();
        var adapter = CreateAdapter(publicKey);
        var (rawBody, fields) = BuildRefundNotify(privateKey);

        var refundRepoMock = new Mock<IRefundOrderRepository>();
        refundRepoMock
            .Setup(r => r.GetByOutRefundNoAsync(OutRefundNo, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RefundOrder?)null);

        var sut = CreateHandler(adapter, refundRepoMock: refundRepoMock);

        // 行动
        var result = await sut.HandleAsync(rawBody, fields);

        // 断言：退款单不存在返回 fail
        Assert.Equal("fail", result);
        refundRepoMock.Verify(r => r.GetByOutRefundNoAsync(OutRefundNo, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_RefundNotify_AlreadySucceeded_ShouldReturnSuccessIdempotently()
    {
        // 安排：退款通知但退款单已成功（幂等跳过）
        var (privateKey, publicKey) = GenerateKeyPair();
        var adapter = CreateAdapter(publicKey);
        var (rawBody, fields) = BuildRefundNotify(privateKey);

        var refund = CreateRefundOrder(50m);
        // 通过反射置为已成功态，模拟重复回调
        refund.GetType().GetProperty(nameof(RefundOrder.Status))!
            .SetValue(refund, RefundStatus.Succeeded);

        var refundRepoMock = new Mock<IRefundOrderRepository>();
        refundRepoMock
            .Setup(r => r.GetByOutRefundNoAsync(OutRefundNo, It.IsAny<CancellationToken>()))
            .ReturnsAsync(refund);

        var uowMock = new Mock<IUnitOfWork>();
        var sut = CreateHandler(adapter, refundRepoMock: refundRepoMock, uowMock: uowMock);

        // 行动
        var result = await sut.HandleAsync(rawBody, fields);

        // 断言：幂等跳过返回 success，不调用 UpdateAsync / SaveEntitiesAsync
        Assert.Equal("success", result);
        refundRepoMock.Verify(r => r.UpdateAsync(It.IsAny<RefundOrder>(), It.IsAny<CancellationToken>()), Times.Never);
        uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
