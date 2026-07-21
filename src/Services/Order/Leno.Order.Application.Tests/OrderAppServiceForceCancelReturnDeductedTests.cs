using Leno.Order.Application.DTOs;
using Leno.Order.Application.Services;
using Leno.Order.Domain.Aggregates;
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
/// ForceCancel 在 Paid/Shipped 状态下应调用 ReturnDeductedBatchAsync 而非 ReleaseBatchAsync 的单元测试。
/// 验证 P0-T2 修复：已发货订单强制取消时归还已扣减库存，而非释放预占（预占已被删除，释放无效）。
/// </summary>
public class OrderAppServiceForceCancelReturnDeductedTests
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

    public OrderAppServiceForceCancelReturnDeductedTests()
    {
        _sut = new OrderAppService(
            _orderRepoMock.Object, _uowMock.Object, _orderNoGenMock.Object,
            _stockSvcMock.Object, _pricingSvcMock.Object, _freightMock.Object,
            _productAcMock.Object, _promoAcMock.Object, _pointsAcMock.Object,
            _logisticsMock.Object, _logisticsRepoMock.Object,
            _eventBusMock.Object, _busMock.Object, _sagaMock.Object);
    }

    [Fact]
    public async Task ForceCancelAsync_ShippedOrder_Should_Call_ReturnDeducted_Not_Release()
    {
        // Arrange
        var order = CreatePaidAndShippedOrder();
        _orderRepoMock.Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        _stockSvcMock.Setup(s => s.ReturnDeductedBatchAsync(It.IsAny<Guid>(), It.IsAny<Dictionary<Guid, int>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _stockSvcMock.Setup(s => s.ReleaseBatchAsync(It.IsAny<Guid>(), It.IsAny<Dictionary<Guid, int>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var dto = new ForceCancelOrderDto { Reason = "test", OperatorId = OperatorId };

        // Act
        await _sut.ForceCancelAsync(OrderId, dto.OperatorId, dto, CancellationToken.None);

        // Assert：Shipped 状态应调用 ReturnDeductedBatchAsync，不调用 ReleaseBatchAsync
        _stockSvcMock.Verify(
            s => s.ReturnDeductedBatchAsync(OrderId, It.IsAny<Dictionary<Guid, int>>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _stockSvcMock.Verify(
            s => s.ReleaseBatchAsync(OrderId, It.IsAny<Dictionary<Guid, int>>(), It.IsAny<CancellationToken>()),
            Times.Never);
        order.Status.Should().Be(OrderStatus.Cancelled);
    }

    [Fact]
    public async Task ForceCancelAsync_PaidOrder_Should_Call_ReturnDeducted_Not_Release()
    {
        // Arrange
        var order = CreatePaidOrder();
        _orderRepoMock.Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        _stockSvcMock.Setup(s => s.ReturnDeductedBatchAsync(It.IsAny<Guid>(), It.IsAny<Dictionary<Guid, int>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _stockSvcMock.Setup(s => s.ReleaseBatchAsync(It.IsAny<Guid>(), It.IsAny<Dictionary<Guid, int>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var dto = new ForceCancelOrderDto { Reason = "test", OperatorId = OperatorId };

        // Act
        await _sut.ForceCancelAsync(OrderId, dto.OperatorId, dto, CancellationToken.None);

        // Assert：Paid 状态（已确认扣减）应调用 ReturnDeductedBatchAsync
        _stockSvcMock.Verify(
            s => s.ReturnDeductedBatchAsync(OrderId, It.IsAny<Dictionary<Guid, int>>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _stockSvcMock.Verify(
            s => s.ReleaseBatchAsync(It.IsAny<Guid>(), It.IsAny<Dictionary<Guid, int>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ForceCancelAsync_PendingPaymentOrder_Should_Call_Release_Not_ReturnDeducted()
    {
        // Arrange
        var order = CreatePendingPaymentOrder();
        _orderRepoMock.Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        _stockSvcMock.Setup(s => s.ReleaseBatchAsync(It.IsAny<Guid>(), It.IsAny<Dictionary<Guid, int>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _stockSvcMock.Setup(s => s.ReturnDeductedBatchAsync(It.IsAny<Guid>(), It.IsAny<Dictionary<Guid, int>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var dto = new ForceCancelOrderDto { Reason = "test", OperatorId = OperatorId };

        // Act
        await _sut.ForceCancelAsync(OrderId, dto.OperatorId, dto, CancellationToken.None);

        // Assert：PendingPayment 状态（仅预占未扣减）应调用 ReleaseBatchAsync
        _stockSvcMock.Verify(
            s => s.ReleaseBatchAsync(OrderId, It.IsAny<Dictionary<Guid, int>>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _stockSvcMock.Verify(
            s => s.ReturnDeductedBatchAsync(It.IsAny<Guid>(), It.IsAny<Dictionary<Guid, int>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static OrderAggregate CreatePaidOrder()
    {
        var order = CreateBaseOrder("TEST-FC-PAID");
        order.MarkPaymentInitiated(PaymentMethod.WeChatPay);
        order.MarkAsPaid(PaymentId, "WeChatPay", DateTime.UtcNow, "TEST-TRADE-001", order.TotalAmount);
        return order;
    }

    private static OrderAggregate CreatePaidAndShippedOrder()
    {
        var order = CreatePaidOrder();
        order.Ship("SF123456", "SF", DateTime.UtcNow, Guid.NewGuid());
        return order;
    }

    private static OrderAggregate CreatePendingPaymentOrder()
    {
        return CreateBaseOrder("TEST-FC-PENDING");
    }

    private static OrderAggregate CreateBaseOrder(string orderNo)
    {
        var snapshot = ProductSnapshot.Create(SkuId, Guid.NewGuid(), "Test Product", "Red-XL", null, SellerId);
        var item = OrderItem.Create(Guid.NewGuid(), SkuId, snapshot, 99.99m, 1, null);
        return OrderAggregate.Create(
            OrderId, orderNo, OrderType.Normal, UserId, SellerId,
            new List<OrderItem> { item }, CreateAddressSnapshot(), 0m, 0m, DateTime.UtcNow.AddMinutes(30));
    }

    private static AddressSnapshot CreateAddressSnapshot() =>
        AddressSnapshot.Create("张三", "13800138000", "北京市", "北京市", "朝阳区", "测试地址");
}
