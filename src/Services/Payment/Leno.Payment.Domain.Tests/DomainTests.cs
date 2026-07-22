using Leno.Payment.Domain.Aggregates;
using Leno.Payment.Domain.Exceptions;
using Leno.Payment.Domain.ValueObjects;

namespace Leno.Payment.Domain.Tests;

public class PaymentOrderTests
{
    private static readonly Guid OrderId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public void Create_Valid_ShouldCreatePendingPayment()
    {
        var payment = PaymentOrder.Create(Guid.NewGuid(), OrderId, UserId, 100m, "CNY", PaymentChannel.WeChatPay);

        payment.Status.Should().Be(PaymentStatus.Pending);
        payment.OrderId.Should().Be(OrderId);
        payment.UserId.Should().Be(UserId);
        payment.Amount.Should().Be(100m);
        payment.Currency.Should().Be("CNY");
        payment.Channel.Should().Be(PaymentChannel.WeChatPay);
        payment.OutTradeNo.Should().StartWith("PAY");
        payment.ExpireAt.Should().BeAfter(DateTime.UtcNow.AddHours(1));
    }

    [Fact]
    public void Create_DefaultCurrency_ShouldSetCny()
    {
        var payment = PaymentOrder.Create(Guid.NewGuid(), OrderId, UserId, 100m, "", PaymentChannel.Alipay);

        payment.Currency.Should().Be("CNY");
    }

    [Fact]
    public void Create_DefaultTradeType_ShouldBeNative()
    {
        // P2-19：未显式传入 tradeType 时默认 Native，保持向后兼容
        var payment = PaymentOrder.Create(Guid.NewGuid(), OrderId, UserId, 100m, "CNY", PaymentChannel.WeChatPay);

        payment.TradeType.Should().Be(TradeType.Native);
    }

    [Fact]
    public void Create_ExplicitTradeType_ShouldAssignFromParameter()
    {
        // P2-19：显式传入 H5/JsApi/App 时应正确赋值到聚合根
        var h5Payment = PaymentOrder.Create(Guid.NewGuid(), OrderId, UserId, 100m, "CNY", PaymentChannel.WeChatPay, TradeType.H5);
        var jsApiPayment = PaymentOrder.Create(Guid.NewGuid(), OrderId, UserId, 100m, "CNY", PaymentChannel.WeChatPay, TradeType.JsApi);
        var appPayment = PaymentOrder.Create(Guid.NewGuid(), OrderId, UserId, 100m, "CNY", PaymentChannel.WeChatPay, TradeType.App);

        h5Payment.TradeType.Should().Be(TradeType.H5);
        jsApiPayment.TradeType.Should().Be(TradeType.JsApi);
        appPayment.TradeType.Should().Be(TradeType.App);
    }

    [Fact]
    public void Create_EmptyPaymentId_ShouldThrowException()
    {
        var act = () => PaymentOrder.Create(Guid.Empty, OrderId, UserId, 100m, "CNY", PaymentChannel.WeChatPay);

        act.Should().Throw<PaymentDomainException>().WithMessage("*PaymentId*");
    }

    [Fact]
    public void Create_EmptyOrderId_ShouldThrowException()
    {
        var act = () => PaymentOrder.Create(Guid.NewGuid(), Guid.Empty, UserId, 100m, "CNY", PaymentChannel.WeChatPay);

        act.Should().Throw<PaymentDomainException>().WithMessage("*OrderId*");
    }

    [Fact]
    public void Create_EmptyUserId_ShouldThrowException()
    {
        var act = () => PaymentOrder.Create(Guid.NewGuid(), OrderId, Guid.Empty, 100m, "CNY", PaymentChannel.WeChatPay);

        act.Should().Throw<PaymentDomainException>().WithMessage("*UserId*");
    }

    [Fact]
    public void Create_ZeroAmount_ShouldThrowException()
    {
        var act = () => PaymentOrder.Create(Guid.NewGuid(), OrderId, UserId, 0m, "CNY", PaymentChannel.WeChatPay);

        act.Should().Throw<PaymentDomainException>().WithMessage("*金额*");
    }

    [Fact]
    public void MarkChannelOrdered_Valid_ShouldTransitionAndRecordParams()
    {
        var payment = CreatePayment();

        payment.MarkChannelOrdered("TRADE001", "prepay_123", "https://qr.example.com", null);

        payment.Status.Should().Be(PaymentStatus.ChannelOrdered);
        payment.ChannelTradeNo.Should().Be("TRADE001");
        payment.PrepayId.Should().Be("prepay_123");
        payment.CodeUrl.Should().Be("https://qr.example.com");
    }

