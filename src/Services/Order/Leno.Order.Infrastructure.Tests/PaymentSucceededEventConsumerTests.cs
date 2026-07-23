using System.Reflection;
using Leno.Infrastructure.EventBus;
using Leno.Order.Application.ProcessManagers;
using Leno.Order.Domain.Aggregates;
using Leno.Order.Domain.Repositories;
using Leno.Order.Domain.ValueObjects;
using Leno.Order.Infrastructure.Consumers;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Leno.Infrastructure.Abstractions;
using Moq;
using OrderAggregate = Leno.Order.Domain.Aggregates.Order;

namespace Leno.Order.Infrastructure.Tests;

/// <summary>
/// 支付成功事件消费者测试。
/// T4 拆分后，本消费者仅负责订单状态变更（MarkAsPaid）与本地事务保存，
/// 库存确认与积分确认已迁移至独立的 <see cref="StockConfirmConsumer"/> / <see cref="PointsConfirmConsumer"/>。
/// 3.3：双轨期 feature flag 默认关闭，Process Manager 不参与（旧路径行为不变）。
/// </summary>
public class PaymentSucceededEventConsumerTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid SellerId = Guid.NewGuid();
    private static readonly Guid SkuId1 = Guid.NewGuid();
    private static readonly Guid SkuId2 = Guid.NewGuid();
    private static readonly Guid SpuId = Guid.NewGuid();
    private static readonly Guid OrderId = Guid.NewGuid();
    private static readonly Guid PaymentId = Guid.NewGuid();

    [Fact]
    public async Task HandleAsync_PaymentSucceeded_ShouldMarkAsPaidAndSave()
    {
        // Arrange
        var order = CreateOrderWithTwoSkus();
        // 模拟支付已发起，使订单进入 PendingPayment 状态，方可被 MarkAsPaid 标记为已支付
        order.MarkPaymentInitiated(PaymentMethod.Alipay);
        var mockOrderRepo = new Mock<IOrderRepository>();
        mockOrderRepo.Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockLogger = new Mock<ILogger<PaymentSucceededEventConsumer>>();
        var mockIdempotencyStore = new Mock<IIdempotencyStore>();

        var consumer = CreateConsumer(mockOrderRepo.Object, mockUnitOfWork.Object, mockLogger.Object, mockIdempotencyStore.Object);

        var integrationEvent = new PaymentSucceededEvent
        {
            EventId = Guid.NewGuid(),
            OrderId = OrderId,
            PaymentId = PaymentId,
            UserId = UserId,
            Channel = "alipay",
            TradeNo = "2024071200001",
            PaidAt = DateTime.UtcNow,
            Amount = order.TotalAmount
        };

        // Act - 通过反射调用受保护的 HandleAsync
        var handleMethod = typeof(PaymentSucceededEventConsumer)
            .GetMethod("HandleAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(handleMethod);

        await (Task)handleMethod!.Invoke(consumer, [integrationEvent, CancellationToken.None])!;

        // Assert
        // 1. 订单应被标记为已支付
        Assert.Equal(OrderStatus.Paid, order.Status);
        Assert.Equal(PaymentId, order.PaymentId);

        // 2. 订单应被更新并保存（含 Outbox 领域事件持久化）
        mockOrderRepo.Verify(r => r.UpdateAsync(order, It.IsAny<CancellationToken>()), Times.Once);
        mockUnitOfWork.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_OrderNotFound_ShouldSkipAndNotSave()
    {
        // Arrange
        var mockOrderRepo = new Mock<IOrderRepository>();
        mockOrderRepo.Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrderAggregate?)null);

        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockLogger = new Mock<ILogger<PaymentSucceededEventConsumer>>();
        var mockIdempotencyStore = new Mock<IIdempotencyStore>();

        var consumer = CreateConsumer(mockOrderRepo.Object, mockUnitOfWork.Object, mockLogger.Object, mockIdempotencyStore.Object);

        var integrationEvent = new PaymentSucceededEvent
        {
            EventId = Guid.NewGuid(),
            OrderId = OrderId,
            PaymentId = PaymentId,
            UserId = UserId,
            Channel = "alipay",
            TradeNo = "2024071200001",
            PaidAt = DateTime.UtcNow
        };

        // Act
        var handleMethod = typeof(PaymentSucceededEventConsumer)
            .GetMethod("HandleAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(handleMethod);

        await (Task)handleMethod!.Invoke(consumer, [integrationEvent, CancellationToken.None])!;

        // Assert
        mockOrderRepo.Verify(r => r.UpdateAsync(It.IsAny<OrderAggregate>(), It.IsAny<CancellationToken>()), Times.Never);
        mockUnitOfWork.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_OrderNotPendingPayment_ShouldSkipAndNotSave()
    {
        // Arrange
        var order = CreateOrderWithTwoSkus();
        // 先将订单置为已支付状态
        order.MarkPaymentInitiated(PaymentMethod.WeChatPay);
        order.MarkAsPaid(Guid.NewGuid(), "wechat", DateTime.UtcNow, "2024071200002", order.TotalAmount);

        var mockOrderRepo = new Mock<IOrderRepository>();
        mockOrderRepo.Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockLogger = new Mock<ILogger<PaymentSucceededEventConsumer>>();
        var mockIdempotencyStore = new Mock<IIdempotencyStore>();

        var consumer = CreateConsumer(mockOrderRepo.Object, mockUnitOfWork.Object, mockLogger.Object, mockIdempotencyStore.Object);

        var integrationEvent = new PaymentSucceededEvent
        {
            EventId = Guid.NewGuid(),
            OrderId = OrderId,
            PaymentId = PaymentId,
            UserId = UserId,
            Channel = "alipay",
            TradeNo = "2024071200001",
            PaidAt = DateTime.UtcNow
        };

        // Act
        var handleMethod = typeof(PaymentSucceededEventConsumer)
            .GetMethod("HandleAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(handleMethod);

        await (Task)handleMethod!.Invoke(consumer, [integrationEvent, CancellationToken.None])!;

        // Assert
        mockOrderRepo.Verify(r => r.UpdateAsync(It.IsAny<OrderAggregate>(), It.IsAny<CancellationToken>()), Times.Never);
        mockUnitOfWork.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }


    [Fact]
    public async Task HandleAsync_MembershipOrderPaymentSucceeded_ShouldAutoComplete()
    {
        // Arrange
        var order = CreateMembershipOrder();
        // 模拟支付已发起，使订单进入 PendingPayment 状态，方可被 MarkAsPaid 标记为已支付
        order.MarkPaymentInitiated(PaymentMethod.Alipay);
        var mockOrderRepo = new Mock<IOrderRepository>();
        mockOrderRepo.Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockLogger = new Mock<ILogger<PaymentSucceededEventConsumer>>();
        var mockIdempotencyStore = new Mock<IIdempotencyStore>();

        var consumer = CreateConsumer(mockOrderRepo.Object, mockUnitOfWork.Object, mockLogger.Object, mockIdempotencyStore.Object);

        var integrationEvent = new PaymentSucceededEvent
        {
            EventId = Guid.NewGuid(),
            OrderId = OrderId,
            PaymentId = PaymentId,
            UserId = UserId,
            Channel = "alipay",
            TradeNo = "2024071200001",
            PaidAt = DateTime.UtcNow,
            Amount = order.TotalAmount
        };

        // Act
        var handleMethod = typeof(PaymentSucceededEventConsumer)
            .GetMethod("HandleAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(handleMethod);

        await (Task)handleMethod!.Invoke(consumer, [integrationEvent, CancellationToken.None])!;

        // Assert
        // 1. 会员订单应被标记为已支付并自动完成
        Assert.Equal(OrderStatus.Completed, order.Status);
        Assert.Equal(PaymentId, order.PaymentId);

        // 2. 订单应被更新并保存
        mockOrderRepo.Verify(r => r.UpdateAsync(order, It.IsAny<CancellationToken>()), Times.Once);
        mockUnitOfWork.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// 创建被测消费者实例，注入默认关闭 Process Manager 的 Options（双轨期：旧路径行为不变）。
    /// </summary>
    private static PaymentSucceededEventConsumer CreateConsumer(
        IOrderRepository orderRepo,
        IUnitOfWork unitOfWork,
        ILogger<PaymentSucceededEventConsumer> logger,
        IIdempotencyStore idempotencyStore)
    {
        var processManagerMock = new Mock<IOrderPaymentProcessManager>();
        var optionsMock = new Mock<IOptionsMonitor<OrderPaymentProcessOptions>>();
        optionsMock.Setup(o => o.CurrentValue)
            .Returns(new OrderPaymentProcessOptions { UsePaymentProcessManager = false });

        return new PaymentSucceededEventConsumer(
            orderRepo,
            unitOfWork,
            logger,
            idempotencyStore,
            processManagerMock.Object,
            optionsMock.Object);
    }

    private static OrderAggregate CreateMembershipOrder()
    {
        var snapshot = ProductSnapshot.Create(SkuId1, SpuId, "会员套餐", "月度会员", null, SellerId);
        var item = OrderItem.Create(Guid.NewGuid(), SkuId1, snapshot, 29.99m, 1, null);
        var address = AddressSnapshot.Create("张三", "13800138000", "广东省", "深圳市", "南山区", "科技园路1号");

        return OrderAggregate.Create(
            OrderId,
            "ORD2024071200002",
            OrderType.Membership,
            UserId,
            Guid.Empty, // membership orders allow empty SellerId
            [item],
            address,
            freightAmount: 0m,
            pointsOffsetAmount: 0m,
            DateTime.UtcNow.AddHours(2));
    }

    private static OrderAggregate CreateOrderWithTwoSkus()
    {
        var skuId1 = SkuId1;
        var skuId2 = SkuId2;

        var snapshot1 = ProductSnapshot.Create(skuId1, SpuId, "商品A", "商品A-红色", null, SellerId);
        var snapshot2 = ProductSnapshot.Create(skuId2, SpuId, "商品B", "商品B-蓝色", null, SellerId);

        var item1 = OrderItem.Create(Guid.NewGuid(), skuId1, snapshot1, 99.99m, 3, null);
        var item2 = OrderItem.Create(Guid.NewGuid(), skuId2, snapshot2, 49.99m, 5, null);

        var address = AddressSnapshot.Create("张三", "13800138000", "广东省", "深圳市", "南山区", "科技园路1号");

        return OrderAggregate.Create(
            OrderId,
            "ORD2024071200001",
            OrderType.Normal,
            UserId,
            SellerId,
            [item1, item2],
            address,
            freightAmount: 10m,
            pointsOffsetAmount: 0m,
            DateTime.UtcNow.AddHours(2));
    }
}
