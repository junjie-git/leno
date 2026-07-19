namespace Leno.SellerShop.Application;

/// <summary>
/// 卖家店铺域跨 BC 内部查询服务（M4 双轨方案）。
/// 仅暴露跨 BC 查询所需的方法子集（只读），供 SellerGrpcService 复用。
/// </summary>
public interface ISellerInternalQueryService
{
    /// <summary>
    /// 查询卖家信息（seller_id = 用户域 UserId）。
    /// </summary>
    /// <param name="sellerId">卖家账号标识（用户域 UserId）。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>卖家信息；卖家档案不存在返回 null。</returns>
    Task<SellerInfoDto?> GetSellerInfoAsync(Guid sellerId, CancellationToken ct = default);

    /// <summary>
    /// 查询店铺信息。
    /// </summary>
    /// <param name="shopId">店铺标识。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>店铺信息；店铺不存在返回 null。</returns>
    Task<ShopInfoDto?> GetShopInfoAsync(Guid shopId, CancellationToken ct = default);
}

/// <summary>卖家信息 DTO（跨 BC 查询用）。</summary>
public sealed class SellerInfoDto
{
    public Guid SellerId { get; init; }

    /// <summary>卖家名称（取自卖家档案 RealName）。</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>卖家档案状态（枚举名转为字符串，如 Approved/PendingReview）。</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>所属店铺标识；卖家档案未关联店铺时为 Guid.Empty。</summary>
    public Guid ShopId { get; init; }
}

/// <summary>店铺信息 DTO（跨 BC 查询用）。</summary>
public sealed class ShopInfoDto
{
    public Guid ShopId { get; init; }

    /// <summary>店铺名称。</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>店铺状态（枚举名转为字符串，如 Active/Suspended）。</summary>
    public string Status { get; init; } = string.Empty;

    public Guid SellerId { get; init; }
}
