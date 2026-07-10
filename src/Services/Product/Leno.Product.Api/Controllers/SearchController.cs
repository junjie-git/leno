using Leno.Infrastructure.Auth;
using Leno.Product.Application;
using Leno.Product.Application.DTOs;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leno.Product.Api.Controllers;

/// <summary>
/// 商品搜索控制器（CQRS 读侧），基于 Elasticsearch 读模型提供买家端全文搜索。
/// 仅返回在售商品（Status = OnSale）。
/// </summary>
[Authorize]
[ApiController]
[Route("api/products/search")]
public sealed class SearchController : ProductControllerBase
{
    private readonly IProductSearchService _searchService;

    public SearchController(ICurrentUserContext currentUser, IProductSearchService searchService)
        : base(currentUser)
    {
        ArgumentNullException.ThrowIfNull(searchService);
        _searchService = searchService;
    }

    /// <summary>
    /// 全文搜索在售商品，支持关键词、分类、品牌、价格区间过滤与排序分页。
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PageResult<ProductSearchResultDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SearchAsync([FromQuery] ProductSearchQueryDto query, CancellationToken ct)
    {
        var result = await _searchService.SearchAsync(
            keyword: query.Keyword,
            categoryId: query.CategoryId,
            brandId: query.BrandId,
            minPrice: query.MinPrice,
            maxPrice: query.MaxPrice,
            sort: query.Sort,
            page: query.Page,
            pageSize: query.PageSize,
            ct);
        return Ok(ApiResponse.Success(result));
    }
}
