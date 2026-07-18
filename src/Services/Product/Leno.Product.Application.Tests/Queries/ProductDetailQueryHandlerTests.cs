using Leno.Product.Application.Queries;
using Moq;

namespace Leno.Product.Application.Tests.Queries;

public class ProductDetailQueryHandlerTests
{
    private readonly Mock<IProductReadModelAccessor> _accessorMock = new();
    private readonly ProductDetailQueryHandler _sut;

    public ProductDetailQueryHandlerTests()
    {
        _sut = new ProductDetailQueryHandler(_accessorMock.Object);
    }

    [Fact]
    public async Task HandleAsync_ShouldDelegateToReadModelAccessorAndReturnResult()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var query = new ProductDetailQuery
        {
            ProductId = productId,
            CurrentUserId = Guid.NewGuid()
        };

        var detailResult = new ProductDetailResult
        {
            ProductId = productId,
            Title = "测试商品",
            Subtitle = "副标题",
            MainImageUrl = "https://img.example.com/1.jpg",
            CategoryId = Guid.NewGuid(),
            BrandId = Guid.NewGuid(),
            ShopId = Guid.NewGuid(),
            Status = "OnSale",
            Specs = new List<string> { "颜色", "尺寸" },
            MinPrice = 99.9m,
            MaxPrice = 4999.9m,
            Currency = "CNY",
            Score = 4.7,
            ReviewCount = 123,
            IndexedAt = new DateTime(2026, 7, 19, 0, 0, 0, DateTimeKind.Utc)
        };

        _accessorMock
            .Setup(a => a.GetByIdAsync(productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(detailResult);

        // Act
        var result = await _sut.HandleAsync(query);

        // Assert
        result.Should().NotBeNull();
        result!.ProductId.Should().Be(productId);
        result.Title.Should().Be("测试商品");
        result.Subtitle.Should().Be("副标题");
        result.MainImageUrl.Should().Be("https://img.example.com/1.jpg");
        result.Status.Should().Be("OnSale");
        result.Specs.Should().BeEquivalentTo(new[] { "颜色", "尺寸" });
        result.MinPrice.Should().Be(99.9m);
        result.MaxPrice.Should().Be(4999.9m);
        result.Currency.Should().Be("CNY");
        result.Score.Should().Be(4.7);
        result.ReviewCount.Should().Be(123);

        _accessorMock.Verify(a => a.GetByIdAsync(productId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenReadModelNotFound_ShouldReturnNull()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var query = new ProductDetailQuery { ProductId = productId };

        _accessorMock
            .Setup(a => a.GetByIdAsync(productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProductDetailResult?)null);

        // Act
        var result = await _sut.HandleAsync(query);

        // Assert
        result.Should().BeNull();
        _accessorMock.Verify(a => a.GetByIdAsync(productId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_NullQuery_ShouldThrowArgumentNullException()
    {
        var act = () => _sut.HandleAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