    [Fact]
    public void MarkChannelOrdered_NotPending_ShouldThrowException()
    {
        var payment = CreatePayment();
        payment.MarkChannelOrdered("TRADE001", null, null, null);

        var act = () => payment.MarkChannelOrdered("TRADE002", null, null, null);

        act.Should().Throw<PaymentDomainException>().WithMessage("*状态*");
    }

    [Fact]
    public void MarkChannelOrdered_EmptyTradeNo_ShouldThrowException()
    {
        var payment = CreatePayment();

        var act = () => payment.MarkChannelOrdered("", null, null, null);

        act.Should().Throw<PaymentDomainException>().WithMessage("*交易号*");
    }

    [Fact]
    public void MarkSucceeded_FromPending_ShouldTransitionToPaid()
    {
        var payment = CreatePayment();
        var paidAt = DateTime.UtcNow;

        payment.MarkSucceeded("TRADE001", 100m, paidAt);

        payment.Status.Should().Be(PaymentStatus.Paid);
        payment.ChannelTradeNo.Should().Be("TRADE001");
        payment.PaidAt.Should().Be(paidAt);
    }

    [Fact]
    public void MarkSucceeded_FromChannelOrdered_ShouldTransitionToPaid()
    {
        var payment = CreatePayment();
        payment.MarkChannelOrdered("TRADE001", null, null, null);
        var paidAt = DateTime.UtcNow;

        payment.MarkSucceeded("TRADE001", 100m, paidAt);

        payment.Status.Should().Be(PaymentStatus.Paid);
    }

    [Fact]
    public void MarkSucceeded_AlreadyPaid_ShouldThrowException()
    {
        var payment = CreatePayment();
        payment.MarkSucceeded("TRADE001", 100m, DateTime.UtcNow);

        var act = () => payment.MarkSucceeded("TRADE002", 100m, DateTime.UtcNow);

        act.Should().Throw<PaymentDomainException>().WithMessage("*状态*");
    }

    [Fact]
    public void MarkSucceeded_EmptyTradeNo_ShouldThrowException()
    {
        var payment = CreatePayment();

        var act = () => payment.MarkSucceeded("", 100m, DateTime.UtcNow);

        act.Should().Throw<PaymentDomainException>().WithMessage("*交易号*");
    }

    [Fact]
    public void MarkSucceeded_AmountMatch_WithDecimalPrecision_ShouldTransitionToPaid()
    {
        // 渠道金额按字符串解析（如 "100.00"），与本地 100m 数值相等应通过
        var payment = CreatePayment();

        payment.MarkSucceeded("TRADE001", 100.00m, DateTime.UtcNow);

        payment.Status.Should().Be(PaymentStatus.Paid);
    }

    [Fact]
    public void MarkSucceeded_AmountMismatch_ShouldThrowException()
    {
        var payment = CreatePayment();

        var act = () => payment.MarkSucceeded("TRADE001", 0.01m, DateTime.UtcNow);

        act.Should().Throw<PaymentDomainException>()
            .Where(ex => ex.ErrorCode == "PAYMENT_AMOUNT_MISMATCH"
                && ex.Message.Contains("金额")
                && ex.Message.Contains("0.01"));
        payment.Status.Should().Be(PaymentStatus.Pending);
    }

    [Fact]
    public void MarkSucceeded_LowerAmount_ShouldThrowExceptionAndKeepStatus()
    {
        var payment = CreatePayment();

        var act = () => payment.MarkSucceeded("TRADE001", 99.99m, DateTime.UtcNow);

        act.Should().Throw<PaymentDomainException>();
        payment.Status.Should().Be(PaymentStatus.Pending);
        payment.ChannelTradeNo.Should().BeNull();
        payment.PaidAt.Should().BeNull();
    }

    [Fact]
    public void MarkFailed_FromPending_ShouldTransitionToFailed()
    {
        var payment = CreatePayment();

        payment.MarkFailed("余额不足");

        payment.Status.Should().Be(PaymentStatus.Failed);
        payment.FailReason.Should().Be("余额不足");
    }

    [Fact]
    public void MarkFailed_FromChannelOrdered_ShouldTransitionToFailed()
    {
        var payment = CreatePayment();
        payment.MarkChannelOrdered("TRADE001", null, null, null);

        payment.MarkFailed("用户取消");

        payment.Status.Should().Be(PaymentStatus.Failed);
    }

