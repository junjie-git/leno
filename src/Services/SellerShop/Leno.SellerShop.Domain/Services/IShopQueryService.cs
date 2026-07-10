using Leno.SellerShop.Domain.ValueObjects;

namespace Leno.SellerShop.Domain.Services;

/// <summary>
/// 店铺查询防腐层接口，供商品域等下游上下文查询店铺可售状态。
/// 下游上下文不直接引用店铺聚合，仅通过店铺标识查询状态以决定商品可售性。
/// </summary>
public interface IShopQueryService
{
    /// <summary>
    /// 查询店铺当前状态。店铺不存在返回 null，由调用方决定降级行为。
    /// </summary>
    /// <param name="shopId">店铺标识。</param>
    Task<ShopStatus?> GetShopStatusAsync(Guid shopId, CancellationToken ct = default);
}
