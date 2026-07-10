using Leno.SharedKernel.Abstractions;
using Leno.SellerShop.Domain.Aggregates;

namespace Leno.SellerShop.Domain.Repositories;

/// <summary>
/// 店铺运营指标仓储接口，定义在领域层，由基础设施层实现。
/// 指标按 (ShopId, Date) 维度唯一，由事件驱动 upsert。
/// </summary>
public interface IShopMetricsRepository : IRepository<ShopMetrics>
{
    /// <summary>按店铺与日期查询当日指标，返回带跟踪的聚合（用于写场景增量更新）。</summary>
    Task<ShopMetrics?> GetByShopIdAsync(Guid shopId, DateOnly metricsDate, CancellationToken ct = default);

    /// <summary>按店铺与日期范围查询指标序列（只读，用于趋势图表）。</summary>
    Task<IReadOnlyList<ShopMetrics>> GetByDateRangeAsync(
        Guid shopId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken ct = default);

    /// <summary>
    /// 新增或更新指标。已跟踪的聚合（经 GetByShopIdAsync 加载并修改）无需调用；
    /// 新建的聚合经此方法写入。同一 (ShopId, Date) 已存在时按更新处理。
    /// </summary>
    Task UpsertAsync(ShopMetrics metrics, CancellationToken ct = default);
}
