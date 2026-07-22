using Leno.Infrastructure.Abstractions.Cqrs;
using Leno.Infrastructure.Auth;
using Leno.Product.Application.DTOs;
using Leno.Product.Application.Queries;
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
    private readonly IQueryHandler<ProductSearchQuery, ProductSearchResult> _queryHandler;

    public SearchController(
        ICurrentUserContext currentUser,
        IQueryHandler<ProductSearchQuery, ProductSearchResult> queryHandler)
        : base(currentUser)
    {
        ArgumentNullException.ThrowIfNull(queryHandler);
        _queryHandler = queryHandler;
    }

    /// <summary>
    /// 全文搜索在售商品，支持关键词、分类、品牌、价格区间过滤与排序分页。
    /// 修复审计 #17：原实现绕过 CQRS QueryHandler 直接调用 IProductSearchService，
    /// 现统一经由 <see cref="IQueryHandler{TQuery, TResult}"/> 读侧入口，保持 CQRS 职责分层一致。
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<ProductSearchResult>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SearchAsync([FromQuery] ProductSearchQueryDto query, CancellationToken ct)
    {
        // ProductSearchQueryDto.Page 从 1 起；ProductSearchQuery.PageIndex 从 0 起，需做转换。
        var pageIndex = query.Page < 1 ? 0 : query.Page - 1;
        var cqrsQuery = new ProductSearchQuery
        {
            Keyword = query.Keyword,
            CategoryId = query.CategoryId,
            BrandId = query.BrandId,
            MinPrice = query.MinPrice,
            MaxPrice = query.MaxPrice,
            SortBy = query.Sort,
            PageIndex = pageIndex,
            PageSize = query.PageSize
        };

        var result = await _queryHandler.HandleAsync(cqrsQuery, ct);
        return Ok(ApiResponse.Success(result));
    }
}
