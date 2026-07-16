using System.Reflection;
using Leno.Infrastructure.EventBus;
using Leno.Order.Application.Services;
using Leno.Order.Domain.Aggregates;
using Leno.Order.Domain.Repositories;
using Leno.Order.Domain.Services;
using Leno.Order.Domain.ValueObjects;
using Leno.Order.Infrastructure.Consumers;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;
using Leno.Infrastructure.Abstractions;
using Moq;
using OrderAggregate = Leno.Order.Domain.Aggregates.Order;

namespace Leno.Order.Infrastructure.Tests;

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
    public async Task HandleAsync_PaymentSucceeded_ShouldConfirmStockAfterMarkAsPaid()
    {
        // Arrange
        var order = CreateOrderWithTwoSkus();
        var mockOrderRepo = new Mock<IOrderRepository>();
        mockOrderRepo.Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockStockService = new Mock<IStockReservationDomainService>();
        var mockPointsAc = new Mock<IPointsAntiCorruptionService>();
        var mockLogger = new Mock<ILogger<PaymentSucceededEventConsumer>>();
        var mockIdempotencyStore = new Mock<IIdempotencyStore>();

        var consumer = new PaymentSucceededEventConsumer(
            mockOrderRepo.Object,
            mockUnitOfWork.Object,
            mockStockService.Object,
            mockPointsAc.Object,
            mockLogger.Object,
            mockIdempotencyStore.Object);

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

        // Act - invoke protected HandleAsync via reflection
        var handleMethod = typeof(PaymentSucceededEventConsumer)
            .GetMethod("HandleAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(handleMethod);

        await (Task)handleMethod!.Invoke(consumer, [integrationEvent, CancellationToken.None])!;

        // Assert
        // 1. Order should be marked as paid
        Assert.Equal(OrderStatus.Paid, order.Status);
        Assert.Equal(PaymentId, order.PaymentId);

        // 2. ConfirmBatchAsync should be called with correct sku quantities
        mockStockService.Verify(
            s => s.ConfirmBatchAsync(
                OrderId,
                It.Is<Dictionary<Guid, int>>(d =>
                    d.Count == 2 &&
                    d[SkuId1] == 3 &&
                    d[SkuId2] == 5),
                It.IsAny<CancellationToken>()),
            Times.Once);

        // 3. ConfirmDeductionAsync should be called for points
        mockPointsAc.Verify(
            p => p.ConfirmDeductionAsync(OrderId, It.IsAny<CancellationToken>()),
            Times.Once);

        // 4. Order should be updated and saved
        mockOrderRepo.Verify(r => r.UpdateAsync(order, It.IsAny<CancellationToken>()), Times.Once);
        mockUnitOfWork.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_OrderNotFound_ShouldSkipAndNotConfirmStock()
    {
        // Arrange
        var mockOrderRepo = new Mock<IOrderRepository>();
        mockOrderRepo.Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrderAggregate?)null);

        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockStockService = new Mock<IStockReservationDomainService>();
        var mockPointsAc = new Mock<IPointsAntiCorruptionService>();
        var mockLogger = new Mock<ILogger<PaymentSucceededEventConsumer>>();
        var mockIdempotencyStore = new Mock<IIdempotencyStore>();

        var consumer = new PaymentSucceededEventConsumer(
            mockOrderRepo.Object,
            mockUnitOfWork.Object,
            mockStockService.Object,
            mockPointsAc.Object,
            mockLogger.Object,
            mockIdempotencyStore.Object);

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
        mockStockService.Verify(
            s => s.ConfirmBatchAsync(It.IsAny<Guid>(), It.IsAny<Dictionary<Guid, int>>(), It.IsAny<CancellationToken>()),
            Times.Never);
        mockOrderRepo.Verify(r => r.UpdateAsync(It.IsAny<OrderAggregate>(), It.IsAny<CancellationToken>()), Times.Never);
        mockUnitOfWork.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_OrderNotPendingPayment_ShouldSkipAndNotConfirmStock()
    {
        // Arrange
        var order = CreateOrderWithTwoSkus();
        // 先将订单置为已支付状态
        order.MarkAsPaid(Guid.NewGuid(), "wechat", DateTime.UtcNow, "2024071200002");

        var mockOrderRepo = new Mock<IOrderRepository>();
        mockOrderRepo.Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockStockService = new Mock<IStockReservationDomainService>();
        var mockPointsAc = new Mock<IPointsAntiCorruptionService>();
        var mockLogger = new Mock<ILogger<PaymentSucceededEventConsumer>>();
        var mockIdempotencyStore = new Mock<IIdempotencyStore>();

        var consumer = new PaymentSucceededEventConsumer(
            mockOrderRepo.Object,
            mockUnitOfWork.Object,
            mockStockService.Object,
            mockPointsAc.Object,
            mockLogger.Object,
            mockIdempotencyStore.Object);

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
        mockStockService.Verify(
            s => s.ConfirmBatchAsync(It.IsAny<Guid>(), It.IsAny<Dictionary<Guid, int>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }


    [Fact]
    public async Task HandleAsync_MembershipOrderPaymentSucceeded_ShouldAutoComplete()
    {
        // Arrange
        var order = CreateMembershipOrder();
        var mockOrderRepo = new Mock<IOrderRepository>();
        mockOrderRepo.Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockStockService = new Mock<IStockReservationDomainService>();
        var mockPointsAc = new Mock<IPointsAntiCorruptionService>();
        var mockLogger = new Mock<ILogger<PaymentSucceededEventConsumer>>();
        var mockIdempotencyStore = new Mock<IIdempotencyStore>();

        var consumer = new PaymentSucceededEventConsumer(
            mockOrderRepo.Object,
            mockUnitOfWork.Object,
            mockStockService.Object,
            mockPointsAc.Object,
            mockLogger.Object,
            mockIdempotencyStore.Object);

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
        // 1. Order should be marked as paid
        Assert.Equal(OrderStatus.Completed, order.Status);
        Assert.Equal(PaymentId, order.PaymentId);

        // 2. Stock should NOT be confirmed (membership orders skip stock)
        mockStockService.Verify(
            s => s.ConfirmBatchAsync(It.IsAny<Guid>(), It.IsAny<Dictionary<Guid, int>>(), It.IsAny<CancellationToken>()),
            Times.Never);

        // 3. Order should be updated and saved
        mockOrderRepo.Verify(r => r.UpdateAsync(order, It.IsAny<CancellationToken>()), Times.Once);
        mockUnitOfWork.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
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
    }    private static OrderAggregate CreateOrderWithTwoSkus()
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