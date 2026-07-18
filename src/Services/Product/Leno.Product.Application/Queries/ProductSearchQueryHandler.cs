using Leno.Infrastructure.Abstractions.Cqrs;
using Leno.Product.Application.DTOs;
using Leno.SharedContracts.Responses;

namespace Leno.Product.Application.Queries;

/// <summary>
/// 买家端商品搜索查询处理器。
/// 委托给既有 <see cref="IProductSearchService"/>（位于 Infrastructure 层，走 ES 读模型），
/// 将其返回的 <see cref="ProductSearchResultDto"/> 适配为 <see cref="ProductSearchResult"/>。
/// 双发期 2 周内与 <c>SPUAppService.QueryProductsAsync</c> 并存，2 周后 Controller 切换到本 QueryHandler。
/// </summary>
public sealed class ProductSearchQueryHandler : IQueryHandler<ProductSearchQuery, ProductSearchResult>
{
    private readonly IProductSearchService _searchService;

    public ProductSearchQueryHandler(IProductSearchService searchService)
    {
        ArgumentNullException.ThrowIfNull(searchService);
        _searchService = searchService;
    }

    /// <inheritdoc />
    public async Task<ProductSearchResult> HandleAsync(ProductSearchQuery query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        // PageIndex 从 0 起，IProductSearchService.SearchAsync 期望 Page 从 1 起
        var page = query.PageIndex < 0 ? 1 : query.PageIndex + 1;

        PageResult<ProductSearchResultDto> pageResult = await _searchService.SearchAsync(
            keyword: query.Keyword,
            categoryId: query.CategoryId,
            brandId: query.BrandId,
            minPrice: query.MinPrice,
            maxPrice: query.MaxPrice,
            sort: query.SortBy,
            page: page,
            pageSize: query.PageSize,
            ct: ct);

        var items = pageResult.Items.Select(ToSummaryDto).ToList();

        return new ProductSearchResult
        {
            Items = items,
            TotalCount = pageResult.Total,
            PageIndex = pageResult.Page - 1, // 内部对外保持从 0 起
            PageSize = pageResult.PageSize
        };
    }

    private static ProductSummaryDto ToSummaryDto(ProductSearchResultDto dto)
        => new()
        {
            ProductId = dto.Id,
            Title = dto.Title,
            Subtitle = dto.Subtitle,
            MainImageUrl = dto.MainImageUrl,
            CategoryId = dto.CategoryId,
            BrandId = dto.BrandId,
            ShopId = dto.ShopId,
            MinPrice = dto.MinPrice,
            MaxPrice = dto.MaxPrice,
            Currency = dto.Currency
        };
}
