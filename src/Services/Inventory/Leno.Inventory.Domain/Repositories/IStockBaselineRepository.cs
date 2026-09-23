using Leno.Inventory.Domain.Aggregates;
using Leno.SharedKernel.Abstractions;

namespace Leno.Inventory.Domain.Repositories;

/// <summary>
/// 库存基线仓储接口 —— SKU 维度库存计数器（可用 / 预占 / 已扣减）的持久化契约。
/// <para>
/// 除通用聚合 CRUD 外，暴露四个**原子条件 UPDATE** 操作（预占/确认/释放/归还）。
/// 计数器语义下使用"加载-修改-保存"会在并发预占时产生丢失更新，
/// 故高频路径以受守卫条件保护的单语句 UPDATE 落地（见实现），受影响行数为 0 即守卫不满足。
/// </para>
/// </summary>
public interface IStockBaselineRepository : IRepository<StockBaseline>
{
    /// <summary>
    /// 按 SKU 标识加载库存基线聚合根，不存在返回 null。
    /// </summary>
    Task<StockBaseline?> GetBySkuIdAsync(Guid skuId, CancellationToken ct = default);

    /// <summary>
    /// 预占：可卖量（available - reserved）充足时 reserved += quantity。
    /// </summary>
    /// <returns>受影响行数：1 = 成功；0 = 库存不足或基线不存在。</returns>
    Task<int> TryReserveAsync(Guid skuId, int quantity, CancellationToken ct = default);

    /// <summary>
    /// 确认扣减：预占充足时 available -= quantity、reserved -= quantity、deducted += quantity。
    /// </summary>
    /// <returns>受影响行数：1 = 成功；0 = 预占不足或基线不存在。</returns>
    Task<int> TryConfirmAsync(Guid skuId, int quantity, CancellationToken ct = default);

    /// <summary>
    /// 释放预占：预占充足时 reserved -= quantity（可用量不变 —— 预占本就占用自可用池）。
    /// </summary>
    /// <returns>受影响行数：1 = 成功；0 = 预占不足或基线不存在。</returns>
    Task<int> TryReleaseAsync(Guid skuId, int quantity, CancellationToken ct = default);

    /// <summary>
    /// 归还已扣减：available += quantity、deducted -= quantity。
    /// </summary>
    /// <returns>受影响行数：1 = 成功；0 = 基线不存在或扣减量不足。</returns>
    Task<int> TryReturnAsync(Guid skuId, int quantity, CancellationToken ct = default);

    /// <summary>
    /// 当前可卖量（available - reserved）。基线不存在视为 0。
    /// </summary>
    Task<int> GetSellableAsync(Guid skuId, CancellationToken ct = default);

    /// <summary>
    /// 基线同步（消费 <c>StockAdjustedEvent</c> 的唯一写入方）：
    /// 不存在则创建（预占/扣减为 0），存在则将可用量覆盖为商品域权威值。
    /// </summary>
    Task SetAvailableAsync(Guid skuId, Guid productId, int availableQty, CancellationToken ct = default);
}