    [Fact]
    public void MarkFailed_AlreadyPaid_ShouldThrowException()
    {
        var payment = CreatePayment();
        payment.MarkSucceeded("TRADE001", 100m, DateTime.UtcNow);

        var act = () => payment.MarkFailed("reason");

        act.Should().Throw<PaymentDomainException>().WithMessage("*状态*");
    }

    [Fact]
    public void MarkClosed_FromPending_ShouldTransitionToClosed()
    {
        var payment = CreatePayment();

        payment.MarkClosed("超时未支付");

        payment.Status.Should().Be(PaymentStatus.Closed);
        payment.FailReason.Should().Be("超时未支付");
    }

    [Fact]
    public void MarkClosed_FromFailed_ShouldTransitionToClosed()
    {
        var payment = CreatePayment();
        payment.MarkFailed("失败");

        payment.MarkClosed("手动关闭");

        payment.Status.Should().Be(PaymentStatus.Closed);
    }

    [Fact]
    public void MarkClosed_AlreadyPaid_ShouldThrowException()
    {
        var payment = CreatePayment();
        payment.MarkSucceeded("TRADE001", 100m, DateTime.UtcNow);

        var act = () => payment.MarkClosed("原因");

        act.Should().Throw<PaymentDomainException>().WithMessage("*状态*");
    }

    [Fact]
    public void Create_Concurrent10000_ShouldHaveNoOutTradeNoCollision()
    {
        // P2-16：原时间戳+6位随机数在同秒内碰撞概率 1/900000，改为时间戳+GUID 后应无碰撞。
        const int count = 10000;
        var results = new string[count];
        Parallel.For(0, count, i =>
        {
            var payment = PaymentOrder.Create(Guid.NewGuid(), OrderId, UserId, 100m, "CNY", PaymentChannel.WeChatPay);
            results[i] = payment.OutTradeNo;
        });

        var distinctCount = results.Distinct().Count();
        distinctCount.Should().Be(count, "10000 次并发生成 OutTradeNo 不应有碰撞");
    }

    [Fact]
    public void Create_OutTradeNo_ShouldStartWithPayAndFitWithin64Chars()
    {
        // P2-16：验证 OutTradeNo 格式：PAY 前缀 + 14 位时间戳 + 32 位 GUID(N) = 49 字符，不超过 MaxLength=64
        var payment = PaymentOrder.Create(Guid.NewGuid(), OrderId, UserId, 100m, "CNY", PaymentChannel.WeChatPay);

        payment.OutTradeNo.Should().StartWith("PAY");
        payment.OutTradeNo.Length.Should().BeLessThanOrEqualTo(64);
        payment.OutTradeNo.Length.Should().Be(3 + 14 + 32, "PAY(3) + 时间戳(14) + GUID.N(32) = 49");
    }

    private static PaymentOrder CreatePayment()
    {
        return PaymentOrder.Create(Guid.NewGuid(), OrderId, UserId, 100m, "CNY", PaymentChannel.WeChatPay);
    }
}

public class RefundOrderTests
{
    private static readonly Guid PaymentId = Guid.NewGuid();
    private static readonly Guid OrderId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid AfterSalesId = Guid.NewGuid();

    [Fact]
    public void Create_Valid_ShouldCreateRefunding()
    {
        var refund = RefundOrder.Create(
            Guid.NewGuid(), PaymentId, OrderId, UserId, AfterSalesId,
            50m, "CNY", "PAY20260701000001", PaymentChannel.WeChatPay);

        refund.Status.Should().Be(RefundStatus.Refunding);
        refund.RefundAmount.Should().Be(50m);
        refund.OutTradeNo.Should().Be("PAY20260701000001");
        refund.OutRefundNo.Should().StartWith("RFD");
    }

    [Fact]
    public void Create_EmptyRefundId_ShouldThrowException()
    {
        var act = () => RefundOrder.Create(
            Guid.Empty, PaymentId, OrderId, UserId, AfterSalesId,
            50m, "CNY", "PAY20260701000001", PaymentChannel.WeChatPay);

        act.Should().Throw<PaymentDomainException>().WithMessage("*RefundId*");
    }

    [Fact]
    public void Create_EmptyPaymentId_ShouldThrowException()
    {
        var act = () => RefundOrder.Create(
            Guid.NewGuid(), Guid.Empty, OrderId, UserId, AfterSalesId,
            50m, "CNY", "PAY20260701000001", PaymentChannel.WeChatPay);

        act.Should().Throw<PaymentDomainException>().WithMessage("*PaymentId*");
    }

