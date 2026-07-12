using Leno.SharedKernel.Abstractions;
using Leno.SellerShop.Domain.Aggregates;

namespace Leno.SellerShop.Domain.Repositories;

/// <summary>
/// 店铺经营数据仓储接口，定义在领域层，由基础设施层实现。
/// 每店铺仅一条记录，按 ShopId 唯一查询。
/// </summary>
public interface IShopDashboardRepository : IRepository<ShopDashboardData>
{
    /// <summary>按店铺标识查询经营数据，不存在时返回 null。</summary>
    Task<ShopDashboardData?> GetByShopIdAsync(Guid shopId, CancellationToken ct = default);
}