using Leno.Order.Application.DTOs;
using Leno.Order.Application.Services;
using Leno.Order.Domain.Aggregates;
using Leno.Order.Domain.Events;
using Leno.Order.Domain.Repositories;
using Leno.Order.Domain.Services;
using Leno.Order.Domain.ValueObjects;
using Leno.Infrastructure.Abstractions;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using MassTransit;
using Moq;
using OrderAggregate = Leno.Order.Domain.Aggregates.Order;

namespace Leno.Order.Application.Tests;

/// <summary>
/// ForceCancel 改走 Outbox 同事务发布退款事件的单元测试。
/// 验证 OrderAppService.ForceCancelAsync 不再直接调用 IEventBus.PublishAsync 发布
/// RefundRequestedDomainEvent，而是通过聚合 AddDomainEvent + SaveEntitiesAsync 走 Outbox。
/// </summary>
public class OrderAppServiceForceCancelTests
{
    private readonly Mock<IOrderRepository> _orderRepoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<IOrderNumberGenerator> _orderNoGenMock = new();
    private readonly Mock<IStockReservationDomainService> _stockSvcMock = new();
    private readonly Mock<IOrderPricingDomainService> _pricingSvcMock = new();
    private readonly Mock<IFreightCalculator> _freightMock = new();
    private readonly Mock<IProductAntiCorruptionService> _productAcMock = new();
    private readonly Mock<IPromotionAntiCorruptionService> _promoAcMock = new();
    private readonly Mock<IPointsAntiCorruptionService> _pointsAcMock = new();
    private readonly Mock<ILogisticsTrackingService> _logisticsMock = new();
    private readonly Mock<ILogisticsCompanyRepository> _logisticsRepoMock = new();
    private readonly Mock<IEventBus> _eventBusMock = new();
    private readonly Mock<IBus> _busMock = new();
    private readonly Mock<IOrderSagaOrchestrator> _sagaMock = new();
    private readonly OrderAppService _sut;

    private static readonly Guid OrderId = Guid.NewGuid();
    private static readonly Guid OperatorId = Guid.NewGuid();
    private static readonly Guid PaymentId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid SellerId = Guid.NewGuid();
    private static readonly Guid SkuId = Guid.NewGuid();

    public OrderAppServiceForceCancelTests()
    {
        _sut = new OrderAppService(
            _orderRepoMock.Object, _uowMock.Object, _orderNoGenMock.Object,
            _stockSvcMock.Object, _pricingSvcMock.Object, _freightMock.Object,
            _productAcMock.Object, _promoAcMock.Object, _pointsAcMock.Object,
            _logisticsMock.Object, _logisticsRepoMock.Object,
            _eventBusMock.Object, _busMock.Object, _sagaMock.Object);
    }

    [Fact]
    public async Task ForceCancelAsync_PaidOrder_ShouldPublishRefundViaOutboxNotEventBus()
    {
        // Arrange: 已支付订单
        var order = CreatePaidOrder();
        _orderRepoMock.Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        // Act
        await _sut.ForceCancelAsync(OrderId, OperatorId, new ForceCancelOrderDto { Reason = "测试强制取消" }, CancellationToken.None);

        // Assert: 不再通过 IEventBus 直接发布
        _eventBusMock.Verify(b => b.PublishAsync(It.IsAny<RefundRequestedIntegrationEvent>(), It.IsAny<CancellationToken>()), Times.Never);

        // Assert: 退款事件作为领域事件挂在聚合上（由 Outbox 在 SaveEntitiesAsync 时持久化）
        order.DomainEvents.OfType<RefundRequestedDomainEvent>().Should().HaveCount(1);

        // Assert: SaveEntitiesAsync 调用一次（Outbox 在此时同事务持久化）
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ForceCancelAsync_PendingPaymentOrder_ShouldNotPublishRefund()
    {
        // Arrange: 待支付订单
        var order = CreatePendingPaymentOrder();
        _orderRepoMock.Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        // Act
        await _sut.ForceCancelAsync(OrderId, OperatorId, new ForceCancelOrderDto { Reason = "测试取消" }, CancellationToken.None);

        // Assert: 待支付订单无需退款
        order.DomainEvents.OfType<RefundRequestedDomainEvent>().Should().BeEmpty();
        _eventBusMock.Verify(b => b.PublishAsync(It.IsAny<RefundRequestedIntegrationEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static OrderAggregate CreatePaidOrder()
    {
        var order = CreateBaseOrder("TEST-FC-001");
        order.MarkPaymentInitiated(PaymentMethod.WeChatPay);
        order.MarkAsPaid(PaymentId, "WeChatPay", DateTime.UtcNow, "TEST-TRADE-001", order.TotalAmount);
        return order;
    }

    private static OrderAggregate CreatePendingPaymentOrder()
    {
        return CreateBaseOrder("TEST-FC-002");
    }

    private static OrderAggregate CreateBaseOrder(string orderNo)
    {
        // Order.Create 校验订单明细不可为空，须填充有效 OrderItem
        var snapshot = ProductSnapshot.Create(SkuId, Guid.NewGuid(), "Test Product", "Red-XL", null, SellerId);
        var item = OrderItem.Create(Guid.NewGuid(), SkuId, snapshot, 99.99m, 1, null);
        return OrderAggregate.Create(
            OrderId, orderNo, OrderType.Normal, UserId, SellerId,
            new List<OrderItem> { item }, CreateAddressSnapshot(), 0m, 0m, DateTime.UtcNow.AddMinutes(30));
    }

    private static AddressSnapshot CreateAddressSnapshot() =>
        AddressSnapshot.Create("张三", "13800138000", "北京市", "北京市", "朝阳区", "测试地址");
}
