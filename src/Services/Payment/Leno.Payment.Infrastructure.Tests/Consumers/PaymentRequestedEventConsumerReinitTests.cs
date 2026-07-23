using System.Reflection;
using Leno.Infrastructure.Abstractions;
using Leno.Infrastructure.EventBus;
using Leno.Payment.Domain.Aggregates;
using Leno.Payment.Domain.Exceptions;
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
/// P1-4 测试：验证 PaymentRequestedEventConsumer 在订单已存在支付单时按状态分流的重新发起逻辑。
/// 根因：原幂等检查对任何已存在支付单一律跳过，导致支付单卡在 Pending（渠道下单未完成）或
/// Failed/Closed 终态时用户无法重新发起支付。修复后按状态分流：
/// <list type="bullet">
/// <item>Paid：抛 <see cref="PaymentAlreadySucceededException"/> 拒绝重复发起。</item>
/// <item>ChannelOrdered 且链接生效：幂等跳过，复用现有支付单。</item>
/// <item>Pending（卡死）/ChannelOrdered 已过期：MarkFailed 回收旧单，创建新支付单。</item>
/// <item>Failed/Closed：终态，直接创建新支付单。</item>
/// <item>不存在：创建新支付单。</item>
/// </list>
/// </summary>
public class PaymentRequestedEventConsumerReinitTests
{
    private static readonly Guid OrderId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private const decimal Amount = 100m;

    private static PaymentRequestedIntegrationEvent CreateEvent()
    {
        return new PaymentRequestedIntegrationEvent
        {
            EventId = Guid.NewGuid(),
            OrderId = OrderId,
            UserId = UserId,
            Amount = Amount,
            Currency = "CNY",
            Channel = "WeChatPay"
        };
    }

    private static PaymentRequestedEventConsumer CreateConsumer(
        Mock<IPaymentOrderRepository> repoMock,
        Mock<IUnitOfWork> uowMock,
        Mock<IPaymentChannelFactory> factoryMock,
        Mock<IIdempotencyStore> idempotencyMock)
    {
        return new PaymentRequestedEventConsumer(
            repoMock.Object,
            uowMock.Object,
            factoryMock.Object,
            NullLogger<PaymentRequestedEventConsumer>.Instance,
            idempotencyMock.Object);
    }

