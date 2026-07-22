using Leno.Infrastructure.Abstractions.Cqrs;
using Leno.Infrastructure.Auth;
using Leno.Product.Application.DTOs;
using Leno.Product.Application.Queries;
using Leno.Product.Api.Controllers;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace Leno.Product.Api.Tests;

/// <summary>
/// 修复审计 #17：验证 SearchController 经由 CQRS <see cref="IQueryHandler{TQuery, TResult}"/> 读侧入口，
/// 并正确完成 <see cref="ProductSearchQueryDto"/> → <see cref="ProductSearchQuery"/> 的映射
/// （Page 1-based → PageIndex 0-based、Sort → SortBy）。
/// </summary>
public class SearchControllerCqrsTests
{
    private readonly Mock<ICurrentUserContext> _currentUserMock = new();
    private readonly Mock<IQueryHandler<ProductSearchQuery, ProductSearchResult>> _handlerMock = new();

    public SearchControllerCqrsTests()
    {
        _currentUserMock.SetupGet(c => c.IsAuthenticated).Returns(true);
        _currentUserMock.SetupGet(c => c.UserId).Returns(Guid.NewGuid());
    }

    [Fact]
    public async Task SearchAsync_ShouldDelegateToQueryHandler_WithDefaultPaging()
    {
        // 默认 Page=1 → PageIndex=0；PageSize=20
        var dto = new ProductSearchQueryDto();
        var expected = new ProductSearchResult
        {
            Items = Array.Empty<ProductSummaryDto>(),
            TotalCount = 0,
            PageIndex = 0,
            PageSize = 20
        };
        ProductSearchQuery? captured = null;
        _handlerMock
            .Setup(h => h.HandleAsync(It.IsAny<ProductSearchQuery>(), It.IsAny<CancellationToken>()))
            .Callback<ProductSearchQuery, CancellationToken>((q, _) => captured = q)
            .ReturnsAsync(expected);

        var controller = new SearchController(_currentUserMock.Object, _handlerMock.Object);

        var actionResult = await controller.SearchAsync(dto, CancellationToken.None);

        _handlerMock.Verify(h => h.HandleAsync(It.IsAny<ProductSearchQuery>(), It.IsAny<CancellationToken>()), Times.Once);
        captured.Should().NotBeNull();
        captured!.PageIndex.Should().Be(0);
        captured.PageSize.Should().Be(20);
        captured.SortBy.Should().BeNull();
        captured.Keyword.Should().BeNull();

        actionResult.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeOfType<ApiResponse<ProductSearchResult>>()
            .Which.Data.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task SearchAsync_ShouldConvertOneBasedPageToZeroBasedPageIndex()
    {
        var dto = new ProductSearchQueryDto { Page = 3, PageSize = 15 };
        _handlerMock
            .Setup(h => h.HandleAsync(It.IsAny<ProductSearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProductSearchResult
            {
                Items = Array.Empty<ProductSummaryDto>(),
                TotalCount = 0,
                PageIndex = 2,
                PageSize = 15
            });

        var controller = new SearchController(_currentUserMock.Object, _handlerMock.Object);

        await controller.SearchAsync(dto, CancellationToken.None);

        _handlerMock.Verify(
            h => h.HandleAsync(
                It.Is<ProductSearchQuery>(q => q.PageIndex == 2 && q.PageSize == 15),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SearchAsync_ShouldClampNonPositivePageToFirstPage()
    {
        var dto = new ProductSearchQueryDto { Page = 0, PageSize = 10 };
        _handlerMock
            .Setup(h => h.HandleAsync(It.IsAny<ProductSearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProductSearchResult
            {
                Items = Array.Empty<ProductSummaryDto>(),
                TotalCount = 0,
                PageIndex = 0,
                PageSize = 10
            });

        var controller = new SearchController(_currentUserMock.Object, _handlerMock.Object);

        await controller.SearchAsync(dto, CancellationToken.None);

        _handlerMock.Verify(
            h => h.HandleAsync(
                It.Is<ProductSearchQuery>(q => q.PageIndex == 0),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SearchAsync_ShouldMapAllFiltersAndSort()
    {
        var categoryId = Guid.NewGuid();
        var brandId = Guid.NewGuid();
        var dto = new ProductSearchQueryDto
        {
            Keyword = "phone",
            CategoryId = categoryId,
            BrandId = brandId,
            MinPrice = 100m,
            MaxPrice = 500m,
            Sort = "price_asc",
            Page = 2,
            PageSize = 25
        };
        _handlerMock
            .Setup(h => h.HandleAsync(It.IsAny<ProductSearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProductSearchResult
            {
                Items = Array.Empty<ProductSummaryDto>(),
                TotalCount = 0,
                PageIndex = 1,
                PageSize = 25
            });

        var controller = new SearchController(_currentUserMock.Object, _handlerMock.Object);

        await controller.SearchAsync(dto, CancellationToken.None);

        _handlerMock.Verify(
            h => h.HandleAsync(
                It.Is<ProductSearchQuery>(q =>
                    q.Keyword == "phone"
                    && q.CategoryId == categoryId
                    && q.BrandId == brandId
                    && q.MinPrice == 100m
                    && q.MaxPrice == 500m
                    && q.SortBy == "price_asc"
                    && q.PageIndex == 1
                    && q.PageSize == 25),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
