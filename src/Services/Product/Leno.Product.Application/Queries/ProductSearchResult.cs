namespace Leno.Product.Application.Queries;

/// <summary>
/// 买家端商品搜索结果（CQRS 读侧 Query Result）。
/// 字段命名遵循 <c>ProductReadModel</c>：价格区间由 SKU 集合预聚合为 MinPrice/MaxPrice。
/// </summary>
public sealed class ProductSearchResult
{
    /// <summary>当前页商品摘要列表。</summary>
    public required IReadOnlyList<ProductSummaryDto> Items { get; init; }

    /// <summary>命中总数。</summary>
    public int TotalCount { get; init; }

    /// <summary>页码，从 0 起（与 <see cref="ProductSearchQuery.PageIndex"/> 一致）。</summary>
    public int PageIndex { get; init; }

    /// <summary>每页条数。</summary>
    public int PageSize { get; init; }
}

/// <summary>
/// 买家端商品摘要 DTO（基于 ES 读模型字段）。
/// </summary>
public sealed class ProductSummaryDto
{
    public Guid ProductId { get; init; }

    public string Title { get; init; } = string.Empty;

    public string? Subtitle { get; init; }

    public string MainImageUrl { get; init; } = string.Empty;

    public Guid CategoryId { get; init; }

    public Guid? BrandId { get; init; }

    public Guid ShopId { get; init; }

    public decimal MinPrice { get; init; }

    public decimal MaxPrice { get; init; }

    public string Currency { get; init; } = "CNY";

    /// <summary>加权平均评分，由评价评分消费者增量维护。</summary>
    public double Score { get; init; }

    /// <summary>可见评价总数。</summary>
    public int ReviewCount { get; init; }
}