    [Fact]
    public void Create_EmptyOutTradeNo_ShouldThrowException()
    {
        var act = () => RefundOrder.Create(
            Guid.NewGuid(), PaymentId, OrderId, UserId, AfterSalesId,
            50m, "CNY", "", PaymentChannel.WeChatPay);

        act.Should().Throw<PaymentDomainException>().WithMessage("*商户单号*");
    }

    [Fact]
    public void Create_ZeroAmount_ShouldThrowException()
    {
        var act = () => RefundOrder.Create(
            Guid.NewGuid(), PaymentId, OrderId, UserId, AfterSalesId,
            0m, "CNY", "PAY20260701000001", PaymentChannel.WeChatPay);

        act.Should().Throw<PaymentDomainException>().WithMessage("*金额*");
    }

    [Fact]
    public void MarkSucceeded_Valid_ShouldTransitionToSucceeded()
    {
        var refund = CreateRefund();
        var refundedAt = DateTime.UtcNow;

        refund.MarkSucceeded("REFUND001", refundedAt);

        refund.Status.Should().Be(RefundStatus.Succeeded);
        refund.ChannelRefundNo.Should().Be("REFUND001");
        refund.RefundedAt.Should().Be(refundedAt);
    }

    [Fact]
    public void MarkSucceeded_NotRefunding_ShouldThrowException()
    {
        var refund = CreateRefund();
        refund.MarkSucceeded("REFUND001", DateTime.UtcNow);

        var act = () => refund.MarkSucceeded("REFUND002", DateTime.UtcNow);

        act.Should().Throw<PaymentDomainException>().WithMessage("*状态*");
    }

    [Fact]
    public void MarkSucceeded_EmptyChannelRefundNo_ShouldThrowException()
    {
        var refund = CreateRefund();

        var act = () => refund.MarkSucceeded("", DateTime.UtcNow);

        act.Should().Throw<PaymentDomainException>().WithMessage("*退款单号*");
    }

    [Fact]
    public void MarkFailed_Valid_ShouldTransitionToFailed()
    {
        var refund = CreateRefund();

        refund.MarkFailed("账户异常");

        refund.Status.Should().Be(RefundStatus.Failed);
        refund.FailReason.Should().Be("账户异常");
    }

    [Fact]
    public void MarkFailed_NotRefunding_ShouldThrowException()
    {
        var refund = CreateRefund();
        refund.MarkFailed("失败");

        var act = () => refund.MarkFailed("再次失败");

        act.Should().Throw<PaymentDomainException>().WithMessage("*状态*");
    }

    [Fact]
    public void Create_Concurrent10000_ShouldHaveNoOutRefundNoCollision()
    {
        // P2-16：原时间戳+6位随机数在同秒内碰撞概率 1/900000，改为时间戳+GUID 后应无碰撞。
        const int count = 10000;
        var results = new string[count];
        Parallel.For(0, count, i =>
        {
            var refund = RefundOrder.Create(
                Guid.NewGuid(), PaymentId, OrderId, UserId, AfterSalesId,
                50m, "CNY", "PAY20260701000001", PaymentChannel.WeChatPay);
            results[i] = refund.OutRefundNo;
        });

        var distinctCount = results.Distinct().Count();
        distinctCount.Should().Be(count, "10000 次并发生成 OutRefundNo 不应有碰撞");
    }

    [Fact]
    public void Create_OutRefundNo_ShouldStartWithRfdAndFitWithin64Chars()
    {
        // P2-16：验证 OutRefundNo 格式：RFD 前缀 + 14 位时间戳 + 32 位 GUID(N) = 49 字符，不超过 MaxLength=64
        var refund = RefundOrder.Create(
            Guid.NewGuid(), PaymentId, OrderId, UserId, AfterSalesId,
            50m, "CNY", "PAY20260701000001", PaymentChannel.WeChatPay);

        refund.OutRefundNo.Should().StartWith("RFD");
        refund.OutRefundNo.Length.Should().BeLessThanOrEqualTo(64);
        refund.OutRefundNo.Length.Should().Be(3 + 14 + 32, "RFD(3) + 时间戳(14) + GUID.N(32) = 49");
    }

    private static RefundOrder CreateRefund()
    {
        return RefundOrder.Create(
            Guid.NewGuid(), PaymentId, OrderId, UserId, AfterSalesId,
            50m, "CNY", "PAY20260701000001", PaymentChannel.WeChatPay);
    }
}