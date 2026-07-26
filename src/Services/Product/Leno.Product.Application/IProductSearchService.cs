using Leno.Product.Application.DTOs;
using Leno.SharedContracts.Responses;

namespace Leno.Product.Application;

/// <summary>
/// 商品搜索服务接口（CQRS 读侧），供买家端全文搜索与多视角查询。
/// 实现基于 Elasticsearch 读模型，与写侧聚合解耦。
/// </summary>
public interface IProductSearchService
{
    /// <summary>
    /// 全文搜索在售商品，支持分类、价格区间过滤与排序分页。
    /// </summary>
    /// <param name="keyword">搜索关键词，可空表示不限。</param>
    /// <param name="categoryId">分类过滤，可空。</param>
    /// <param name="brandId">品牌过滤，可空。</param>
    /// <param name="minPrice">最低价格，可空。</param>
    /// <param name="maxPrice">最高价格，可空。</param>
    /// <param name="sort">排序方式：price_asc / price_desc / hot（综合热度，按 SalesCount 倒序）/ sales（销量倒序）/ default，可空。</param>
    /// <param name="page">页码，从 1 起。</param>
    /// <param name="pageSize">每页条数，最大 100。</param>
    Task<PageResult<ProductSearchResultDto>> SearchAsync(
        string? keyword,
        Guid? categoryId,
        Guid? brandId,
        decimal? minPrice,
        decimal? maxPrice,
        string? sort,
        int page,
        int pageSize,
        CancellationToken ct = default);
}
