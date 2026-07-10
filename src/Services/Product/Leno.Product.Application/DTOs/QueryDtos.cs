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
