using Leno.Product.Domain.Aggregates;
using Leno.SharedKernel.Abstractions;

namespace Leno.Product.Domain.Repositories;

/// <summary>
/// 库存基线仓储接口，定义在领域层，由基础设施层实现。
/// 按 SKU 标识查询基线，写操作由工作单元统一提交。
/// </summary>
public interface IStockBaselineRepository : IRepository<StockBaseline>
{
    /// <summary>按 SKU 标识查询库存基线，不存在返回 null。</summary>
    Task<StockBaseline?> GetBySkuIdAsync(Guid skuId, CancellationToken ct = default);
}
