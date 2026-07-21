using Leno.Order.Application.Queries;
using Leno.Order.Domain.Aggregates;
using Leno.Order.Domain.Repositories;
using Leno.Order.Domain.Services;
using Leno.Order.Domain.ValueObjects;
using Moq;
using OrderAggregate = Leno.Order.Domain.Aggregates.Order;
using DomainLogisticsTraceResult = Leno.Order.Domain.ValueObjects.LogisticsTraceResult;
using DomainLogisticsTraceNode = Leno.Order.Domain.ValueObjects.LogisticsTraceNode;

namespace Leno.Order.Application.Tests.Queries;

public class LogisticsTraceQueryHandlerTests
{
    private readonly Mock<IOrderRepository> _orderRepoMock = new();
    private readonly Mock<ILogisticsCompanyRepository> _logisticsCompanyRepoMock = new();
    private readonly Mock<ILogisticsTrackingService> _logisticsTrackingMock = new();
    private readonly LogisticsTraceQueryHandler _sut;

    private static readonly Guid OrderId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid SellerId = Guid.NewGuid();
    private static readonly Guid SkuId = Guid.NewGuid();

    public LogisticsTraceQueryHandlerTests()
    {
        _sut = new LogisticsTraceQueryHandler(
            _orderRepoMock.Object,
            _logisticsCompanyRepoMock.Object,
            _logisticsTrackingMock.Object);
    }

    [Fact]
    public async Task HandleAsync_OrderNotFound_ShouldReturnNull()
    {
        // Arrange
        var query = new LogisticsTraceQuery { OrderId = OrderId };

        _orderRepoMock
            .Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrderAggregate?)null);

        // Act
        var result = await _sut.HandleAsync(query);

        // Assert
        result.Should().BeNull();
        _logisticsTrackingMock.Verify(
            s => s.QueryTraceAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_OrderNotShipped_ShouldReturnEmptyResultWithNullTrackingNo()
    {
        // Arrange：订单为 PendingPayment 态，未填写物流单号
        var order = CreateOrder();
        var query = new LogisticsTraceQuery { OrderId = OrderId };

        _orderRepoMock
            .Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        // Act
        var result = await _sut.HandleAsync(query);

        // Assert
        result.Should().NotBeNull();
        result!.OrderId.Should().Be(OrderId);
        result.TrackingNo.Should().BeNull();
        result.LogisticsCompany.Should().BeNull();
        result.Nodes.Should().BeEmpty();

        _logisticsTrackingMock.Verify(
            s => s.QueryTraceAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_OrderShippedAndCompanySupportsTracking_ShouldDelegateAndMapNodes()
    {
        // Arrange：订单已发货，物流公司支持轨迹查询
        var order = CreateOrder();
        order.MarkPaymentInitiated(PaymentMethod.WeChatPay);
        order.MarkAsPaid(Guid.NewGuid(), "WeChatPay", DateTime.UtcNow, "TRADE-001", order.TotalAmount);
        order.Ship("SF-1234567890", "SF", DateTime.UtcNow, SellerId);

        var query = new LogisticsTraceQuery { OrderId = OrderId };

        _orderRepoMock
            .Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var company = LogisticsCompany.Create(
            Guid.NewGuid(), "顺丰速运", "SF", "95338", supportTracking: true);
        _logisticsCompanyRepoMock
            .Setup(r => r.ListAsync(1, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LogisticsCompany> { company });

        var occurredAt = new DateTime(2026, 7, 19, 8, 30, 0, DateTimeKind.Utc);
        var traceResult = new DomainLogisticsTraceResult(
            "SF-1234567890",
            "SF",
            new List<DomainLogisticsTraceNode>
            {
                new("已签收", occurredAt, "深圳市"),
                new("派送中", occurredAt.AddHours(-2), "深圳市南山区")
            },
            isFromCache: false);

        _logisticsTrackingMock
            .Setup(s => s.QueryTraceAsync("SF-1234567890", "SF", It.IsAny<CancellationToken>()))
            .ReturnsAsync(traceResult);

        // Act
        var result = await _sut.HandleAsync(query);

        // Assert
        result.Should().NotBeNull();
        result!.OrderId.Should().Be(OrderId);
        result.TrackingNo.Should().Be("SF-1234567890");
        result.LogisticsCompany.Should().Be("SF");
        result.Nodes.Should().HaveCount(2);

        var first = result.Nodes[0];
        first.Description.Should().Be("已签收");
        first.Time.Should().Be(occurredAt);
        first.Location.Should().Be("深圳市");

        var second = result.Nodes[1];
        second.Description.Should().Be("派送中");
        second.Location.Should().Be("深圳市南山区");

        _logisticsTrackingMock.Verify(
            s => s.QueryTraceAsync("SF-1234567890", "SF", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_NullQuery_ShouldThrowArgumentNullException()
    {
        var act = () => _sut.HandleAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    private static OrderAggregate CreateOrder()
    {
        var snapshot = ProductSnapshot.Create(SkuId, Guid.NewGuid(), "测试商品", "红色-XL", null, SellerId);
        var item = OrderItem.Create(Guid.NewGuid(), SkuId, snapshot, 99.99m, 1, null);
        return OrderAggregate.Create(
            OrderId, "ORD-LTR-001", OrderType.Normal, UserId, SellerId,
            new List<OrderItem> { item },
            AddressSnapshot.Create("张三", "13800138000", "广东", "深圳", "南山区", "科技园路1号"),
            10m, 0m, DateTime.UtcNow.AddHours(1));
    }
}
