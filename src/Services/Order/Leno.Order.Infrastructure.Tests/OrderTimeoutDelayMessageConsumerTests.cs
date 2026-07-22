using Leno.Infrastructure.Abstractions;
using Leno.Order.Application.Messages;
using Leno.Order.Application.Services;
using Leno.Order.Domain.Aggregates;
using Leno.Order.Domain.Repositories;
using Leno.Order.Domain.Services;
using Leno.Order.Domain.ValueObjects;
using Leno.Order.Infrastructure.Consumers;
using Leno.SharedKernel.Abstractions;
using MassTransit;
using Microsoft.Extensions.Logging;
using Moq;
using OrderAggregate = Leno.Order.Domain.Aggregates.Order;

namespace Leno.Order.Infrastructure.Tests;

public class OrderTimeoutDelayMessageConsumerTests
{
    private readonly Mock<IOrderRepository> _orderRepoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<IStockReservationDomainService> _stockServiceMock = new();
    private readonly Mock<IPointsAntiCorruptionService> _pointsAcMock = new();
    private readonly Mock<IPromotionAntiCorruptionService> _promotionAcMock = new();
    private readonly Mock<IIdempotencyStore> _idempotencyStoreMock = new();
    private readonly Mock<ILogger<OrderTimeoutDelayMessageConsumer>> _loggerMock = new();
    private readonly OrderTimeoutDelayMessageConsumer _sut;

