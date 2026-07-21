using Leno.Payment.Domain.Aggregates;
using Leno.Payment.Domain.Repositories;
using Leno.Payment.Domain.Services;
using Leno.Payment.Domain.ValueObjects;
using Leno.Payment.Infrastructure.Notify;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace Leno.Payment.Infrastructure.Tests.Notify;

/// <summary>
/// P0-1 测试：验证 <see cref="WeChatPayNotifyHandler"/> 不再在验签前调用 <c>ParseXml</c>。
/// 微信 V3 回调为 JSON 格式，<c>ParseXml</c>（XML 解析）在验签前执行会导致 JSON 报文抛 <c>XmlException</c>，
/// 被外层 catch 吞掉返回 <c>FAIL</c>，所有 V3 回调无法处理。
/// 修复后：<c>ParseXml</c> 不再被调用，验签失败直接返回 <c>FAIL</c>，验签成功后使用 <see cref="ChannelNotifyResult"/> 字段。
/// </summary>
public class WeChatPayNotifyHandlerParseXmlTests
{
    private static readonly Guid OrderId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private const string OutTradeNo = "PAY20260722000001";
    private const string ChannelTradeNo = "4200000000202607220000000001";

    /// <summary>
    /// 构造 handler，使用 Mock 的 <see cref="IPaymentChannelAdapter"/> 替代真实 <c>WeChatPayAdapter</c>。
    /// 修复前提：<see cref="WeChatPayNotifyHandler"/> 构造函数需改为接收 <see cref="IPaymentChannelAdapter"/>。
    /// </summary>
    private static WeChatPayNotifyHandler CreateHandler(
        Mock<IPaymentChannelAdapter> adapterMock,
        Mock<IPaymentOrderRepository>? orderRepoMock = null,
        Mock<IRefundOrderRepository>? refundRepoMock = null,
        Mock<IUnitOfWork>? uowMock = null,
        IConnectionMultiplexer? redis = null)
    {
        orderRepoMock ??= new Mock<IPaymentOrderRepository>();
        refundRepoMock ??= new Mock<IRefundOrderRepository>();
        uowMock ??= new Mock<IUnitOfWork>();

        return new WeChatPayNotifyHandler(
            adapterMock.Object,
            orderRepoMock.Object,
            refundRepoMock.Object,
            uowMock.Object,
            redis,
            NullLogger<WeChatPayNotifyHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_VerifyFailed_ShouldReturnFail_WithoutThrowingXmlException()
    {
        // Arrange：V3 JSON 报文（非 XML），验签返回 false
        // 修复前：ParseXml(rawBody) 会因 JSON 报文抛 XmlException，被外层 catch 吞掉返回 FAIL
        // 修复后：不再调用 ParseXml，验签失败直接返回 FAIL
        var rawBody = "{\"id\":\"evt-001\",\"event_type\":\"TRANSACTION.SUCCESS\",\"resource\":{\"ciphertext\":\"abc\",\"nonce\":\"def\",\"associated_data\":\"\"}}";
        var headers = new Dictionary<string, string>
        {
            ["Wechatpay-Timestamp"] = "1234567890",
            ["Wechatpay-Nonce"] = "nonce123",
            ["Wechatpay-Signature"] = "invalid_sig",
            ["Wechatpay-Serial"] = "serial_001"
        };

        var adapterMock = new Mock<IPaymentChannelAdapter>();
        adapterMock
            .Setup(a => a.VerifyNotifyAsync(rawBody, headers, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelNotifyResult { Verified = false });

        var sut = CreateHandler(adapterMock);

        // Act：验签失败应直接返回 FAIL，不应因 ParseXml 抛 XmlException
        var result = await sut.HandleAsync(rawBody, headers);

        // Assert
        Assert.Equal("FAIL", result);
        adapterMock.Verify(a => a.VerifyNotifyAsync(rawBody, headers, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_VerifySucceeded_V3Json_ShouldProcessSuccessfully()
    {
        // Arrange：V3 JSON 报文，验签通过，ChannelNotifyResult 含 OutTradeNo 等字段
        // 修复前：ParseXml(rawBody) 会因 JSON 报文抛 XmlException，即使验签通过也无法处理
        // 修复后：不再调用 ParseXml，验签通过后使用 ChannelNotifyResult 字段处理
        var rawBody = "{\"id\":\"evt-001\",\"event_type\":\"TRANSACTION.SUCCESS\",\"resource\":{\"ciphertext\":\"abc\",\"nonce\":\"def\",\"associated_data\":\"\"}}";
        var headers = new Dictionary<string, string>
        {
            ["Wechatpay-Timestamp"] = "1234567890",
            ["Wechatpay-Nonce"] = "nonce123",
            ["Wechatpay-Signature"] = "valid_sig",
            ["Wechatpay-Serial"] = "serial_001"
        };

        var paidAt = DateTime.UtcNow;
        var adapterMock = new Mock<IPaymentChannelAdapter>();
        adapterMock
            .Setup(a => a.VerifyNotifyAsync(rawBody, headers, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelNotifyResult
            {
                Verified = true,
                OutTradeNo = OutTradeNo,
                ChannelTradeNo = ChannelTradeNo,
                IsPaid = true,
                Amount = 100m,
                PaidAt = paidAt
            });

        var order = PaymentOrder.Create(Guid.NewGuid(), OrderId, UserId, 100m, "CNY", PaymentChannel.WeChatPay);
        var orderRepoMock = new Mock<IPaymentOrderRepository>();
        orderRepoMock
            .Setup(r => r.GetByOutTradeNoAsync(OutTradeNo, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        // Redis null = 开发环境放行
        var sut = CreateHandler(adapterMock, orderRepoMock, redis: null);

        // Act：验签通过后应使用 ChannelNotifyResult 字段处理，不依赖 ParseXml
        var result = await sut.HandleAsync(rawBody, headers);

        // Assert
        Assert.Equal("SUCCESS", result);
        Assert.Equal(PaymentStatus.Paid, order.Status);
        Assert.Equal(ChannelTradeNo, order.ChannelTradeNo);
        orderRepoMock.Verify(r => r.GetByOutTradeNoAsync(OutTradeNo, It.IsAny<CancellationToken>()), Times.Once);
    }
}
