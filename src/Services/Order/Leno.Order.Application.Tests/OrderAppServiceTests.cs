using Leno.Order.Application.DTOs;
using Leno.Order.Application.Messages;
using Leno.Order.Application.Services;
using Leno.Order.Domain.Aggregates;
using Leno.Order.Domain.Events;
using Leno.Order.Domain.Exceptions;
using Leno.Order.Domain.Repositories;
using Leno.Order.Domain.Services;
using Leno.Order.Domain.ValueObjects;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using Leno.Infrastructure.Abstractions;
using MassTransit;
using MassTransit.Scheduling;
using Microsoft.Extensions.Logging;
using Moq;
using OrderAggregate = Leno.Order.Domain.Aggregates.Order;

namespace Leno.Order.Application.Tests;

public class OrderAppServiceTests
{
    private readonly Mock<IOrderRepository> _orderRepoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<IOrderNumberGenerator> _numberGenMock = new();
    private readonly Mock<IStockReservationDomainService> _stockServiceMock = new();
    private readonly Mock<IOrderPricingDomainService> _pricingServiceMock = new();
    private readonly Mock<IFreightCalculator> _freightCalculatorMock = new();
    private readonly Mock<IProductAntiCorruptionService> _productAcMock = new();
    private readonly Mock<IPromotionAntiCorruptionService> _promotionAcMock = new();
    private readonly Mock<IPointsAntiCorruptionService> _pointsAcMock = new();
    private readonly Mock<ILogisticsTrackingService> _logisticsTrackingMock = new();
    private readonly Mock<ILogisticsCompanyRepository> _logisticsCompanyRepoMock = new();
    private readonly Mock<IEventBus> _eventBusMock = new();
    private readonly Mock<IBus> _busMock = new();
    private readonly IOrderSagaOrchestrator _sagaOrchestrator;
    private readonly OrderAppService _sut;

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid SellerId = Guid.NewGuid();
    private static readonly Guid OrderId = Guid.NewGuid();
    private static readonly Guid SkuId = Guid.NewGuid();

    public OrderAppServiceTests()
    {
        // 以共享 Mock 构造真实 Saga 编排器，使 CreateOrderAsync 的断言可直击底层依赖调用
        _sagaOrchestrator = new OrderSagaOrchestrator(
            _orderRepoMock.Object,
            _uowMock.Object,
            _numberGenMock.Object,
            _stockServiceMock.Object,
            _pricingServiceMock.Object,
            _freightCalculatorMock.Object,
            _promotionAcMock.Object,
            _pointsAcMock.Object,
            _busMock.Object,
            new Mock<ILogger<OrderSagaOrchestrator>>().Object,
            Microsoft.Extensions.Options.Options.Create(new Leno.Order.Application.Sagas.OrderSagaOptions()));

        _sut = new OrderAppService(
            _orderRepoMock.Object,
            _uowMock.Object,
            _numberGenMock.Object,
            _stockServiceMock.Object,
            _pricingServiceMock.Object,
            _freightCalculatorMock.Object,
            _productAcMock.Object,
            _promotionAcMock.Object,
            _pointsAcMock.Object,
            _logisticsTrackingMock.Object,
            _logisticsCompanyRepoMock.Object,
            _eventBusMock.Object,
            _busMock.Object,
            _sagaOrchestrator);
    }

    #region GetByIdAsync

    [Fact]
    public async Task GetByIdAsync_ExistingOrder_ShouldReturnDto()
    {
        var order = CreateOrder();
        _orderRepoMock.Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var result = await _sut.GetByIdAsync(OrderId);

        result.Should().NotBeNull();
        result.Id.Should().Be(OrderId);
        result.OrderNo.Should().Be("ORD-001");
    }

