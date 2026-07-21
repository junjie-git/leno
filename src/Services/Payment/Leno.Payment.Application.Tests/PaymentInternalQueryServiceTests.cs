using Leno.Payment.Application.Services;
using Leno.Payment.Domain.Aggregates;
using Leno.Payment.Domain.Repositories;
using Leno.Payment.Domain.ValueObjects;
using Moq;
using Xunit;

namespace Leno.Payment.Application.Tests;

/// <summary>
/// P1-12 测试：验证 PaymentInternalQueryService 填充 PaymentInfoResultDto 的所有字段，
/// 包括 Amount/Currency/PaidAt/TradeNo/RefundedAmount，供 gRPC/HTTP 跨域查询使用完整数据。
/// 根因：原 DTO 仅含 PaymentId/Channel/OrderId/Status，导致 gRPC PaymentInfo.AmountCents=0
/// 且 PaidAt 为空字符串，跨域调用方无法做金额校验或时间判断。
/// </summary>
public class PaymentInternalQueryServiceTests
{
    private static readonly Guid OrderId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static PaymentOrder CreatePaidOrder(decimal amount, string channelTradeNo, DateTime paidAt)
    {
        var order = PaymentOrder.Create(
            Guid.NewGuid(), OrderId, UserId, amount, "CNY", PaymentChannel.WeChatPay);
        order.MarkChannelOrdered(channelTradeNo, null, null, null);
        order.MarkSucceeded(channelTradeNo, amount, paidAt);
        return order;
    }

    private static PaymentInternalQueryService CreateService(
        Mock<IPaymentOrderRepository> paymentRepoMock,
        Mock<IRefundOrderRepository> refundRepoMock)
    {
        return new PaymentInternalQueryService(paymentRepoMock.Object, refundRepoMock.Object);
    }

    [Fact]
    public async Task GetPaymentInfoByOrderIdAsync_WhenPaid_ShouldPopulateAmountAndPaidAtAndTradeNo()
    {
        // 安排：已支付单 100 元，第三方交易号 WX_TXN_001，支付时间为 UTC 固定点
        var paidAt = new DateTime(2026, 7, 22, 10, 0, 0, DateTimeKind.Utc);
        var channelTradeNo = "WX_TXN_001";
        var payment = CreatePaidOrder(100m, channelTradeNo, paidAt);

        var paymentRepoMock = new Mock<IPaymentOrderRepository>();
        paymentRepoMock
            .Setup(r => r.GetByOrderIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);

        var refundRepoMock = new Mock<IRefundOrderRepository>();
        refundRepoMock
            .Setup(r => r.GetSuccessfulRefundsByPaymentIdAsync(payment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RefundOrder>());

        var sut = CreateService(paymentRepoMock, refundRepoMock);

        // 行动
        var dto = await sut.GetPaymentInfoByOrderIdAsync(OrderId, CancellationToken.None);

        // 断言：DTO 字段被完整填充
        Assert.NotNull(dto);
        dto!.Amount.Should().Be(100m);
        dto.Currency.Should().Be("CNY");
        dto.PaidAt.Should().Be(paidAt);
        dto.TradeNo.Should().Be(channelTradeNo);
        dto.RefundedAmount.Should().Be(0m);
        dto.Channel.Should().Be((int)PaymentChannel.WeChatPay);
        dto.Status.Should().Be((int)PaymentStatus.Paid);
    }

    [Fact]
    public async Task GetPaymentInfoByOrderIdAsync_WhenRefundsExist_ShouldSumRefundedAmount()
    {
        // 安排：已支付单 200 元，已成功退款 50+30=80 元
        var paidAt = new DateTime(2026, 7, 22, 10, 0, 0, DateTimeKind.Utc);
        var channelTradeNo = "WX_TXN_002";
        var payment = CreatePaidOrder(200m, channelTradeNo, paidAt);

        // 构造两笔已成功退款单
        var refund1 = CreateSucceededRefund(payment.Id, 50m);
        var refund2 = CreateSucceededRefund(payment.Id, 30m);

        var paymentRepoMock = new Mock<IPaymentOrderRepository>();
        paymentRepoMock
            .Setup(r => r.GetByOrderIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);

        var refundRepoMock = new Mock<IRefundOrderRepository>();
        refundRepoMock
            .Setup(r => r.GetSuccessfulRefundsByPaymentIdAsync(payment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RefundOrder> { refund1, refund2 });

        var sut = CreateService(paymentRepoMock, refundRepoMock);

        // 行动
        var dto = await sut.GetPaymentInfoByOrderIdAsync(OrderId, CancellationToken.None);

        // 断言：已退款金额汇总为 80 元
        Assert.NotNull(dto);
        dto!.RefundedAmount.Should().Be(80m);
        dto.Amount.Should().Be(200m);
    }

    [Fact]
    public async Task GetPaymentInfoByOrderIdAsync_WhenPaymentNotFound_ShouldReturnNull()
    {
        // 安排
        var paymentRepoMock = new Mock<IPaymentOrderRepository>();
        paymentRepoMock
            .Setup(r => r.GetByOrderIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentOrder?)null);

        var refundRepoMock = new Mock<IRefundOrderRepository>();

        var sut = CreateService(paymentRepoMock, refundRepoMock);

        // 行动
        var dto = await sut.GetPaymentInfoByOrderIdAsync(OrderId, CancellationToken.None);

        // 断言
        dto.Should().BeNull();
    }

    [Fact]
    public async Task GetPaymentInfoByOrderIdAsync_WhenPending_ShouldHaveNullPaidAtAndTradeNo()
    {
        // 安排：Pending 态支付单（未支付），PaidAt/ChannelTradeNo 应为 null
        var payment = PaymentOrder.Create(
            Guid.NewGuid(), OrderId, UserId, 150m, "CNY", PaymentChannel.Alipay);

        var paymentRepoMock = new Mock<IPaymentOrderRepository>();
        paymentRepoMock
            .Setup(r => r.GetByOrderIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);

        var refundRepoMock = new Mock<IRefundOrderRepository>();
        refundRepoMock
            .Setup(r => r.GetSuccessfulRefundsByPaymentIdAsync(payment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RefundOrder>());

        var sut = CreateService(paymentRepoMock, refundRepoMock);

        // 行动
        var dto = await sut.GetPaymentInfoByOrderIdAsync(OrderId, CancellationToken.None);

        // 断言：未支付单 PaidAt/TradeNo 为 null，Amount 仍填充
        Assert.NotNull(dto);
        dto!.Amount.Should().Be(150m);
        dto.PaidAt.Should().BeNull();
        dto.TradeNo.Should().BeNull();
        dto.RefundedAmount.Should().Be(0m);
        dto.Channel.Should().Be((int)PaymentChannel.Alipay);
        dto.Status.Should().Be((int)PaymentStatus.Pending);
    }

    private static RefundOrder CreateSucceededRefund(Guid paymentId, decimal refundAmount)
    {
        var refund = RefundOrder.Create(
            Guid.NewGuid(),
            paymentId,
            OrderId,
            UserId,
            Guid.NewGuid(),
            refundAmount,
            "CNY",
            "PAY_TEST_OUT_TRADE_NO",
            PaymentChannel.WeChatPay);
        // 通过反射设置状态为 Succeeded 以模拟已成功退款
        typeof(RefundOrder).GetProperty("Status")!.SetValue(refund, RefundStatus.Succeeded);
        typeof(RefundOrder).GetProperty("RefundedAt")!.SetValue(refund, DateTime.UtcNow);
        return refund;
    }
}
