using Leno.Order.Application.Queries;
using Moq;

namespace Leno.Order.Application.Tests.Queries;

public class OrderDetailQueryHandlerTests
{
    private readonly Mock<IOrderReadModelAccessor> _accessorMock = new();
    private readonly OrderDetailQueryHandler _sut;

    public OrderDetailQueryHandlerTests()
    {
        _sut = new OrderDetailQueryHandler(_accessorMock.Object);
    }

    [Fact]
    public async Task HandleAsync_ShouldDelegateToReadModelAccessorAndReturnResult()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var query = new OrderDetailQuery
        {
            OrderId = orderId,
            CurrentUserId = Guid.NewGuid()
        };

        var detail = new OrderDetailResult
        {
            OrderId = orderId,
            OrderNo = "ORD-002",
            UserId = Guid.NewGuid(),
            SellerId = Guid.NewGuid(),
            OrderType = "Normal",
            ItemsAmount = 200m,
            DiscountAmount = 20m,
            PointsOffsetAmount = 10m,
            FreightAmount = 10m,
            TotalAmount = 180m,
            Currency = "CNY",
            Status = "Shipped",
            CreatedAt = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            PaidAt = new DateTime(2026, 7, 1, 1, 0, 0, DateTimeKind.Utc),
            ShippedAt = new DateTime(2026, 7, 2, 0, 0, 0, DateTimeKind.Utc),
            CompletedAt = null,
            CancelledAt = null,
            Items = new List<OrderItemDto>
            {
                new()
                {
                    SkuId = Guid.NewGuid(),
                    ProductName = "测试商品",
                    SkuName = "红色-XL",
                    UnitPrice = 100m,
                    Quantity = 2,
                    Subtotal = 200m
                }
            }
        };

        _accessorMock
            .Setup(a => a.GetDetailAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(detail);

        // Act
        var result = await _sut.HandleAsync(query);

        // Assert
        result.Should().NotBeNull();
        result!.OrderId.Should().Be(orderId);
        result.OrderNo.Should().Be("ORD-002");
        result.Status.Should().Be("Shipped");
        result.TotalAmount.Should().Be(180m);
        result.Items.Should().HaveCount(1);
        result.Items[0].ProductName.Should().Be("测试商品");
        result.Items[0].Subtotal.Should().Be(200m);

        _accessorMock.Verify(a => a.GetDetailAsync(orderId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenReadModelNotFound_ShouldReturnNull()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var query = new OrderDetailQuery { OrderId = orderId };

        _accessorMock
            .Setup(a => a.GetDetailAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrderDetailResult?)null);

        // Act
        var result = await _sut.HandleAsync(query);

        // Assert
        result.Should().BeNull();
        _accessorMock.Verify(a => a.GetDetailAsync(orderId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_NullQuery_ShouldThrowArgumentNullException()
    {
        var act = () => _sut.HandleAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