    [Fact]
    public async Task GetByIdAsync_NotFound_ShouldThrowException()
    {
        _orderRepoMock.Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrderAggregate?)null);

        var act = () => _sut.GetByIdAsync(OrderId);

        await act.Should().ThrowAsync<OrderDomainException>().WithMessage("*不存在*");
    }

    #endregion

    #region ShipAsync

    [Fact]
    public async Task ShipAsync_ValidInput_ShouldShip()
    {
        var order = CreateOrder();
        order.MarkPaymentInitiated(PaymentMethod.WeChatPay);
        order.MarkAsPaid(Guid.NewGuid(), "WeChatPay", DateTime.UtcNow, "T001", order.TotalAmount);
        _orderRepoMock.Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        _logisticsCompanyRepoMock.Setup(r => r.GetByCodeAsync("SF", It.IsAny<CancellationToken>()))
            .ReturnsAsync(LogisticsCompany.Create(Guid.NewGuid(), "顺丰速运", "SF", null, true));

        await _sut.ShipAsync(OrderId, SellerId, new ShipOrderDto { LogisticsNo = "SF123", LogisticsCompanyCode = "SF" });

        order.Status.Should().Be(OrderStatus.Shipped);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ShipAsync_NotFound_ShouldThrowException()
    {
        _orderRepoMock.Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrderAggregate?)null);

        var act = () => _sut.ShipAsync(OrderId, Guid.NewGuid(), new ShipOrderDto { LogisticsNo = "SF123" });

        await act.Should().ThrowAsync<OrderDomainException>().WithMessage("*不存在*");
    }

    #endregion

    #region ConfirmReceiptAsync

    [Fact]
    public async Task ConfirmReceiptAsync_Valid_ShouldComplete()
    {
        var order = CreateOrder();
        order.MarkPaymentInitiated(PaymentMethod.WeChatPay);
        order.MarkAsPaid(Guid.NewGuid(), "WeChatPay", DateTime.UtcNow, "T001", order.TotalAmount);
        order.Ship("SF123", "SF", DateTime.UtcNow, Guid.NewGuid());
        _orderRepoMock.Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        await _sut.ConfirmReceiptAsync(OrderId, UserId);

        order.Status.Should().Be(OrderStatus.Completed);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConfirmReceiptAsync_WrongUser_ShouldThrowException()
    {
        var order = CreateOrder();
        order.MarkPaymentInitiated(PaymentMethod.WeChatPay);
        order.MarkAsPaid(Guid.NewGuid(), "WeChatPay", DateTime.UtcNow, "T001", order.TotalAmount);
        order.Ship("SF123", "SF", DateTime.UtcNow, Guid.NewGuid());
        _orderRepoMock.Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var act = () => _sut.ConfirmReceiptAsync(OrderId, Guid.NewGuid());

        await act.Should().ThrowAsync<OrderDomainException>().WithMessage("*无权*");
    }

    #endregion

    #region CancelAsync

    [Fact]
    public async Task CancelAsync_Valid_ShouldCancel()
    {
        var order = CreateOrder();
        _orderRepoMock.Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        _stockServiceMock.Setup(s => s.ReleaseBatchAsync(OrderId, It.IsAny<Dictionary<Guid, int>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _sut.CancelAsync(OrderId, UserId, new CancelOrderDto { Reason = "Changed mind" });

        order.Status.Should().Be(OrderStatus.Cancelled);
        _promotionAcMock.Verify(p => p.ReleaseCouponsAsync(OrderId, It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancelAsync_WrongUser_ShouldThrowException()
    {
        var order = CreateOrder();
        _orderRepoMock.Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var act = () => _sut.CancelAsync(OrderId, Guid.NewGuid(), new CancelOrderDto { Reason = "test" });

        await act.Should().ThrowAsync<OrderDomainException>().WithMessage("*无权*");
    }

    [Fact]
    public async Task CancelAsync_Should_SaveEntities_First_Then_Release_Resources()
    {
        // Arrange
        var order = CreateOrder();
        _orderRepoMock.Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        // 记录 SaveEntitiesAsync 与 ReleaseBatchAsync 的调用顺序
        var callOrder = new List<string>();
        _uowMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()))
            .Returns(() => { callOrder.Add("SaveEntitiesAsync"); return Task.FromResult(true); });
        _stockServiceMock.Setup(s => s.ReleaseBatchAsync(It.IsAny<Guid>(), It.IsAny<Dictionary<Guid, int>>(), It.IsAny<CancellationToken>()))
            .Returns(() => { callOrder.Add("ReleaseStock"); return Task.CompletedTask; });

        var dto = new CancelOrderDto { Reason = "test" };

        // Act
        await _sut.CancelAsync(OrderId, UserId, dto, CancellationToken.None);

        // Assert：SaveEntitiesAsync 应在 ReleaseBatchAsync 之前执行
        callOrder.IndexOf("SaveEntitiesAsync").Should().BeLessThan(callOrder.IndexOf("ReleaseStock"));
    }

    [Fact]
    public async Task CancelAsync_Should_Publish_OrderCancelledEvent_Via_Outbox()
    {
        // Arrange
        var order = CreateOrder();
        _orderRepoMock.Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var dto = new CancelOrderDto { Reason = "test" };

        // Act
        await _sut.CancelAsync(OrderId, UserId, dto, CancellationToken.None);

        // Assert：订单聚合应包含 OrderCancelledDomainEvent（经 Outbox 同事务持久化）
        order.Status.Should().Be(OrderStatus.Cancelled);
        order.DomainEvents.Should().Contain(e => e is OrderCancelledDomainEvent);
        _orderRepoMock.Verify(r => r.UpdateAsync(order, It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region ForceCancelAsync

    [Fact]
    public async Task ForceCancelAsync_Valid_ShouldForceCancel()
    {
        var order = CreateOrder();
        order.MarkPaymentInitiated(PaymentMethod.WeChatPay);
        order.MarkAsPaid(Guid.NewGuid(), "WeChatPay", DateTime.UtcNow, "T001", order.TotalAmount);
        _orderRepoMock.Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        _stockServiceMock.Setup(s => s.ReleaseBatchAsync(OrderId, It.IsAny<Dictionary<Guid, int>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _sut.ForceCancelAsync(OrderId, Guid.NewGuid(), new ForceCancelOrderDto { Reason = "Fraudulent" });

        order.Status.Should().Be(OrderStatus.Cancelled);
        _promotionAcMock.Verify(p => p.ReleaseCouponsAsync(OrderId, It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region QueryAsync

    [Fact]
    public async Task QueryAsync_ShouldReturnPagedResult()
    {
        var order = CreateOrder();
        _orderRepoMock.Setup(r => r.QueryAsync(UserId, null, null, null, null, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OrderAggregate> { order });
        _orderRepoMock.Setup(r => r.CountAsync(UserId, null, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var result = await _sut.QueryAsync(UserId, null, null, 1, 20);

        result.Items.Should().HaveCount(1);
        result.Total.Should().Be(1);
    }

    #endregion

    #region PayAsync

    [Fact]
    public async Task PayAsync_Valid_ShouldInitiatePaymentAndSaveWithOutbox()
    {
        var order = CreateOrder();
        _orderRepoMock.Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        await _sut.PayAsync(OrderId, UserId, new PayOrderDto { PaymentMethod = PaymentMethod.WeChatPay });

        // 聚合标记已置位，领域事件含 PaymentRequestedDomainEvent（经 Outbox 同事务发布）
        order.PaymentInitiated.Should().BeTrue();
        order.PaymentMethod.Should().Be(PaymentMethod.WeChatPay);
        order.DomainEvents.Should().Contain(e => e is PaymentRequestedDomainEvent);

        // 不再直接调用 _eventBus.PublishAsync；经 Outbox 持久化
        _eventBusMock.Verify(e => e.PublishAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never);
        _orderRepoMock.Verify(r => r.UpdateAsync(order, It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PayAsync_AlreadyInitiated_ShouldThrowAndNotSave()
    {
        var order = CreateOrder();
        order.MarkPaymentInitiated(PaymentMethod.WeChatPay);
        _orderRepoMock.Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var act = () => _sut.PayAsync(OrderId, UserId, new PayOrderDto { PaymentMethod = PaymentMethod.Alipay });

        await act.Should().ThrowAsync<OrderDomainException>().WithMessage("*已发起*");
        _eventBusMock.Verify(e => e.PublishAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PayAsync_WrongUser_ShouldThrowException()
    {
        var order = CreateOrder();
        _orderRepoMock.Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var act = () => _sut.PayAsync(OrderId, Guid.NewGuid(), new PayOrderDto { PaymentMethod = PaymentMethod.WeChatPay });

        await act.Should().ThrowAsync<OrderDomainException>().WithMessage("*无权*");
    }

    [Fact]
    public async Task PayAsync_NotPendingPayment_ShouldThrowException()
    {
        var order = CreateOrder();
        order.MarkPaymentInitiated(PaymentMethod.WeChatPay);
        order.MarkAsPaid(Guid.NewGuid(), "WeChatPay", DateTime.UtcNow, "T001", order.TotalAmount);
        _orderRepoMock.Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var act = () => _sut.PayAsync(OrderId, UserId, new PayOrderDto { PaymentMethod = PaymentMethod.WeChatPay });

        await act.Should().ThrowAsync<OrderDomainException>().WithMessage("*状态*");
    }

    #endregion

    #region CreateOrderAsync

    [Fact]
    public async Task CreateOrderAsync_Valid_ShouldScheduleTimeoutMessage()
    {
        // Arrange
        var skuInfo = new SkuInfo
        {
            SkuId = SkuId,
            SpuId = Guid.NewGuid(),
            SellerId = SellerId,
            ProductName = "Test Product",
            SkuName = "Red-XL",
            UnitPrice = 99.99m,
            IsOnSale = true
        };
        _productAcMock.Setup(p => p.GetSkuInfoAsync(SkuId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(skuInfo);
        _pricingServiceMock.Setup(p => p.ValidatePricesAsync(It.IsAny<List<(Guid, decimal)>>(), It.IsAny<IReadOnlyDictionary<Guid, decimal>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _promotionAcMock.Setup(p => p.CalculateDiscountAsync(UserId, It.IsAny<List<(Guid, decimal)>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);
        _freightCalculatorMock.Setup(f => f.CalculateAsync(SellerId, It.IsAny<string>(), 1, 99.99m, It.IsAny<CancellationToken>()))
            .ReturnsAsync(10m);
        _stockServiceMock.Setup(s => s.ReserveBatchAsync(It.IsAny<Guid>(), It.IsAny<Dictionary<Guid, int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _numberGenMock.Setup(n => n.GenerateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("ORD-001");

        var dto = new CreateOrderDto
        {
            Items = new List<CheckoutItemDto>
            {
                new() { SkuId = SkuId, Quantity = 1 }
            },
            RecipientName = "张三",
            RecipientPhone = "13800138000",
            Province = "广东",
            City = "深圳",
            District = "南山区",
            Detail = "科技园路1号"
        };

        // Act
        var result = await _sut.CreateOrderAsync(UserId, dto);

        // Assert
        result.Should().NotBeNull();
        result.OrderNo.Should().Be("ORD-001");
        // ScheduleSend internally calls Publish<ScheduleMessage> on the bus
        _busMock.Verify(
            b => b.Publish(It.IsAny<ScheduleMessage>(), It.IsAny<IPipe<PublishContext<ScheduleMessage>>>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task CreateOrderAsync_PointsFreezeFails_ShouldReleaseStockAndNotPersistOrder()
    {
        // Arrange — 单卖家 + 使用积分触发 FreezeAsync
        var skuInfo = new SkuInfo
        {
            SkuId = SkuId,
            SpuId = Guid.NewGuid(),
            SellerId = SellerId,
            ProductName = "Test Product",
            SkuName = "Red-XL",
            UnitPrice = 99.99m,
            IsOnSale = true
        };
        _productAcMock.Setup(p => p.GetSkuInfoAsync(SkuId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(skuInfo);
        _pricingServiceMock.Setup(p => p.ValidatePricesAsync(It.IsAny<List<(Guid, decimal)>>(), It.IsAny<IReadOnlyDictionary<Guid, decimal>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _promotionAcMock.Setup(p => p.CalculateDiscountAsync(UserId, It.IsAny<List<(Guid, decimal)>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);
        _freightCalculatorMock.Setup(f => f.CalculateAsync(SellerId, It.IsAny<string>(), 1, 99.99m, It.IsAny<CancellationToken>()))
            .ReturnsAsync(10m);
        _stockServiceMock.Setup(s => s.ReserveBatchAsync(It.IsAny<Guid>(), It.IsAny<Dictionary<Guid, int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _pointsAcMock.Setup(p => p.FreezeAsync(UserId, It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OrderDomainException("积分域冻结失败", "ORDER_POINTS_FREEZE_FAILED"));

        var dto = new CreateOrderDto
        {
            Items = new List<CheckoutItemDto>
            {
                new() { SkuId = SkuId, Quantity = 1 }
            },
            PointsToUse = 100, // 触发 FreezeAsync
            RecipientName = "张三",
            RecipientPhone = "13800138000",
            Province = "广东",
            City = "深圳",
            District = "南山区",
            Detail = "科技园路1号"
        };

        // Act
        var act = () => _sut.CreateOrderAsync(UserId, dto);

        // Assert — 积分冻结失败须回滚已预占库存、不持久化订单、异常向上抛
        await act.Should().ThrowAsync<OrderDomainException>().WithMessage("*积分域冻结失败*");

        _stockServiceMock.Verify(
            s => s.ReserveBatchAsync(It.IsAny<Guid>(), It.IsAny<Dictionary<Guid, int>>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _stockServiceMock.Verify(
            s => s.ReleaseBatchAsync(It.IsAny<Guid>(), It.IsAny<Dictionary<Guid, int>>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _pointsAcMock.Verify(
            p => p.FreezeAsync(UserId, It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _orderRepoMock.Verify(
            r => r.AddAsync(It.IsAny<OrderAggregate>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateOrderAsync_MultiSellerSecondGroupReserveFails_ShouldCompensateFirstGroupAndNotPersist()
    {
        // Arrange — 两卖家拆单：第一组（卖家1）成功，第二组（卖家2）库存预占失败
        var sellerId2 = Guid.NewGuid();
        var skuId2 = Guid.NewGuid();
        var skuInfo1 = new SkuInfo
        {
            SkuId = SkuId,
            SpuId = Guid.NewGuid(),
            SellerId = SellerId,
            ProductName = "Product A",
            SkuName = "A-XL",
            UnitPrice = 100m,
            IsOnSale = true
        };
        var skuInfo2 = new SkuInfo
        {
            SkuId = skuId2,
            SpuId = Guid.NewGuid(),
            SellerId = sellerId2,
            ProductName = "Product B",
            SkuName = "B-M",
            UnitPrice = 50m,
            IsOnSale = true
        };
        _productAcMock.Setup(p => p.GetSkuInfoAsync(SkuId, It.IsAny<CancellationToken>())).ReturnsAsync(skuInfo1);
        _productAcMock.Setup(p => p.GetSkuInfoAsync(skuId2, It.IsAny<CancellationToken>())).ReturnsAsync(skuInfo2);
        _pricingServiceMock.Setup(p => p.ValidatePricesAsync(It.IsAny<List<(Guid, decimal)>>(), It.IsAny<IReadOnlyDictionary<Guid, decimal>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        // 第一组涉及优惠（discount > 0），补偿时须释放优惠券
        _promotionAcMock.Setup(p => p.CalculateDiscountAsync(UserId, It.IsAny<List<(Guid, decimal)>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(10m);
        _pricingServiceMock.Setup(p => p.CalculateAndAllocateAsync(It.IsAny<decimal>(), It.IsAny<List<(Guid, decimal)>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<(Guid SkuId, decimal Allocation)> { (SkuId, 10m) });
        _freightCalculatorMock.Setup(f => f.CalculateAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(10m);
        // 第一组预占成功，第二组预占失败（顺序触发）
        _stockServiceMock.SetupSequence(s => s.ReserveBatchAsync(It.IsAny<Guid>(), It.IsAny<Dictionary<Guid, int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .ReturnsAsync(false);
        _pointsAcMock.Setup(p => p.FreezeAsync(UserId, It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _numberGenMock.Setup(n => n.GenerateAsync(It.IsAny<CancellationToken>())).ReturnsAsync("ORD-001");

        var dto = new CreateOrderDto
        {
            Items = new List<CheckoutItemDto>
            {
                new() { SkuId = SkuId, Quantity = 1 },
                new() { SkuId = skuId2, Quantity = 1 }
            },
            PointsToUse = 100, // 触发积分冻结，验证补偿释放积分
            RecipientName = "张三",
            RecipientPhone = "13800138000",
            Province = "广东",
            City = "深圳",
            District = "南山区",
            Detail = "科技园路1号"
        };

        // Act
        var act = () => _sut.CreateOrderAsync(UserId, dto);

        // Assert — 第二组失败须补偿第一组：释放库存/积分/优惠券；订单未持久化；抛原始异常
        // P1-T24：并行阶段聚合未入仓储（DbContext 非线程安全），失败时聚合仅存在于内存，无需 RemoveAsync
        await act.Should().ThrowAsync<OrderDomainException>().WithMessage("*库存预占失败*");

        _stockServiceMock.Verify(
            s => s.ReserveBatchAsync(It.IsAny<Guid>(), It.IsAny<Dictionary<Guid, int>>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
        _stockServiceMock.Verify(
            s => s.ReleaseBatchAsync(It.IsAny<Guid>(), It.IsAny<Dictionary<Guid, int>>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _pointsAcMock.Verify(
            p => p.FreezeAsync(UserId, It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _pointsAcMock.Verify(
            p => p.ReleaseAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _promotionAcMock.Verify(
            p => p.ReleaseCouponsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _orderRepoMock.Verify(
            r => r.RemoveAsync(It.IsAny<OrderAggregate>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region GetLogisticsTraceAsync

    [Fact]
    public async Task GetLogisticsTraceAsync_NoLogisticsNo_ShouldReturnEmpty()
    {
        var order = CreateOrder();
        _orderRepoMock.Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var result = await _sut.GetLogisticsTraceAsync(OrderId);

        result.LogisticsNo.Should().BeEmpty();
        result.Nodes.Should().BeEmpty();
    }

    [Fact]
    public async Task GetLogisticsTraceAsync_NoCompanyCode_ShouldReturnWarning()
    {
        var order = CreateOrder();
        order.MarkPaymentInitiated(PaymentMethod.WeChatPay);
        order.MarkAsPaid(Guid.NewGuid(), "WeChatPay", DateTime.UtcNow, "T001", order.TotalAmount);
        order.Ship("SF123", "SF", DateTime.UtcNow, Guid.NewGuid());
        // Reset company code to simulate missing
        typeof(OrderAggregate).GetProperty("LogisticsCompanyCode")!.SetValue(order, null);
        _orderRepoMock.Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var result = await _sut.GetLogisticsTraceAsync(OrderId);

        result.LogisticsNo.Should().Be("SF123");
        result.HasWarning.Should().BeTrue();
    }

    [Fact]
    public async Task GetLogisticsTraceAsync_CompanyNotSupportTracking_ShouldReturnWarning()
    {
        var order = CreateOrder();
        order.MarkPaymentInitiated(PaymentMethod.WeChatPay);
        order.MarkAsPaid(Guid.NewGuid(), "WeChatPay", DateTime.UtcNow, "T001", order.TotalAmount);
        order.Ship("SF123", "SF", DateTime.UtcNow, Guid.NewGuid());
        _orderRepoMock.Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        _logisticsCompanyRepoMock.Setup(r => r.GetByCodeAsync("SF", It.IsAny<CancellationToken>()))
            .ReturnsAsync(LogisticsCompany.Create(Guid.NewGuid(), "顺丰", "SF", null, false));

        var result = await _sut.GetLogisticsTraceAsync(OrderId);

        result.LogisticsNo.Should().Be("SF123");
        result.HasWarning.Should().BeTrue();
    }

    [Fact]
    public async Task GetLogisticsTraceAsync_CompanyDisabled_ShouldReturnWarning()
    {
        var order = CreateOrder();
        order.MarkPaymentInitiated(PaymentMethod.WeChatPay);
        order.MarkAsPaid(Guid.NewGuid(), "WeChatPay", DateTime.UtcNow, "T001", order.TotalAmount);
        order.Ship("SF123", "SF", DateTime.UtcNow, Guid.NewGuid());
        _orderRepoMock.Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        var company = LogisticsCompany.Create(Guid.NewGuid(), "顺丰", "SF", null, true);
        company.Disable();
        _logisticsCompanyRepoMock.Setup(r => r.GetByCodeAsync("SF", It.IsAny<CancellationToken>()))
            .ReturnsAsync(company);

        var result = await _sut.GetLogisticsTraceAsync(OrderId);

        result.LogisticsNo.Should().Be("SF123");
        result.HasWarning.Should().BeTrue();
    }

    [Fact]
    public async Task GetLogisticsTraceAsync_Valid_ShouldReturnTrace()
    {
        var order = CreateOrder();
        order.MarkPaymentInitiated(PaymentMethod.WeChatPay);
        order.MarkAsPaid(Guid.NewGuid(), "WeChatPay", DateTime.UtcNow, "T001", order.TotalAmount);
        order.Ship("SF123", "SF", DateTime.UtcNow, Guid.NewGuid());
        _orderRepoMock.Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        _logisticsCompanyRepoMock.Setup(r => r.GetByCodeAsync("SF", It.IsAny<CancellationToken>()))
            .ReturnsAsync(LogisticsCompany.Create(Guid.NewGuid(), "顺丰", "SF", null, true));
        _logisticsTrackingMock.Setup(t => t.QueryTraceAsync("SF123", "SF", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LogisticsTraceResult("SF123", "SF", new List<LogisticsTraceNode>
            {
                new("已揽收", DateTime.UtcNow, "深圳")
            }, false));

        var result = await _sut.GetLogisticsTraceAsync(OrderId);

        result.LogisticsNo.Should().Be("SF123");
        result.CompanyCode.Should().Be("SF");
        result.Nodes.Should().HaveCount(1);
        result.IsFromCache.Should().BeFalse();
        result.HasWarning.Should().BeFalse();
    }

    [Fact]
    public async Task GetLogisticsTraceAsync_FromCache_ShouldIndicateCache()
    {
        var order = CreateOrder();
        order.MarkPaymentInitiated(PaymentMethod.WeChatPay);
        order.MarkAsPaid(Guid.NewGuid(), "WeChatPay", DateTime.UtcNow, "T001", order.TotalAmount);
        order.Ship("SF123", "SF", DateTime.UtcNow, Guid.NewGuid());
        _orderRepoMock.Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        _logisticsCompanyRepoMock.Setup(r => r.GetByCodeAsync("SF", It.IsAny<CancellationToken>()))
            .ReturnsAsync(LogisticsCompany.Create(Guid.NewGuid(), "顺丰", "SF", null, true));
        _logisticsTrackingMock.Setup(t => t.QueryTraceAsync("SF123", "SF", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LogisticsTraceResult("SF123", "SF", new List<LogisticsTraceNode>
            {
                new("已揽收", DateTime.UtcNow, "深圳")
            }, true));

        var result = await _sut.GetLogisticsTraceAsync(OrderId);

        result.IsFromCache.Should().BeTrue();
    }

    [Fact]
    public async Task GetLogisticsTraceAsync_NotFound_ShouldThrowException()
    {
        _orderRepoMock.Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrderAggregate?)null);

        var act = () => _sut.GetLogisticsTraceAsync(OrderId);

        await act.Should().ThrowAsync<OrderDomainException>().WithMessage("*不存在*");
    }

    #endregion

    private static OrderAggregate CreateOrder()
    {
        var snapshot = ProductSnapshot.Create(SkuId, Guid.NewGuid(), "Test Product", "Red-XL", null, SellerId);
        var item = OrderItem.Create(Guid.NewGuid(), SkuId, snapshot, 99.99m, 1, null);
        return OrderAggregate.Create(
            OrderId, "ORD-001", OrderType.Normal, UserId, SellerId,
            new List<OrderItem> { item }, CreateAddress(), 10m, 0m, DateTime.UtcNow.AddHours(1));
    }

    private static AddressSnapshot CreateAddress()
    {
        return AddressSnapshot.Create("张三", "13800138000", "广东", "深圳", "南山区", "科技园路1号");
    }
}