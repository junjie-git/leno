using Leno.Inventory.Domain.Aggregates;
using Leno.SharedKernel.Abstractions;

namespace Leno.Inventory.Domain.Repositories;

/// <summary>
/// 库存台账仓储接口 —— 订单 × SKU 维度占用记录（<see cref="StockReservation"/>）的持久化契约。
/// <para>
/// 台账是库存占用的唯一事实来源：命令按订单寻址（不带 SKU 明细），由本仓储按
/// <paramref name="orderId"/> 解析该订单的全部占用条目；唯一约束 (order_id, sku_id)
/// 与单向状态机共同提供命令幂等。
/// </para>
/// </summary>
public interface IStockReservationRepository : IRepository<StockReservation>
{
    /// <summary>
    /// 加载某订单的全部台账条目（任意状态）。
    /// </summary>
    Task<IReadOnlyList<StockReservation>> GetByOrderAsync(Guid orderId, CancellationToken ct = default);

    /// <summary>
    /// 批量写入台账条目（预占时与基线原子 UPDATE 同事务）。
    /// </summary>
    Task AddRangeAsync(IReadOnlyList<StockReservation> entries, CancellationToken ct = default);

    /// <summary>
    /// 该订单是否已存在台账条目（用于预占幂等重放的快速判定）。
    /// </summary>
    Task<bool> ExistsForOrderAsync(Guid orderId, CancellationToken ct = default);
}
