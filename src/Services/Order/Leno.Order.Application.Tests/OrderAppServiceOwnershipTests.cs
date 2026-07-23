using Leno.Order.Application.DTOs;
using Leno.Order.Application.Services;
using Leno.Order.Domain.Aggregates;
using Leno.Order.Domain.Exceptions;
using Leno.Order.Domain.Repositories;
using Leno.Order.Domain.Services;
using Leno.Order.Domain.ValueObjects;
using Leno.Infrastructure.Abstractions;
using Leno.SharedKernel.Abstractions;
using MassTransit;
using Moq;
using OrderAggregate = Leno.Order.Domain.Aggregates.Order;

namespace Leno.Order.Application.Tests;

/// <summary>
/// OrderAppService.ShipAsync 卖家归属越权校验单元测试。
/// 验证非归属卖家发货被拒（抛 ORDER_NOT_OWNED），归属卖家发货成功。
/// </summary>
public class OrderAppServiceOwnershipTests
{
    // 复用 OrderAppServiceForceCancelTests 的 Mock 装配模式
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
    private static readonly Guid OwnerSellerId = Guid.NewGuid();
    private static readonly Guid OtherSellerId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid SkuId = Guid.NewGuid();

    public OrderAppServiceOwnershipTests()
    {
        _sut = new OrderAppService(
            _orderRepoMock.Object, _uowMock.Object, _orderNoGenMock.Object,
            _stockSvcMock.Object, _pricingSvcMock.Object, _freightMock.Object,
            _productAcMock.Object, _promoAcMock.Object, _pointsAcMock.Object,
            _logisticsMock.Object, _logisticsRepoMock.Object,
            _eventBusMock.Object, _busMock.Object, _sagaMock.Object);
    }

    [Fact]
    public async Task ShipAsync_NonOwnerSeller_ShouldThrow403()
    {
        // Arrange: 订单归属 OwnerSellerId，但调用方是 OtherSellerId
        var order = CreatePaidOrder(OwnerSellerId);
        _orderRepoMock.Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var dto = new ShipOrderDto { LogisticsNo = "SF1234567890", LogisticsCompanyCode = "SF" };

        // Act & Assert: 非归属卖家应抛 OrderDomainException
        var act = () => _sut.ShipAsync(OrderId, OtherSellerId, dto, CancellationToken.None);
        await act.Should().ThrowAsync<OrderDomainException>()
            .WithMessage("*无权操作*")
            .Where(ex => ex.ErrorCode == "ORDER_NOT_OWNED");

        // 确保未变更订单状态
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ShipAsync_OwnerSeller_ShouldSucceed()
    {
        // Arrange: 订单归属 OwnerSellerId，调用方也是 OwnerSellerId
        var order = CreatePaidOrder(OwnerSellerId);
        _orderRepoMock.Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        _logisticsRepoMock.Setup(r => r.GetByCodeAsync("SF", It.IsAny<CancellationToken>()))
            .ReturnsAsync(LogisticsCompany.Create(Guid.NewGuid(), "顺丰速运", "SF", null, true));

        var dto = new ShipOrderDto { LogisticsNo = "SF1234567890", LogisticsCompanyCode = "SF" };

        // Act
        await _sut.ShipAsync(OrderId, OwnerSellerId, dto, CancellationToken.None);

        // Assert
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private static OrderAggregate CreatePaidOrder(Guid sellerId)
    {
        // Order.Create 校验订单明细不可为空，须填充有效 OrderItem
        var snapshot = ProductSnapshot.Create(SkuId, Guid.NewGuid(), "Test Product", "Red-XL", null, sellerId);
        var item = OrderItem.Create(Guid.NewGuid(), SkuId, snapshot, 99.99m, 1, null);
        var order = OrderAggregate.Create(
            OrderId, "TEST-SHIP-001", OrderType.Normal, UserId, sellerId,
            new List<OrderItem> { item }, CreateAddress(), 0m, 0m, DateTime.UtcNow.AddMinutes(30));
        order.MarkPaymentInitiated(PaymentMethod.WeChatPay);
        order.MarkAsPaid(Guid.NewGuid(), "WeChatPay", DateTime.UtcNow, "TEST-TRADE", order.TotalAmount);
        return order;
    }

    private static AddressSnapshot CreateAddress() =>
        AddressSnapshot.Create("张三", "13800138000", "北京市", "北京市", "朝阳区", "测试地址");
}
