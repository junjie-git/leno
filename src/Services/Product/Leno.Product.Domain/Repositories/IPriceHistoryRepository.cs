using Leno.Product.Domain.Aggregates;
using Leno.SharedKernel.Abstractions;

namespace Leno.Product.Domain.Repositories;

/// <summary>
/// 价格历史仓储接口，定义在领域层，由基础设施层实现。
/// 从 SPU 聚合拆分而来，支持按 SPU/SKU 维度查询价格变更轨迹。
/// 写操作由工作单元统一提交。
/// </summary>
public interface IPriceHistoryRepository : IRepository<PriceHistory>
{
    /// <summary>按 SPU 标识查询其下所有 SKU 的价格变更历史，按变更时间倒序。</summary>
    Task<IReadOnlyList<PriceHistory>> GetBySpuIdAsync(Guid spuId, CancellationToken ct = default);

    /// <summary>按 SKU 标识查询其价格变更历史，按变更时间倒序。</summary>
    Task<IReadOnlyList<PriceHistory>> GetBySkuIdAsync(Guid skuId, CancellationToken ct = default);
}
