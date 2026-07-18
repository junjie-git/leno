namespace Leno.Product.Application.Queries;

/// <summary>
/// 买家端商品搜索查询参数（CQRS 读侧 Query）。
/// 由 <see cref="ProductSearchQueryHandler"/> 处理，委托给 <c>IProductSearchService</c> 走 ES 读模型。
/// 双发期内与 <c>ProductSearchQueryDto</c> 并存，2 周后 Controller 切换到本 Query。
/// </summary>
public sealed class ProductSearchQuery
{
    /// <summary>搜索关键词，可空表示不限。</summary>
    public string? Keyword { get; init; }

    /// <summary>分类过滤，可空。</summary>
    public Guid? CategoryId { get; init; }

    /// <summary>品牌过滤，可空。</summary>
    public Guid? BrandId { get; init; }

    /// <summary>最低价格，可空。</summary>
    public decimal? MinPrice { get; init; }

    /// <summary>最高价格，可空。</summary>
    public decimal? MaxPrice { get; init; }

    /// <summary>页码，从 0 起（内部转换为 <c>IProductSearchService</c> 所需的从 1 起页码）。</summary>
    public int PageIndex { get; init; }

    /// <summary>每页条数，最大 100。</summary>
    public int PageSize { get; init; } = 20;

    /// <summary>排序方式：price_asc / price_desc / created_desc / default，可空。</summary>
    public string? SortBy { get; init; }
}
