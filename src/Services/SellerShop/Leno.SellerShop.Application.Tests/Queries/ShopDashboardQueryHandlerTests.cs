using Leno.SellerShop.Application.Queries;
using Moq;

namespace Leno.SellerShop.Application.Tests.Queries;

/// <summary>
/// 卖家工作台 QueryHandler 单元测试。
/// 通过 mock <see cref="IShopDashboardReadModelAccessor"/> 验证 QueryHandler 委托与结果映射行为。
/// </summary>
public class ShopDashboardQueryHandlerTests
{
    private readonly Mock<IShopDashboardReadModelAccessor> _accessorMock = new();
    private readonly ShopDashboardQueryHandler _sut;

    public ShopDashboardQueryHandlerTests()
    {
        _sut = new ShopDashboardQueryHandler(_accessorMock.Object);
    }

    [Fact]
    public async Task HandleAsync_ShouldDelegateToReadModelAccessorAndReturnResult()
    {
        // Arrange
        var shopId = Guid.NewGuid();
        var query = new ShopDashboardQuery
        {
            ShopId = shopId,
            StartDate = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 7, 31, 0, 0, 0, DateTimeKind.Utc)
        };

        var lastUpdatedAt = new DateTime(2026, 7, 18, 12, 0, 0, DateTimeKind.Utc);
        var indexedAt = new DateTime(2026, 7, 19, 0, 0, 0, DateTimeKind.Utc);

        var result = new ShopDashboardResult
        {
            ShopId = shopId,
            ShopName = "Leno 旗舰店",
            TotalOrders = 120,
            PendingOrders = 8,
            ConfirmedOrders = 12,
            CompletedOrders = 95,
            CancelledOrders = 5,
            TotalReviews = 88,
            AverageRating = 4.65m,
            FiveStarReviews = 60,
            OneStarReviews = 2,
            TotalSales = 128888.88m,
            Currency = "CNY",
            LastUpdatedAt = lastUpdatedAt,
            IndexedAt = indexedAt
        };

        _accessorMock
            .Setup(a => a.GetByShopIdAsync(shopId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        // Act
        var actual = await _sut.HandleAsync(query);

        // Assert
        actual.Should().NotBeNull();
        actual!.ShopId.Should().Be(shopId);
        actual.ShopName.Should().Be("Leno 旗舰店");
        actual.TotalOrders.Should().Be(120);
        actual.PendingOrders.Should().Be(8);
        actual.ConfirmedOrders.Should().Be(12);
        actual.CompletedOrders.Should().Be(95);
        actual.CancelledOrders.Should().Be(5);
        actual.TotalReviews.Should().Be(88);
        actual.AverageRating.Should().Be(4.65m);
        actual.FiveStarReviews.Should().Be(60);
        actual.OneStarReviews.Should().Be(2);
        actual.TotalSales.Should().Be(128888.88m);
        actual.Currency.Should().Be("CNY");
        actual.LastUpdatedAt.Should().Be(lastUpdatedAt);
        actual.IndexedAt.Should().Be(indexedAt);

        _accessorMock.Verify(a => a.GetByShopIdAsync(shopId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenShopNotFound_ShouldReturnNull()
    {
        // Arrange
        var shopId = Guid.NewGuid();
        var query = new ShopDashboardQuery { ShopId = shopId };

        _accessorMock
            .Setup(a => a.GetByShopIdAsync(shopId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShopDashboardResult?)null);

        // Act
        var actual = await _sut.HandleAsync(query);

        // Assert
        actual.Should().BeNull();
        _accessorMock.Verify(a => a.GetByShopIdAsync(shopId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_NullQuery_ShouldThrowArgumentNullException()
    {
        var act = () => _sut.HandleAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
