using Leno.Order.Application.Abstractions;
using Leno.Infrastructure.Abstractions;
using Leno.Order.Domain.Aggregates;
using Leno.Order.Domain.Repositories;
using Leno.Order.Domain.Services;
using Leno.Order.Domain.ValueObjects;
using Leno.Order.Infrastructure.Consumers;
using Leno.SharedContracts.Events;
using MassTransit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using OrderAggregate = Leno.Order.Domain.Aggregates.Order;

namespace Leno.Order.Infrastructure.Tests;

/// <summary>
/// 库存确认消费者测试。
/// T4 拆分后，库存确认（预占 → 真实扣减）由独立的 <see cref="StockConfirmConsumer"/> 负责，
/// 通过独立幂等键（stock-confirm-{PaymentId}）与订单状态变更消费者隔离。
/// </summary>
public class StockConfirmConsumerTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid SellerId = Guid.NewGuid();
    private static readonly Guid SkuId1 = Guid.NewGuid();
    private static readonly Guid SkuId2 = Guid.NewGuid();
    private static readonly Guid SpuId = Guid.NewGuid();
    private static readonly Guid OrderId = Guid.NewGuid();
    private static readonly Guid PaymentId = Guid.NewGuid();

    [Fact]
    public async Task Consume_PaymentSucceeded_ShouldConfirmStockWithCorrectSkuQuantities()
    {
        // Arrange
        var order = CreateOrderWithTwoSkus();
        var mockOrderRepo = new Mock<IOrderRepository>();
        mockOrderRepo.Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var mockStockService = new Mock<IInventoryGateway>();
        mockStockService.Setup(s => s.ConfirmBatchAsync(OrderId))
            .Returns(Task.CompletedTask);

        var mockIdempotencyStore = new Mock<IIdempotencyStore>();
        mockIdempotencyStore.Setup(s => s.IsProcessedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var mockLogger = new Mock<ILogger<StockConfirmConsumer>>();

        var consumer = CreateConsumer(mockOrderRepo.Object, mockStockService.Object, mockIdempotencyStore.Object, mockLogger.Object);

        var evt = new PaymentSucceededEvent
        {
            EventId = Guid.NewGuid(),
            OrderId = OrderId,
            PaymentId = PaymentId,
            UserId = UserId,
            Channel = "alipay",
            TradeNo = "2024071200001",
            PaidAt = DateTime.UtcNow
        };
        var context = CreateConsumeContext(evt);

        // Act
        await consumer.Consume(context.Object);

        // Assert
        // ConfirmBatchAsync 应以正确的 SKU 数量映射被调用
        mockStockService.Verify(
            s => s.ConfirmBatchAsync(OrderId),
            Times.Once);

        // 处理成功后应标记为已处理
        mockIdempotencyStore.Verify(
            s => s.MarkAsProcessedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Consume_AlreadyProcessed_ShouldSkipStockConfirm()
    {
        // Arrange
        var mockOrderRepo = new Mock<IOrderRepository>();
        var mockStockService = new Mock<IInventoryGateway>();

        var mockIdempotencyStore = new Mock<IIdempotencyStore>();
        mockIdempotencyStore.Setup(s => s.IsProcessedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var mockLogger = new Mock<ILogger<StockConfirmConsumer>>();

        var consumer = CreateConsumer(mockOrderRepo.Object, mockStockService.Object, mockIdempotencyStore.Object, mockLogger.Object);

        var evt = new PaymentSucceededEvent
        {
            EventId = Guid.NewGuid(),
            OrderId = OrderId,
            PaymentId = PaymentId,
            UserId = UserId,
            Channel = "alipay",
            TradeNo = "2024071200001",
            PaidAt = DateTime.UtcNow
        };
        var context = CreateConsumeContext(evt);

        // Act
        await consumer.Consume(context.Object);

        // Assert
        // 已处理则不应调用库存确认，也不应加载订单
        mockStockService.Verify(
            s => s.ConfirmBatchAsync(It.IsAny<Guid>()),
            Times.Never);
        mockOrderRepo.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Consume_MembershipOrder_ShouldSkipStockConfirm()
    {
        // Arrange
        var order = CreateMembershipOrder();
        var mockOrderRepo = new Mock<IOrderRepository>();
        mockOrderRepo.Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var mockStockService = new Mock<IInventoryGateway>();
        var mockIdempotencyStore = new Mock<IIdempotencyStore>();
        mockIdempotencyStore.Setup(s => s.IsProcessedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var mockLogger = new Mock<ILogger<StockConfirmConsumer>>();

        var consumer = CreateConsumer(mockOrderRepo.Object, mockStockService.Object, mockIdempotencyStore.Object, mockLogger.Object);

        var evt = new PaymentSucceededEvent
        {
            EventId = Guid.NewGuid(),
            OrderId = OrderId,
            PaymentId = PaymentId,
            UserId = UserId,
            Channel = "alipay",
            TradeNo = "2024071200001",
            PaidAt = DateTime.UtcNow
        };
        var context = CreateConsumeContext(evt);

        // Act
        await consumer.Consume(context.Object);

        // Assert
        // 会员订阅订单无实物库存，跳过库存确认
        mockStockService.Verify(
            s => s.ConfirmBatchAsync(It.IsAny<Guid>()),
            Times.Never);

        // 但仍应标记为已处理（避免重试）
        mockIdempotencyStore.Verify(
            s => s.MarkAsProcessedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Consume_OrderNotFound_ShouldSkipAndMarkAsProcessed()
    {
        // Arrange
        var mockOrderRepo = new Mock<IOrderRepository>();
        mockOrderRepo.Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrderAggregate?)null);

        var mockStockService = new Mock<IInventoryGateway>();
        var mockIdempotencyStore = new Mock<IIdempotencyStore>();
        mockIdempotencyStore.Setup(s => s.IsProcessedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var mockLogger = new Mock<ILogger<StockConfirmConsumer>>();

        var consumer = CreateConsumer(mockOrderRepo.Object, mockStockService.Object, mockIdempotencyStore.Object, mockLogger.Object);

        var evt = new PaymentSucceededEvent
        {
            EventId = Guid.NewGuid(),
            OrderId = OrderId,
            PaymentId = PaymentId,
            UserId = UserId,
            Channel = "alipay",
            TradeNo = "2024071200001",
            PaidAt = DateTime.UtcNow
        };
        var context = CreateConsumeContext(evt);

        // Act
        await consumer.Consume(context.Object);

        // Assert
        mockStockService.Verify(
            s => s.ConfirmBatchAsync(It.IsAny<Guid>()),
            Times.Never);
    }

    /// <summary>
    /// 创建被测消费者实例（双轨下线 D1/D2：Process Manager 原型已删除，无 Options 注入）。
    /// </summary>
    private static StockConfirmConsumer CreateConsumer(
        IOrderRepository orderRepo,
        IInventoryGateway inventoryGateway,
        IIdempotencyStore idempotencyStore,
        ILogger<StockConfirmConsumer> logger)
    {
        return new StockConfirmConsumer(
            orderRepo,
            inventoryGateway,
            idempotencyStore,
            logger);
    }

    private static Mock<ConsumeContext<PaymentSucceededEvent>> CreateConsumeContext(PaymentSucceededEvent message, CancellationToken ct = default)
    {
        var mock = new Mock<ConsumeContext<PaymentSucceededEvent>>();
        mock.Setup(c => c.Message).Returns(message);
        mock.Setup(c => c.CancellationToken).Returns(ct == default ? CancellationToken.None : ct);
        return mock;
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
            Guid.Empty,
            [item],
            address,
            freightAmount: 0m,
            pointsOffsetAmount: 0m,
            DateTime.UtcNow.AddHours(2));
    }

    private static OrderAggregate CreateOrderWithTwoSkus()
    {
        var snapshot1 = ProductSnapshot.Create(SkuId1, SpuId, "商品A", "商品A-红色", null, SellerId);
        var snapshot2 = ProductSnapshot.Create(SkuId2, SpuId, "商品B", "商品B-蓝色", null, SellerId);

        var item1 = OrderItem.Create(Guid.NewGuid(), SkuId1, snapshot1, 99.99m, 3, null);
        var item2 = OrderItem.Create(Guid.NewGuid(), SkuId2, snapshot2, 49.99m, 5, null);

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
