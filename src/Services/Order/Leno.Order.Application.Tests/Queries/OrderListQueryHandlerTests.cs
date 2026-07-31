using Leno.Order.Application.Queries;
using Leno.SharedContracts.Responses;
using Moq;

namespace Leno.Order.Application.Tests.Queries;

public class OrderListQueryHandlerTests
{
    private readonly Mock<IOrderReadModelAccessor> _accessorMock = new();
    private readonly OrderListQueryHandler _sut;

    public OrderListQueryHandlerTests()
    {
        _sut = new OrderListQueryHandler(_accessorMock.Object);
    }

    [Fact]
    public async Task HandleAsync_ShouldDelegateToReadModelAccessorAndReturnResult()
    {
        // Arrange
        var query = new OrderListQuery(1, 20)
        {
            UserId = Guid.NewGuid(),
            SellerId = Guid.NewGuid(),
            Status = "Paid",
            StartDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 7, 19, 0, 0, 0, DateTimeKind.Utc)
        };

        var summary = new OrderSummaryDto
        {
            OrderId = Guid.NewGuid(),
            OrderNo = "ORD-001",
            UserId = query.UserId!.Value,
            SellerId = query.SellerId,
            TotalAmount = 199.00m,
            Currency = "CNY",
            Status = "Paid",
            CreatedAt = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            PaidAt = new DateTime(2026, 7, 1, 1, 0, 0, DateTimeKind.Utc),
            ShippedAt = null
        };

        var expectedResult = new PageResult<OrderSummaryDto>(
            new List<OrderSummaryDto> { summary },
            1,
            1,
            20);

        _accessorMock
            .Setup(a => a.ListAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _sut.HandleAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(1);
        result.Total.Should().Be(1);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(20);

        var first = result.Items[0];
        first.OrderId.Should().Be(summary.OrderId);
        first.OrderNo.Should().Be("ORD-001");
        first.Status.Should().Be("Paid");
        first.TotalAmount.Should().Be(199.00m);
        first.Currency.Should().Be("CNY");

        _accessorMock.Verify(a => a.ListAsync(query, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_EmptyResult_ShouldReturnEmptyItems()
    {
        // Arrange
        var query = new OrderListQuery(6, 10);

        var expectedResult = new PageResult<OrderSummaryDto>(
            new List<OrderSummaryDto>(),
            0,
            6,
            10);

        _accessorMock
            .Setup(a => a.ListAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _sut.HandleAsync(query);

        // Assert
        result.Items.Should().BeEmpty();
        result.Total.Should().Be(0);
        result.Page.Should().Be(6);
        result.PageSize.Should().Be(10);
    }

    [Fact]
    public async Task HandleAsync_NullQuery_ShouldThrowArgumentNullException()
    {
        var act = () => _sut.HandleAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task HandleAsync_WithOrderNo_ShouldPassOrderNoToReadModelAccessor()
    {
        // Arrange：构造带 OrderNo 的查询，捕获实际透传给 IOrderReadModelAccessor 的参数，
        // 验证 OrderNo 字段未被丢弃或改写。
        var query = new OrderListQuery(1, 20)
        {
            OrderNo = "ORD-2026-001"
        };

        OrderListQuery? capturedQuery = null;
        var expectedResult = new PageResult<OrderSummaryDto>(
            new List<OrderSummaryDto>(),
            0,
            1,
            20);

        _accessorMock
            .Setup(a => a.ListAsync(It.IsAny<OrderListQuery>(), It.IsAny<CancellationToken>()))
            .Callback<OrderListQuery, CancellationToken>((q, _) => capturedQuery = q)
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _sut.HandleAsync(query);

        // Assert
        result.Should().NotBeNull();
        capturedQuery.Should().NotBeNull();
        capturedQuery!.OrderNo.Should().Be("ORD-2026-001");
        _accessorMock.Verify(a => a.ListAsync(query, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithOrderNoAndOtherFilters_ShouldPreserveAllFields()
    {
        // Arrange：验证 OrderNo 与现有字段（UserId/SellerId/Status/StartDate/EndDate）同时透传，
        // 防止新增 OrderNo 字段覆盖或重置其它过滤参数。
        var userId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var startDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = new DateTime(2026, 7, 26, 0, 0, 0, DateTimeKind.Utc);

        var query = new OrderListQuery(3, 15)
        {
            UserId = userId,
            SellerId = sellerId,
            Status = "Paid",
            OrderNo = "ORD-2026-ABC",
            StartDate = startDate,
            EndDate = endDate
        };

        OrderListQuery? capturedQuery = null;
        var expectedResult = new PageResult<OrderSummaryDto>(
            new List<OrderSummaryDto>(),
            0,
            3,
            15);

        _accessorMock
            .Setup(a => a.ListAsync(It.IsAny<OrderListQuery>(), It.IsAny<CancellationToken>()))
            .Callback<OrderListQuery, CancellationToken>((q, _) => capturedQuery = q)
            .ReturnsAsync(expectedResult);

        // Act
        await _sut.HandleAsync(query);

        // Assert
        capturedQuery.Should().NotBeNull();
        capturedQuery!.UserId.Should().Be(userId);
        capturedQuery.SellerId.Should().Be(sellerId);
        capturedQuery.Status.Should().Be("Paid");
        capturedQuery.OrderNo.Should().Be("ORD-2026-ABC");
        capturedQuery.StartDate.Should().Be(startDate);
        capturedQuery.EndDate.Should().Be(endDate);
        capturedQuery.Page.Should().Be(3);
        capturedQuery.PageSize.Should().Be(15);
    }
}
