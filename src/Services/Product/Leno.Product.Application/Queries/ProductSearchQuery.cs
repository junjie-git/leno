namespace Leno.Product.Application.Queries;

/// <summary>
/// 买家端商品搜索查询参数（CQRS 读侧 Query）。
/// 由 <see cref="ProductSearchQueryHandler"/> 处理，委托给 <c>IProductSearchService</c> 走 ES 读模型。
/// <para>
/// 定位（双轨下线 DEC-9 改判，2026-09-21）：本查询是<b>买家端 ES 搜索路径</b>，
/// 不含 <c>ShopId</c>/<c>Status</c> 过滤；卖家/运营管理端列表走
/// <c>SPUAppService.QueryProductsAsync</c>（SQL）。二者是用途不同的并行读路径，
/// 而非新旧实现 —— ES/SQL 终局归属由 E3 单独决策。
/// </para>
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

    /// <summary>
    /// 排序方式，可空。取值（大小写不敏感）：
    /// <c>price_asc</c> / <c>price_desc</c> / <c>hot</c>（综合热度，按 SalesCount 倒序）
    /// / <c>sales</c>（销量倒序）/ <c>created_desc</c> / <c>default</c>。
    /// </summary>
    public string? SortBy { get; init; }
}
