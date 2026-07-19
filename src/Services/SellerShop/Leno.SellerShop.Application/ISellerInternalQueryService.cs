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

    /// <summary>
    /// 校验卖家对指定资源的归属关系（用于越权防护）。
    /// 按 <paramref name="resourceType"/> 分支：
    /// <list type="bullet">
    /// <item><c>shop</c>：调 <c>IShopAppService.GetMyShopAsync</c> 比对店铺归属。</item>
    /// <item><c>spu</c>：调 <c>IProductAntiCorruptionService.GetSpuSellerIdAsync</c> 反查 SPU 归属卖家。</item>
    /// <item><c>order</c>：调 <c>IOrderAntiCorruptionService.GetOrderSellerIdAsync</c> 反查订单归属卖家。</item>
    /// <item>其他：返回 false 并记 warning（fail-closed，安全优先）。</item>
    /// </list>
    /// 跨域防腐层失败时返回 null（不抛异常），由本方法判 false，避免跨域故障阻断卖家操作。
    /// </summary>
    /// <param name="sellerId">卖家账号标识（用户域 UserId）。</param>
    /// <param name="resourceType">资源类型：shop / spu / order。</param>
    /// <param name="resourceId">资源标识。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>归属校验是否通过；资源不存在、归属不符或资源类型未知时返回 false。</returns>
    Task<bool> ValidateOwnershipAsync(Guid sellerId, string resourceType, Guid resourceId, CancellationToken ct = default);
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
