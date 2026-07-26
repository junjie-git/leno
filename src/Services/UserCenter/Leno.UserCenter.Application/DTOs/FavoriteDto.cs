namespace Leno.UserCenter.Application.DTOs;

/// <summary>
/// 收藏列表查询参数。
/// 从 UserAuth BC 迁入 UserCenter BC（Task A6）。
/// </summary>
public sealed class FavoriteQueryDto
{
    /// <summary>页码，从 1 起。</summary>
    public int Page { get; init; } = 1;

    /// <summary>每页大小，默认 20，最大 100。</summary>
    public int PageSize { get; init; } = 20;

    /// <summary>
    /// 排序字段：comprehensive（综合，默认）/ price（价格）/ sales（销量）/ created（收藏时间）。
    /// </summary>
    public string Sort { get; init; } = "created";

    /// <summary>排序方向：asc（升序）/ desc（降序，默认）。</summary>
    public string Order { get; init; } = "desc";
}

/// <summary>
/// 收藏商品 DTO。
/// 注意：spuTitle/mainImageUrl/price 等商品快照字段由 BC2 商品域通过视图拼接返回，
/// 本域仅持久化收藏关系（user_id + spu_id + favorited_at）。
/// 列表端点返回的快照字段在 Application 层以可空字段暴露，由前端通过 BFF/API Gateway 组合查询填充。
/// </summary>
public sealed class FavoriteDto
{
    /// <summary>收藏记录标识。</summary>
    public Guid FavoriteId { get; init; }

    /// <summary>商品 SPU 标识。</summary>
    public Guid SpuId { get; init; }

    /// <summary>商品标题（商品域快照，本域不持有）。</summary>
    public string? SpuTitle { get; init; }

    /// <summary>商品主图 URL（商品域快照，本域不持有）。</summary>
    public string? MainImageUrl { get; init; }

    /// <summary>商品价格（商品域快照，本域不持有）。</summary>
    public decimal? Price { get; init; }

    /// <summary>商品原价（商品域快照，本域不持有）。</summary>
    public decimal? OriginalPrice { get; init; }

    /// <summary>店铺标识（商品域快照，本域不持有）。</summary>
    public Guid? ShopId { get; init; }

    /// <summary>店铺名称（商品域快照，本域不持有）。</summary>
    public string? ShopName { get; init; }

    /// <summary>销量（商品域快照，本域不持有）。</summary>
    public long? SalesCount { get; init; }

    /// <summary>库存状态文案（商品域快照，本域不持有）。</summary>
    public string? StockStatus { get; init; }

    /// <summary>收藏时间（UTC）。</summary>
    public DateTime FavoritedAt { get; init; }
}

/// <summary>
/// 新增收藏请求 DTO。
/// </summary>
public sealed class AddFavoriteDto
{
    /// <summary>商品 SPU 标识。</summary>
    public Guid SpuId { get; init; }
}

/// <summary>
/// 批量取消收藏请求 DTO。
/// </summary>
public sealed class BatchDeleteFavoritesDto
{
    /// <summary>待取消收藏的 SPU 标识集合。</summary>
    public IReadOnlyList<Guid> SpuIds { get; init; } = Array.Empty<Guid>();
}

/// <summary>
/// 收藏总数响应 DTO。
/// </summary>
public sealed class FavoriteCountDto
{
    /// <summary>收藏总数。</summary>
    public int Count { get; init; }
}
