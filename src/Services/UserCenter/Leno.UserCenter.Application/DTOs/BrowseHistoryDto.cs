namespace Leno.UserCenter.Application.DTOs;

/// <summary>
/// 浏览历史列表查询参数。
/// 从 UserAuth BC 迁入 UserCenter BC（Task A6）。
/// </summary>
public sealed class BrowseHistoryQueryDto
{
    /// <summary>页码，从 1 起。</summary>
    public int Page { get; init; } = 1;

    /// <summary>每页大小，默认 20，最大 100。</summary>
    public int PageSize { get; init; } = 20;
}

/// <summary>
/// 浏览历史 DTO。
/// 商品快照字段（spuTitle/mainImageUrl/price/shopId/shopName）由 BC2 商品域通过 BFF/API Gateway 组合填充。
/// 本域仅持久化浏览关系（user_id + spu_id + sku_id + viewed_at）。
/// </summary>
public sealed class BrowseHistoryDto
{
    /// <summary>浏览历史记录标识。</summary>
    public Guid HistoryId { get; init; }

    /// <summary>商品 SPU 标识。</summary>
    public Guid SpuId { get; init; }

    /// <summary>商品 SKU 标识（可空）。</summary>
    public Guid? SkuId { get; init; }

    /// <summary>商品标题（商品域快照，本域不持有）。</summary>
    public string? SpuTitle { get; init; }

    /// <summary>商品主图 URL（商品域快照，本域不持有）。</summary>
    public string? MainImageUrl { get; init; }

    /// <summary>商品价格（商品域快照，本域不持有）。</summary>
    public decimal? Price { get; init; }

    /// <summary>店铺标识（商品域快照，本域不持有）。</summary>
    public Guid? ShopId { get; init; }

    /// <summary>店铺名称（商品域快照，本域不持有）。</summary>
    public string? ShopName { get; init; }

    /// <summary>浏览时间（UTC）。</summary>
    public DateTime ViewedAt { get; init; }
}

/// <summary>
/// 新增浏览历史请求 DTO。
/// </summary>
public sealed class AddBrowseHistoryDto
{
    /// <summary>商品 SPU 标识。</summary>
    public Guid SpuId { get; init; }

    /// <summary>商品 SKU 标识（可空）。</summary>
    public Guid? SkuId { get; init; }
}

/// <summary>
/// 批量删除浏览历史请求 DTO。
/// </summary>
public sealed class BatchDeleteBrowseHistoryDto
{
    /// <summary>待删除的浏览历史记录标识集合。</summary>
    public IReadOnlyList<Guid> Ids { get; init; } = Array.Empty<Guid>();
}
