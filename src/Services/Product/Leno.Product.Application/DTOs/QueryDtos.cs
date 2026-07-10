namespace Leno.Product.Application.DTOs;

/// <summary>
/// 带原因的操作 DTO，用于商品驳回、下架等操作。
/// </summary>
public sealed class ActionReasonDto
{
    /// <summary>操作原因，不可为空，≤200 字符。</summary>
    public string Reason { get; init; } = string.Empty;
}

/// <summary>
/// 商品分页查询 DTO（运营/卖家端）。
/// Status 为商品状态名称（不区分大小写），可空表示不限。
/// </summary>
public sealed class ProductQueryDto
{
    public Guid? ShopId { get; init; }

    public string? Status { get; init; }

    public Guid? CategoryId { get; init; }

    public string? Keyword { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 20;
}

/// <summary>
/// 品牌分页查询 DTO。
/// </summary>
public sealed class BrandQueryDto
{
    public string? Status { get; init; }

    public string? Keyword { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 20;
}

/// <summary>
/// 买家端商品搜索结果 DTO，由 ES 读模型投影。
/// </summary>
public sealed class ProductSearchResultDto
{
    public Guid Id { get; init; }

    public string Title { get; init; } = string.Empty;

    public string? Subtitle { get; init; }

    public string MainImageUrl { get; init; } = string.Empty;

    public Guid CategoryId { get; init; }

    public Guid? BrandId { get; init; }

    public Guid ShopId { get; init; }

    public decimal MinPrice { get; init; }

    public decimal MaxPrice { get; init; }

    public string Currency { get; init; } = "CNY";
}

/// <summary>
/// 买家端商品搜索查询 DTO（GET 查询参数绑定）。
/// </summary>
public sealed class ProductSearchQueryDto
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

    /// <summary>排序方式：price_asc / price_desc / default，可空。</summary>
    public string? Sort { get; init; }

    /// <summary>页码，从 1 起。</summary>
    public int Page { get; init; } = 1;

    /// <summary>每页条数，最大 100。</summary>
    public int PageSize { get; init; } = 20;
}
