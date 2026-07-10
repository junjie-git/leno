namespace Leno.SellerShop.Application.DTOs;

/// <summary>
/// 运营端店铺分页查询 DTO。
/// Status 为店铺状态名称（不区分大小写），可空表示不限。
/// </summary>
public sealed class AdminShopQueryDto
{
    /// <summary>店铺状态过滤，可空。</summary>
    public string? Status { get; init; }

    /// <summary>店铺名称关键词，可空。</summary>
    public string? Keyword { get; init; }

    /// <summary>页码，从 1 起。</summary>
    public int Page { get; init; } = 1;

    /// <summary>每页条数，最大 100。</summary>
    public int PageSize { get; init; } = 20;
}
