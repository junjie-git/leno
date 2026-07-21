using Leno.Order.Domain.Aggregates;
using Leno.SharedKernel.Abstractions;

namespace Leno.Order.Domain.Repositories;

/// <summary>
/// 库存预占聚合仓储接口，提供按 SKU 维度的聚合加载与持久化。
/// 继承 <see cref="IRepository{T}"/> 复用通用聚合根 CRUD，扩展按 SKU 维度的查询与 GetOrCreate 语义。
/// 用于 Redis 原子层 + DB 聚合审计源的双写策略。
/// </summary>
public interface IStockReservationRepository : IRepository<StockReservation>
{
    /// <summary>
    /// 按 SKU 标识加载库存预占聚合根，不存在返回 null。
    /// </summary>
    /// <param name="skuId">SKU 标识。</param>
    /// <param name="ct">取消令牌。</param>
    Task<StockReservation?> GetBySkuIdAsync(Guid skuId, CancellationToken ct = default);

    /// <summary>
    /// 按 SKU 标识加载库存预占聚合根，不存在则创建基线为 0 的新聚合并返回。
    /// 用于 Redis 与 DB 双写场景下保证聚合始终存在，待后续 <c>SetBaseLineAsync</c> 同步基线。
    /// </summary>
    /// <param name="skuId">SKU 标识。</param>
    /// <param name="ct">取消令牌。</param>
    Task<StockReservation> GetOrCreateAsync(Guid skuId, CancellationToken ct = default);
}
