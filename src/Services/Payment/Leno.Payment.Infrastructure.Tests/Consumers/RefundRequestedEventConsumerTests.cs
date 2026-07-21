using System.Reflection;
using Leno.Infrastructure.Abstractions;
using Leno.Infrastructure.EventBus;
using Leno.Payment.Domain.Aggregates;
using Leno.Payment.Domain.Repositories;
using Leno.Payment.Domain.Services;
using Leno.Payment.Domain.ValueObjects;
using Leno.Payment.Infrastructure.Channels;
using Leno.Payment.Infrastructure.Consumers;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Leno.Payment.Infrastructure.Tests.Consumers;

/// <summary>
/// P1-10 测试：验证 RefundRequestedEventConsumer 校验原支付单状态为 Paid。
/// 根因：原实现获取 originalPayment 后仅检查 null，未校验 Status == Paid。
/// 若原支付单处于 Pending/ChannelOrdered/Failed/Closed 时发起退款，渠道侧会拒绝，
/// 但系统已创建退款单，状态不一致。修复后：非 Paid 态抛 InvalidOperationException，
/// 不创建退款单、不调渠道。
/// </summary>
public class RefundRequestedEventConsumerTests
{
    private static readonly Guid OrderId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid AfterSalesId = Guid.NewGuid();
    private static readonly Guid PaymentId = Guid.NewGuid();
    private static readonly Guid RefundId = Guid.NewGuid();
    private const decimal Amount = 100m;
    private const decimal RefundAmount = 50m;

    private static RefundRequestedIntegrationEvent CreateEvent()
    {
        return new RefundRequestedIntegrationEvent
        {
            EventId = Guid.NewGuid(),
            RefundId = RefundId,
            OrderId = OrderId,
            UserId = UserId,
            AfterSalesId = AfterSalesId,
            PaymentId = PaymentId,
            RefundAmount = RefundAmount,
            Currency = "CNY",
            Channel = "WeChatPay"
        };
    }

    /// <summary>
    /// 构造消费者，使用 Mock 的 <see cref="IPaymentChannelFactory"/> 抽象。
    /// </summary>
    private static RefundRequestedEventConsumer CreateConsumer(
        Mock<IRefundOrderRepository> refundRepoMock,
        Mock<IPaymentOrderRepository> paymentRepoMock,
        Mock<IUnitOfWork> uowMock,
        Mock<IPaymentChannelFactory> factoryMock,
        Mock<IIdempotencyStore> idempotencyMock)
    {
        return new RefundRequestedEventConsumer(
            refundRepoMock.Object,
            paymentRepoMock.Object,
            uowMock.Object,
            factoryMock.Object,
            NullLogger<RefundRequestedEventConsumer>.Instance,
            idempotencyMock.Object);
    }

    /// <summary>
    /// 通过反射调用受保护的 <see cref="IntegrationEventConsumerBase{T}.HandleAsync"/>。
    /// </summary>
    private static async Task InvokeHandleAsync(RefundRequestedEventConsumer consumer, RefundRequestedIntegrationEvent evt)
    {
        var handleMethod = typeof(RefundRequestedEventConsumer)
            .GetMethod("HandleAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(handleMethod);
        await (Task)handleMethod!.Invoke(consumer, [evt, CancellationToken.None])!;
    }

    /// <summary>
    /// 构造指定状态的 PaymentOrder。Pending 态直接由 Create 工厂得到；
    /// 其他状态先经 MarkChannelOrdered 进入 ChannelOrdered，再按需 MarkSucceeded / MarkFailed / MarkClosed。
    /// </summary>
    private static PaymentOrder CreatePaymentWithStatus(PaymentStatus targetStatus)
    {
        var payment = PaymentOrder.Create(
            Guid.NewGuid(), OrderId, UserId, Amount, "CNY", PaymentChannel.WeChatPay);

        if (targetStatus == PaymentStatus.Pending)
        {
            return payment;
        }

        payment.MarkChannelOrdered("WX_TXN_TEST_001", null, null, null);

        if (targetStatus == PaymentStatus.ChannelOrdered)
        {
            return payment;
        }

        switch (targetStatus)
        {
            case PaymentStatus.Paid:
                payment.MarkSucceeded("WX_TXN_TEST_001", Amount, DateTime.UtcNow);
                break;
            case PaymentStatus.Failed:
                payment.MarkFailed("测试失败");
                break;
            case PaymentStatus.Closed:
                payment.MarkClosed("测试关闭");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(targetStatus), targetStatus, "未支持的测试状态");
        }

        return payment;
    }

