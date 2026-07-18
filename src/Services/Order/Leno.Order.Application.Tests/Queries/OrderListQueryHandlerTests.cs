using Leno.Order.Application.Queries;
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
        var query = new OrderListQuery
        {
            UserId = Guid.NewGuid(),
            SellerId = Guid.NewGuid(),
            Status = "Paid",
            StartDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 7, 19, 0, 0, 0, DateTimeKind.Utc),
            PageIndex = 0,
            PageSize = 20
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

        var expectedResult = new OrderListResult
        {
            Items = new List<OrderSummaryDto> { summary },
            TotalCount = 1,
            PageIndex = 0,
            PageSize = 20
        };

        _accessorMock
            .Setup(a => a.ListAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _sut.HandleAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(1);
        result.TotalCount.Should().Be(1);
        result.PageIndex.Should().Be(0);
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
        var query = new OrderListQuery { PageIndex = 5, PageSize = 10 };

        var expectedResult = new OrderListResult
        {
            Items = new List<OrderSummaryDto>(),
            TotalCount = 0,
            PageIndex = 5,
            PageSize = 10
        };

        _accessorMock
            .Setup(a => a.ListAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _sut.HandleAsync(query);

        // Assert
        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
        result.PageIndex.Should().Be(5);
        result.PageSize.Should().Be(10);
    }

    [Fact]
    public async Task HandleAsync_NullQuery_ShouldThrowArgumentNullException()
    {
        var act = () => _sut.HandleAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