    private static readonly Guid OrderId = Guid.NewGuid();
    private static readonly Guid SkuId = Guid.NewGuid();
    private static readonly Guid SellerId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    public OrderTimeoutDelayMessageConsumerTests()
    {
        // 默认未处理，允许消费者执行业务逻辑
        _idempotencyStoreMock.Setup(s => s.IsProcessedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _sut = new OrderTimeoutDelayMessageConsumer(
            _orderRepoMock.Object,
            _uowMock.Object,
            _stockServiceMock.Object,
            _pointsAcMock.Object,
            _promotionAcMock.Object,
            _idempotencyStoreMock.Object,
            _loggerMock.Object);
    }

    private static Mock<ConsumeContext<OrderTimeoutMessage>> CreateConsumeContext(OrderTimeoutMessage message, CancellationToken ct = default)
    {
        var mock = new Mock<ConsumeContext<OrderTimeoutMessage>>();
        mock.Setup(c => c.Message).Returns(message);
        mock.Setup(c => c.CancellationToken).Returns(ct == default ? CancellationToken.None : ct);
        return mock;
    }

    private static OrderAggregate CreateOrder(OrderStatus status = OrderStatus.PendingPayment, DateTime? expireAt = null)
    {
        var snapshot = ProductSnapshot.Create(SkuId, Guid.NewGuid(), "Test Product", "Red-XL", null, SellerId);
        var item = OrderItem.Create(Guid.NewGuid(), SkuId, snapshot, 99.99m, 2, null);
        var order = OrderAggregate.Create(
            OrderId, "ORD-001", OrderType.Normal, UserId, SellerId,
            new List<OrderItem> { item }, CreateAddress(), 10m, 0m,
            DateTime.UtcNow.AddHours(1)); // must be future for domain validation

        // Override via reflection for test scenarios
        if (status != OrderStatus.PendingPayment)
        {
            typeof(OrderAggregate).GetProperty(nameof(OrderAggregate.Status))!
                .SetValue(order, status);
        }

        if (expireAt.HasValue)
        {
            typeof(OrderAggregate).GetProperty(nameof(OrderAggregate.ExpireAt))!
                .SetValue(order, expireAt.Value);
        }

        return order;
    }

    private static AddressSnapshot CreateAddress()
        => AddressSnapshot.Create("张三", "13800138000", "广东", "深圳", "南山区", "科技园路1号");

    #region 超时取消释放库存

    [Fact]
    public async Task Consume_ExpiredPendingPayment_ShouldReleaseStock()
    {
        // Arrange
        var order = CreateOrder(OrderStatus.PendingPayment, DateTime.UtcNow.AddHours(-1));
        _orderRepoMock.Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        _stockServiceMock.Setup(s => s.ReleaseBatchAsync(OrderId, It.IsAny<Dictionary<Guid, int>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var msg = new OrderTimeoutMessage(OrderId);
        var context = CreateConsumeContext(msg);

        // Act
        await _sut.Consume(context.Object);

        // Assert
        _stockServiceMock.Verify(
            s => s.ReleaseBatchAsync(
                OrderId,
                It.Is<Dictionary<Guid, int>>(d => d.ContainsKey(SkuId) && d[SkuId] == 2),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region 超时取消释放积分

    [Fact]
    public async Task Consume_ExpiredPendingPayment_ShouldReleasePoints()
    {
        // Arrange
        var order = CreateOrder(OrderStatus.PendingPayment, DateTime.UtcNow.AddHours(-1));
        _orderRepoMock.Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        _stockServiceMock.Setup(s => s.ReleaseBatchAsync(OrderId, It.IsAny<Dictionary<Guid, int>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var msg = new OrderTimeoutMessage(OrderId);
        var context = CreateConsumeContext(msg);

        // Act
        await _sut.Consume(context.Object);

        // Assert
        _pointsAcMock.Verify(
            p => p.ReleaseAsync(OrderId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region 超时取消释放优惠券

    [Fact]
    public async Task Consume_ExpiredPendingPayment_ShouldReleaseCoupons()
    {
        // Arrange
        var order = CreateOrder(OrderStatus.PendingPayment, DateTime.UtcNow.AddHours(-1));
        _orderRepoMock.Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        _stockServiceMock.Setup(s => s.ReleaseBatchAsync(OrderId, It.IsAny<Dictionary<Guid, int>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var msg = new OrderTimeoutMessage(OrderId);
        var context = CreateConsumeContext(msg);

        // Act
        await _sut.Consume(context.Object);

        // Assert
        _promotionAcMock.Verify(
            p => p.ReleaseCouponsAsync(OrderId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region 跳过非待支付订单

    [Fact]
    public async Task Consume_NonPendingPayment_ShouldSkip()
    {
        // Arrange
        var order = CreateOrder(OrderStatus.Paid, DateTime.UtcNow.AddHours(-1));
        _orderRepoMock.Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var msg = new OrderTimeoutMessage(OrderId);
        var context = CreateConsumeContext(msg);

        // Act
        await _sut.Consume(context.Object);

        // Assert
        _stockServiceMock.Verify(
            s => s.ReleaseBatchAsync(It.IsAny<Guid>(), It.IsAny<Dictionary<Guid, int>>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _pointsAcMock.Verify(
            p => p.ReleaseAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _promotionAcMock.Verify(
            p => p.ReleaseCouponsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _uowMock.Verify(
            u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region 跳过未到支付截止时间的订单

    [Fact]
    public async Task Consume_NotYetExpired_ShouldSkip()
    {
        // Arrange
        var order = CreateOrder(OrderStatus.PendingPayment, DateTime.UtcNow.AddHours(1)); // 未来过期
        _orderRepoMock.Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var msg = new OrderTimeoutMessage(OrderId);
        var context = CreateConsumeContext(msg);

        // Act
        await _sut.Consume(context.Object);

        // Assert
        _stockServiceMock.Verify(
            s => s.ReleaseBatchAsync(It.IsAny<Guid>(), It.IsAny<Dictionary<Guid, int>>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _pointsAcMock.Verify(
            p => p.ReleaseAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _promotionAcMock.Verify(
            p => p.ReleaseCouponsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _uowMock.Verify(
            u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region 订单不存在跳过

    [Fact]
    public async Task Consume_OrderNotFound_ShouldSkip()
    {
        // Arrange
        _orderRepoMock.Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrderAggregate?)null);

        var msg = new OrderTimeoutMessage(OrderId);
        var context = CreateConsumeContext(msg);

        // Act
        await _sut.Consume(context.Object);

        // Assert
        _stockServiceMock.Verify(
            s => s.ReleaseBatchAsync(It.IsAny<Guid>(), It.IsAny<Dictionary<Guid, int>>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _pointsAcMock.Verify(
            p => p.ReleaseAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _promotionAcMock.Verify(
            p => p.ReleaseCouponsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region 幂等去重：已处理则跳过

    [Fact]
    public async Task Consume_AlreadyProcessed_ShouldSkipBusinessLogic()
    {
        // Arrange —— 幂等存储返回已处理
        _idempotencyStoreMock.Setup(s => s.IsProcessedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var msg = new OrderTimeoutMessage(OrderId);
        var context = CreateConsumeContext(msg);

        // Act
        await _sut.Consume(context.Object);

        // Assert —— 不应加载订单、不应释放库存/积分/优惠券、不应保存
        _orderRepoMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _stockServiceMock.Verify(
            s => s.ReleaseBatchAsync(It.IsAny<Guid>(), It.IsAny<Dictionary<Guid, int>>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _pointsAcMock.Verify(
            p => p.ReleaseAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _promotionAcMock.Verify(
            p => p.ReleaseCouponsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion
}