    /// <summary>
    /// 通过反射调用受保护的 <see cref="IntegrationEventConsumerBase{T}.HandleAsync"/>。
    /// </summary>
    private static async Task InvokeHandleAsync(PaymentRequestedEventConsumer consumer, PaymentRequestedIntegrationEvent evt)
    {
        var handleMethod = typeof(PaymentRequestedEventConsumer)
            .GetMethod("HandleAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(handleMethod);
        await (Task)handleMethod!.Invoke(consumer, [evt, CancellationToken.None])!;
    }

    /// <summary>
    /// 通过反射设置 PaymentOrder.ExpireAt（private set）为指定时间。
    /// </summary>
    private static void SetExpireAt(PaymentOrder order, DateTime expireAt)
    {
        typeof(PaymentOrder)
            .GetProperty("ExpireAt")!
            .SetValue(order, expireAt);
    }

    private static PaymentOrder CreatePendingOrder()
    {
        return PaymentOrder.Create(Guid.NewGuid(), OrderId, UserId, Amount, "CNY", PaymentChannel.WeChatPay);
    }

    private static PaymentOrder CreateChannelOrderedOrderWithActiveLink()
    {
        var order = PaymentOrder.Create(Guid.NewGuid(), OrderId, UserId, Amount, "CNY", PaymentChannel.WeChatPay);
        order.MarkChannelOrdered("TRADE_EXISTING_001", "prepay_existing", "https://qr.example.com/existing", null);
        SetExpireAt(order, DateTime.UtcNow.AddHours(1));
        return order;
    }

    private static PaymentOrder CreateChannelOrderedExpiredOrder()
    {
        var order = PaymentOrder.Create(Guid.NewGuid(), OrderId, UserId, Amount, "CNY", PaymentChannel.WeChatPay);
        order.MarkChannelOrdered("TRADE_EXPIRED_001", "prepay_expired", "https://qr.example.com/expired", null);
        SetExpireAt(order, DateTime.UtcNow.AddHours(-1));
        return order;
    }

    private static PaymentOrder CreatePaidOrder()
    {
        var order = PaymentOrder.Create(Guid.NewGuid(), OrderId, UserId, Amount, "CNY", PaymentChannel.WeChatPay);
        order.MarkChannelOrdered("TRADE_PAID_001", null, null, null);
        order.MarkSucceeded("TRADE_PAID_001", Amount, DateTime.UtcNow);
        return order;
    }

    private static PaymentOrder CreateFailedOrder()
    {
        var order = PaymentOrder.Create(Guid.NewGuid(), OrderId, UserId, Amount, "CNY", PaymentChannel.WeChatPay);
        order.MarkFailed("渠道下单失败");
        return order;
    }

    private static PaymentOrder CreateClosedOrder()
    {
        var order = PaymentOrder.Create(Guid.NewGuid(), OrderId, UserId, Amount, "CNY", PaymentChannel.WeChatPay);
        order.MarkClosed("超时未支付");
        return order;
    }

    /// <summary>
    /// 配置新建支付单所需的 Mock：仓储 AddAsync、UnitOfWork、渠道适配器返回成功。
    /// </summary>
    private static void SetupNewPaymentFlow(
        Mock<IPaymentOrderRepository> repoMock,
        Mock<IUnitOfWork> uowMock,
        Mock<IPaymentChannelFactory> factoryMock)
    {
        repoMock.Setup(r => r.AddAsync(It.IsAny<PaymentOrder>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        repoMock.Setup(r => r.UpdateAsync(It.IsAny<PaymentOrder>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        uowMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var adapterMock = new Mock<IPaymentChannelAdapter>();
        adapterMock.Setup(a => a.CreatePaymentAsync(It.IsAny<PaymentOrder>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelPaymentResult
            {
                ChannelTradeNo = "4200000000202607220000000001",
                PrepayId = "wx_new_001",
                CodeUrl = "weixin://wxpay/bizpayurl?pr=new"
            });
        factoryMock.Setup(f => f.GetAdapter(PaymentChannel.WeChatPay))
            .Returns(adapterMock.Object);
    }

    private static Mock<IIdempotencyStore> CreateIdempotencyMock()
    {
        var mock = new Mock<IIdempotencyStore>();
        return mock;
    }

    [Fact]
    public async Task HandleAsync_WhenExistingPaid_ShouldThrowPaymentAlreadySucceededException()
    {
        // 安排：已存在已支付支付单
        var existing = CreatePaidOrder();
        var repoMock = new Mock<IPaymentOrderRepository>();
        repoMock.Setup(r => r.GetByOrderIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var uowMock = new Mock<IUnitOfWork>();
        var factoryMock = new Mock<IPaymentChannelFactory>();
        var idempotencyMock = CreateIdempotencyMock();

        var sut = CreateConsumer(repoMock, uowMock, factoryMock, idempotencyMock);

        // 行动 + 断言：应抛出 PaymentAlreadySucceededException，不创建新支付单
        var ex = await Assert.ThrowsAsync<PaymentAlreadySucceededException>(
            () => InvokeHandleAsync(sut, CreateEvent()));

        ex.OrderId.Should().Be(OrderId);
        ex.PaymentId.Should().Be(existing.Id);
        ex.ErrorCode.Should().Be("PAYMENT_ALREADY_SUCCEEDED");
        repoMock.Verify(r => r.AddAsync(It.IsAny<PaymentOrder>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenExistingChannelOrderedWithActiveLink_ShouldIdempotentSkip()
    {
        // 安排：已存在 ChannelOrdered 且支付链接生效的支付单
        var existing = CreateChannelOrderedOrderWithActiveLink();
        var repoMock = new Mock<IPaymentOrderRepository>();
        repoMock.Setup(r => r.GetByOrderIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var uowMock = new Mock<IUnitOfWork>();
        var factoryMock = new Mock<IPaymentChannelFactory>();
        var idempotencyMock = CreateIdempotencyMock();

        var sut = CreateConsumer(repoMock, uowMock, factoryMock, idempotencyMock);

        // 行动
        await InvokeHandleAsync(sut, CreateEvent());

        // 断言：幂等跳过，不创建新支付单，不调用渠道，不更新旧支付单
        repoMock.Verify(r => r.AddAsync(It.IsAny<PaymentOrder>(), It.IsAny<CancellationToken>()), Times.Never);
        repoMock.Verify(r => r.UpdateAsync(It.IsAny<PaymentOrder>(), It.IsAny<CancellationToken>()), Times.Never);
        factoryMock.Verify(f => f.GetAdapter(It.IsAny<PaymentChannel>()), Times.Never);
        uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
        existing.Status.Should().Be(PaymentStatus.ChannelOrdered);
    }

    [Fact]
    public async Task HandleAsync_WhenExistingPending_ShouldMarkFailedOldAndCreateNewPayment()
    {
        // 安排：已存在卡在 Pending 态的支付单（渠道下单未完成）
        var existing = CreatePendingOrder();
        var repoMock = new Mock<IPaymentOrderRepository>();
        repoMock.Setup(r => r.GetByOrderIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var uowMock = new Mock<IUnitOfWork>();
        var factoryMock = new Mock<IPaymentChannelFactory>();
        SetupNewPaymentFlow(repoMock, uowMock, factoryMock);
        var idempotencyMock = CreateIdempotencyMock();

        var sut = CreateConsumer(repoMock, uowMock, factoryMock, idempotencyMock);

        // 行动
        await InvokeHandleAsync(sut, CreateEvent());

        // 断言：旧支付单被 MarkFailed 回收
        existing.Status.Should().Be(PaymentStatus.Failed);
        existing.FailReason.Should().Contain("回收失效支付单");
        // 旧支付单被 UpdateAsync（MarkFailed），新支付单被 AddAsync + UpdateAsync（MarkChannelOrdered）
        repoMock.Verify(r => r.UpdateAsync(existing, It.IsAny<CancellationToken>()), Times.Once);
        repoMock.Verify(r => r.AddAsync(It.IsAny<PaymentOrder>(), It.IsAny<CancellationToken>()), Times.Once);
        factoryMock.Verify(f => f.GetAdapter(PaymentChannel.WeChatPay), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenExistingChannelOrderedExpired_ShouldMarkFailedOldAndCreateNewPayment()
    {
        // 安排：已存在 ChannelOrdered 但 ExpireAt 已过期的支付单
        var existing = CreateChannelOrderedExpiredOrder();
        var repoMock = new Mock<IPaymentOrderRepository>();
        repoMock.Setup(r => r.GetByOrderIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var uowMock = new Mock<IUnitOfWork>();
        var factoryMock = new Mock<IPaymentChannelFactory>();
        SetupNewPaymentFlow(repoMock, uowMock, factoryMock);
        var idempotencyMock = CreateIdempotencyMock();

        var sut = CreateConsumer(repoMock, uowMock, factoryMock, idempotencyMock);

        // 行动
        await InvokeHandleAsync(sut, CreateEvent());

        // 断言：旧支付单被 MarkFailed 回收，新支付单被创建
        existing.Status.Should().Be(PaymentStatus.Failed);
        existing.FailReason.Should().Contain("回收失效支付单");
        repoMock.Verify(r => r.UpdateAsync(existing, It.IsAny<CancellationToken>()), Times.Once);
        repoMock.Verify(r => r.AddAsync(It.IsAny<PaymentOrder>(), It.IsAny<CancellationToken>()), Times.Once);
        factoryMock.Verify(f => f.GetAdapter(PaymentChannel.WeChatPay), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenExistingFailed_ShouldCreateNewPaymentWithoutUpdatingOld()
    {
        // 安排：已存在 Failed 终态支付单，无需回收，直接创建新支付单
        var existing = CreateFailedOrder();
        var repoMock = new Mock<IPaymentOrderRepository>();
        repoMock.Setup(r => r.GetByOrderIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var uowMock = new Mock<IUnitOfWork>();
        var factoryMock = new Mock<IPaymentChannelFactory>();
        SetupNewPaymentFlow(repoMock, uowMock, factoryMock);
        var idempotencyMock = CreateIdempotencyMock();

        var sut = CreateConsumer(repoMock, uowMock, factoryMock, idempotencyMock);

        // 行动
        await InvokeHandleAsync(sut, CreateEvent());

        // 断言：旧支付单保持 Failed 态不被 UpdateAsync，新支付单被创建
        existing.Status.Should().Be(PaymentStatus.Failed);
        repoMock.Verify(r => r.UpdateAsync(existing, It.IsAny<CancellationToken>()), Times.Never);
        repoMock.Verify(r => r.AddAsync(It.IsAny<PaymentOrder>(), It.IsAny<CancellationToken>()), Times.Once);
        factoryMock.Verify(f => f.GetAdapter(PaymentChannel.WeChatPay), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenExistingClosed_ShouldCreateNewPaymentWithoutUpdatingOld()
    {
        // 安排：已存在 Closed 终态支付单，无需回收，直接创建新支付单
        var existing = CreateClosedOrder();
        var repoMock = new Mock<IPaymentOrderRepository>();
        repoMock.Setup(r => r.GetByOrderIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var uowMock = new Mock<IUnitOfWork>();
        var factoryMock = new Mock<IPaymentChannelFactory>();
        SetupNewPaymentFlow(repoMock, uowMock, factoryMock);
        var idempotencyMock = CreateIdempotencyMock();

        var sut = CreateConsumer(repoMock, uowMock, factoryMock, idempotencyMock);

        // 行动
        await InvokeHandleAsync(sut, CreateEvent());

        // 断言：旧支付单保持 Closed 态不被 UpdateAsync，新支付单被创建
        existing.Status.Should().Be(PaymentStatus.Closed);
        repoMock.Verify(r => r.UpdateAsync(existing, It.IsAny<CancellationToken>()), Times.Never);
        repoMock.Verify(r => r.AddAsync(It.IsAny<PaymentOrder>(), It.IsAny<CancellationToken>()), Times.Once);
        factoryMock.Verify(f => f.GetAdapter(PaymentChannel.WeChatPay), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenNoExistingPayment_ShouldCreateNewPayment()
    {
        // 安排：不存在已存在支付单
        var repoMock = new Mock<IPaymentOrderRepository>();
        repoMock.Setup(r => r.GetByOrderIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentOrder?)null);

        var uowMock = new Mock<IUnitOfWork>();
        var factoryMock = new Mock<IPaymentChannelFactory>();
        SetupNewPaymentFlow(repoMock, uowMock, factoryMock);
        var idempotencyMock = CreateIdempotencyMock();

        var sut = CreateConsumer(repoMock, uowMock, factoryMock, idempotencyMock);

        // 行动
        await InvokeHandleAsync(sut, CreateEvent());

        // 断言：创建新支付单
        repoMock.Verify(r => r.AddAsync(It.IsAny<PaymentOrder>(), It.IsAny<CancellationToken>()), Times.Once);
        factoryMock.Verify(f => f.GetAdapter(PaymentChannel.WeChatPay), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenExistingPendingMarkFailed_ShouldPublishPaymentFailedDomainEvent()
    {
        // 安排：验证 MarkFailed 回收旧支付单时发布 PaymentFailedDomainEvent（而非 PaymentClosedDomainEvent），
        // 使订单域保持待支付可重试，避免触发订单取消。
        var existing = CreatePendingOrder();
        var repoMock = new Mock<IPaymentOrderRepository>();
        repoMock.Setup(r => r.GetByOrderIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var uowMock = new Mock<IUnitOfWork>();
        var factoryMock = new Mock<IPaymentChannelFactory>();
        SetupNewPaymentFlow(repoMock, uowMock, factoryMock);
        var idempotencyMock = CreateIdempotencyMock();

        var sut = CreateConsumer(repoMock, uowMock, factoryMock, idempotencyMock);

        // 行动
        await InvokeHandleAsync(sut, CreateEvent());

        // 断言：旧支付单的领域事件应包含 PaymentFailedDomainEvent，不包含 PaymentClosedDomainEvent
        var domainEvents = existing.DomainEvents.ToList();
        domainEvents.Should().Contain(e => e.GetType().Name == "PaymentFailedDomainEvent");
        domainEvents.Should().NotContain(e => e.GetType().Name == "PaymentClosedDomainEvent");
    }
}
