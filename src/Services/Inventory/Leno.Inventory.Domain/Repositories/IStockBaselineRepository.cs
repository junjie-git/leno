using Leno.Inventory.Domain.Aggregates;
using Leno.SharedKernel.Abstractions;

namespace Leno.Inventory.Domain.Repositories;

/// <summary>
/// 库存基线仓储接口，封装 <see cref="StockBaseline"/> 聚合的加载与持久化操作。
/// 继承 <see cref="IRepository{T}"/> 复用通用聚合根 CRUD，扩展按 SKU 维度的查询语义。
/// </summary>
public interface IStockBaselineRepository : IRepository<StockBaseline>
{
    /// <summary>
    /// 按 SKU 标识加载库存基线聚合根，不存在返回 null。
    /// </summary>
    /// <param name="skuId">SKU 标识。</param>
    /// <param name="ct">取消令牌。</param>
    Task<StockBaseline?> GetBySkuIdAsync(Guid skuId, CancellationToken ct = default);
}
