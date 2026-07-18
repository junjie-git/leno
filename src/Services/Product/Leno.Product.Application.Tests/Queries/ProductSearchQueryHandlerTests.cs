using Leno.Product.Application.DTOs;
using Leno.Product.Application.Queries;
using Leno.SharedContracts.Responses;
using Moq;

namespace Leno.Product.Application.Tests.Queries;

public class ProductSearchQueryHandlerTests
{
    private readonly Mock<IProductSearchService> _searchServiceMock = new();
    private readonly ProductSearchQueryHandler _sut;

    public ProductSearchQueryHandlerTests()
    {
        _sut = new ProductSearchQueryHandler(_searchServiceMock.Object);
    }

    [Fact]
    public async Task HandleAsync_ShouldDelegateToSearchServiceAndMapResult()
    {
        // Arrange
        var query = new ProductSearchQuery
        {
            Keyword = "手机",
            CategoryId = Guid.NewGuid(),
            BrandId = Guid.NewGuid(),
            MinPrice = 100m,
            MaxPrice = 5000m,
            SortBy = "price_asc",
            PageIndex = 0,
            PageSize = 20
        };

        var searchResultDto = new ProductSearchResultDto
        {
            Id = Guid.NewGuid(),
            Title = "测试手机",
            Subtitle = "副标题",
            MainImageUrl = "https://img.example.com/1.jpg",
            CategoryId = query.CategoryId.Value,
            BrandId = query.BrandId,
            ShopId = Guid.NewGuid(),
            MinPrice = 99.9m,
            MaxPrice = 4999.9m,
            Currency = "CNY"
        };

        var pageResult = new PageResult<ProductSearchResultDto>(
            new List<ProductSearchResultDto> { searchResultDto },
            total: 1,
            page: 1,
            pageSize: 20);

        _searchServiceMock
            .Setup(s => s.SearchAsync(
                query.Keyword,
                query.CategoryId,
                query.BrandId,
                query.MinPrice,
                query.MaxPrice,
                query.SortBy,
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(pageResult);

        // Act
        var result = await _sut.HandleAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(1);
        result.TotalCount.Should().Be(1);
        result.PageIndex.Should().Be(0);
        result.PageSize.Should().Be(20);

        var summary = result.Items[0];
        summary.ProductId.Should().Be(searchResultDto.Id);
        summary.Title.Should().Be(searchResultDto.Title);
        summary.Subtitle.Should().Be(searchResultDto.Subtitle);
        summary.MainImageUrl.Should().Be(searchResultDto.MainImageUrl);
        summary.CategoryId.Should().Be(searchResultDto.CategoryId);
        summary.BrandId.Should().Be(searchResultDto.BrandId);
        summary.ShopId.Should().Be(searchResultDto.ShopId);
        summary.MinPrice.Should().Be(searchResultDto.MinPrice);
        summary.MaxPrice.Should().Be(searchResultDto.MaxPrice);
        summary.Currency.Should().Be(searchResultDto.Currency);

        // 验证委托时 PageIndex+1 转换为 page=1
        _searchServiceMock.Verify(s => s.SearchAsync(
            query.Keyword,
            query.CategoryId,
            query.BrandId,
            query.MinPrice,
            query.MaxPrice,
            query.SortBy,
            1,
            query.PageSize,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ShouldConvertPageIndexFromZeroBasedToOneBased()
    {
        // Arrange: PageIndex=2（第 3 页）应转换为 page=3
        var query = new ProductSearchQuery { PageIndex = 2, PageSize = 10 };

        _searchServiceMock
            .Setup(s => s.SearchAsync(
                It.IsAny<string?>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>(),
                It.IsAny<decimal?>(),
                It.IsAny<decimal?>(),
                It.IsAny<string?>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PageResult<ProductSearchResultDto>(
                new List<ProductSearchResultDto>(), 0, 3, 10));

        // Act
        var result = await _sut.HandleAsync(query);

        // Assert: 对外保持从 0 起（PageIndex=2），内部调用从 1 起（page=3）
        result.PageIndex.Should().Be(2);
        _searchServiceMock.Verify(s => s.SearchAsync(
            It.IsAny<string?>(),
            It.IsAny<Guid?>(),
            It.IsAny<Guid?>(),
            It.IsAny<decimal?>(),
            It.IsAny<decimal?>(),
            It.IsAny<string?>(),
            3,
            10,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_NullQuery_ShouldThrowArgumentNullException()
    {
        var act = () => _sut.HandleAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
