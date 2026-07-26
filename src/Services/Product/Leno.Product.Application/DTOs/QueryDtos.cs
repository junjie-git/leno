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

    public Guid? SellerId { get; init; }

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

    /// <summary>
    /// 排序方式，可空。取值（大小写不敏感）：
    /// <list type="bullet">
    /// <item><c>price_asc</c>：按最低 SKU 价格升序。</item>
    /// <item><c>price_desc</c>：按最低 SKU 价格降序。</item>
    /// <item><c>hot</c>：综合热度，按 SalesCount 倒序（综合热度近似为销量倒序）。</item>
    /// <item><c>sales</c>：销量倒序（按 SalesCount 倒序）。</item>
    /// <item><c>default</c> / <c>relevance</c> / 空：默认相关性得分排序。</item>
    /// </list>
    /// </summary>
    public string? Sort { get; init; }

    /// <summary>页码，从 1 起。</summary>
    public int Page { get; init; } = 1;

    /// <summary>每页条数，最大 100。</summary>
    public int PageSize { get; init; } = 20;
}

/// <summary>
/// 批量审核请求 DTO，用于批量审核通过/驳回。
/// </summary>
public sealed class BatchReviewRequestDto
{
    /// <summary>商品标识列表，不可为空。</summary>
    public List<Guid> Ids { get; init; } = new();

    /// <summary>审核原因（驳回时必填，通过时可选）。</summary>
    public string? Reason { get; init; }
}

/// <summary>
/// 批量操作结果 DTO，承载成功与失败明细，保证单个失败不阻塞整批。
/// </summary>
public sealed class BatchOperationResultDto
{
    /// <summary>成功处理的标识列表。</summary>
    public List<Guid> SucceededIds { get; init; } = new();

    /// <summary>失败处理的标识与原因映射。</summary>
    public List<BatchFailureItem> Failures { get; init; } = new();
}

/// <summary>
/// 批量操作失败项，记录失败标识与失败原因。
/// </summary>
public sealed class BatchFailureItem
{
    /// <summary>失败的商品标识。</summary>
    public Guid Id { get; init; }

    /// <summary>失败原因。</summary>
    public string Reason { get; init; } = string.Empty;
}
