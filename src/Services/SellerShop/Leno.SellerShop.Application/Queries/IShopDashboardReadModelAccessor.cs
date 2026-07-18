namespace Leno.SellerShop.Application.Queries;

/// <summary>
/// 店铺工作台读模型访问器抽象（CQRS 读侧端口）。
/// 定义在 Application 层以保持分层洁癖：Application 不直接引用 Infrastructure 层的
/// <c>IEsReadModelRepository&lt;ShopDashboardReadModel&gt;</c>，由 Infrastructure 层实现。
/// 与 <c>IShopDashboardReadModelBuilder</c> 区别：
/// <list type="bullet">
/// <item><c>IShopDashboardReadModelBuilder</c> 从领域仓储重建读模型（写侧事件触发）。</item>
/// <item><c>IShopDashboardReadModelAccessor</c> 直接从 ES 查询已索引的读模型（读侧查询）。</item>
/// </list>
/// </summary>
public interface IShopDashboardReadModelAccessor
{
    /// <summary>
    /// 按店铺标识查询 ES 读模型并映射为 <see cref="ShopDashboardResult"/>。
    /// </summary>
    /// <param name="shopId">店铺标识（语义等同订单域 SellerId）。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>读模型存在则返回 <see cref="ShopDashboardResult"/>，否则返回 null。</returns>
    Task<ShopDashboardResult?> GetByShopIdAsync(Guid shopId, CancellationToken ct = default);
}
