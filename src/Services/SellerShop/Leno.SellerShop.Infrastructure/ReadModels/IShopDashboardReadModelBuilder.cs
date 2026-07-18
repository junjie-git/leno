namespace Leno.SellerShop.Infrastructure.ReadModels;

/// <summary>
/// 店铺工作台读模型构建器，从 SellerShop BC 既有聚合（Shop、ShopDashboardData）查询最新状态并构建读模型快照。
/// 由 3 个事件同步消费者（订单创建/订单完成/评价提交）在事件到达时调用，统一重建 <see cref="ShopDashboardReadModel"/>。
/// </summary>
public interface IShopDashboardReadModelBuilder
{
    /// <summary>
    /// 按店铺标识构建工作台读模型。店铺不存在时返回 null，由调用方决定是否跳过索引。
    /// </summary>
    /// <param name="shopId">店铺标识（语义等同订单域 SellerId）。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>读模型快照；店铺不存在时返回 null。</returns>
    Task<ShopDashboardReadModel?> BuildAsync(Guid shopId, CancellationToken ct);
}