    [Fact]
    public async Task HandleAsync_WhenOriginalPaymentIsPending_ShouldThrow_AndNotCreateRefundOrder()
    {
        // 安排：原支付单为 Pending 态
        var refundRepoMock = new Mock<IRefundOrderRepository>();
        refundRepoMock
            .Setup(r => r.GetByIdAsync(RefundId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RefundOrder?)null);

        var paymentRepoMock = new Mock<IPaymentOrderRepository>();
        paymentRepoMock
            .Setup(r => r.GetByIdAsync(PaymentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreatePaymentWithStatus(PaymentStatus.Pending));

        var uowMock = new Mock<IUnitOfWork>();
        var adapterMock = new Mock<IPaymentChannelAdapter>();
        var factoryMock = new Mock<IPaymentChannelFactory>();
        factoryMock.Setup(f => f.GetAdapter(PaymentChannel.WeChatPay)).Returns(adapterMock.Object);
        var idempotencyMock = new Mock<IIdempotencyStore>();

        var sut = CreateConsumer(refundRepoMock, paymentRepoMock, uowMock, factoryMock, idempotencyMock);

        // 行动
        var ex = await Record.ExceptionAsync(() => InvokeHandleAsync(sut, CreateEvent()));

        // 断言：应抛 InvalidOperationException，不创建退款单、不调渠道、不保存
        Assert.NotNull(ex);
        Assert.IsType<InvalidOperationException>(ex);
        Assert.Contains("非已支付", ex!.Message);

        refundRepoMock.Verify(r => r.AddAsync(It.IsAny<RefundOrder>(), It.IsAny<CancellationToken>()), Times.Never);
        refundRepoMock.Verify(r => r.UpdateAsync(It.IsAny<RefundOrder>(), It.IsAny<CancellationToken>()), Times.Never);
        adapterMock.Verify(a => a.CreateRefundAsync(It.IsAny<RefundOrder>(), It.IsAny<CancellationToken>()), Times.Never);
        uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenOriginalPaymentIsChannelOrdered_ShouldThrow_AndNotCreateRefundOrder()
    {
        // 安排：原支付单为 ChannelOrdered 态（已下渠道单但未支付）
        var refundRepoMock = new Mock<IRefundOrderRepository>();
        refundRepoMock
            .Setup(r => r.GetByIdAsync(RefundId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RefundOrder?)null);

        var paymentRepoMock = new Mock<IPaymentOrderRepository>();
        paymentRepoMock
            .Setup(r => r.GetByIdAsync(PaymentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreatePaymentWithStatus(PaymentStatus.ChannelOrdered));

        var uowMock = new Mock<IUnitOfWork>();
        var adapterMock = new Mock<IPaymentChannelAdapter>();
        var factoryMock = new Mock<IPaymentChannelFactory>();
        factoryMock.Setup(f => f.GetAdapter(PaymentChannel.WeChatPay)).Returns(adapterMock.Object);
        var idempotencyMock = new Mock<IIdempotencyStore>();

        var sut = CreateConsumer(refundRepoMock, paymentRepoMock, uowMock, factoryMock, idempotencyMock);

        // 行动
        var ex = await Record.ExceptionAsync(() => InvokeHandleAsync(sut, CreateEvent()));

        // 断言
        Assert.NotNull(ex);
        Assert.IsType<InvalidOperationException>(ex);
        refundRepoMock.Verify(r => r.AddAsync(It.IsAny<RefundOrder>(), It.IsAny<CancellationToken>()), Times.Never);
        adapterMock.Verify(a => a.CreateRefundAsync(It.IsAny<RefundOrder>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenOriginalPaymentIsPaid_ShouldCreateRefundOrder_AndCallChannel()
    {
        // 安排：原支付单为 Paid 态，应正常处理
        var refundRepoMock = new Mock<IRefundOrderRepository>();
        refundRepoMock
            .Setup(r => r.GetByIdAsync(RefundId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RefundOrder?)null);
        refundRepoMock
            .Setup(r => r.AddAsync(It.IsAny<RefundOrder>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var paymentRepoMock = new Mock<IPaymentOrderRepository>();
        paymentRepoMock
            .Setup(r => r.GetByIdAsync(PaymentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreatePaymentWithStatus(PaymentStatus.Paid));

        var uowMock = new Mock<IUnitOfWork>();
        uowMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var adapterMock = new Mock<IPaymentChannelAdapter>();
        adapterMock
            .Setup(a => a.CreateRefundAsync(It.IsAny<RefundOrder>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelRefundResult { Succeeded = true, ChannelRefundNo = "WX_REFUND_001" });

        var factoryMock = new Mock<IPaymentChannelFactory>();
        factoryMock.Setup(f => f.GetAdapter(PaymentChannel.WeChatPay)).Returns(adapterMock.Object);
        var idempotencyMock = new Mock<IIdempotencyStore>();

        var sut = CreateConsumer(refundRepoMock, paymentRepoMock, uowMock, factoryMock, idempotencyMock);

        // 行动：不应抛异常
        var ex = await Record.ExceptionAsync(() => InvokeHandleAsync(sut, CreateEvent()));

        // 断言：创建退款单 + 调用渠道 + 保存
        Assert.Null(ex);
        refundRepoMock.Verify(r => r.AddAsync(It.IsAny<RefundOrder>(), It.IsAny<CancellationToken>()), Times.Once);
        adapterMock.Verify(a => a.CreateRefundAsync(It.IsAny<RefundOrder>(), It.IsAny<CancellationToken>()), Times.Once);
        uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenOriginalPaymentIsClosed_ShouldThrow_AndNotCreateRefundOrder()
    {
        // 安排：原支付单为 Closed 态
        var refundRepoMock = new Mock<IRefundOrderRepository>();
        refundRepoMock
            .Setup(r => r.GetByIdAsync(RefundId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RefundOrder?)null);

        var paymentRepoMock = new Mock<IPaymentOrderRepository>();
        paymentRepoMock
            .Setup(r => r.GetByIdAsync(PaymentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreatePaymentWithStatus(PaymentStatus.Closed));

        var uowMock = new Mock<IUnitOfWork>();
        var adapterMock = new Mock<IPaymentChannelAdapter>();
        var factoryMock = new Mock<IPaymentChannelFactory>();
        factoryMock.Setup(f => f.GetAdapter(PaymentChannel.WeChatPay)).Returns(adapterMock.Object);
        var idempotencyMock = new Mock<IIdempotencyStore>();

        var sut = CreateConsumer(refundRepoMock, paymentRepoMock, uowMock, factoryMock, idempotencyMock);

        // 行动
        var ex = await Record.ExceptionAsync(() => InvokeHandleAsync(sut, CreateEvent()));

        // 断言
        Assert.NotNull(ex);
        Assert.IsType<InvalidOperationException>(ex);
        refundRepoMock.Verify(r => r.AddAsync(It.IsAny<RefundOrder>(), It.IsAny<CancellationToken>()), Times.Never);
        adapterMock.Verify(a => a.CreateRefundAsync(It.IsAny<RefundOrder>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
