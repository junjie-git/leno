using Leno.Product.Domain.Aggregates;
using Leno.Product.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;

namespace Leno.Product.Domain.Repositories;

/// <summary>
/// SPU 仓储接口，定义在领域层，由基础设施层实现。
/// 查询方法返回聚合根（含 SKU 集合），写操作不立即持久化，由工作单元统一提交。
/// </summary>
public interface ISPURepository : IRepository<SPU>
{
    /// <summary>按店铺标识查询该店铺全部商品（含 SKU）。</summary>
    Task<IReadOnlyList<SPU>> GetByShopIdAsync(Guid shopId, CancellationToken ct = default);

    /// <summary>按 SKU 标识查询其所属 SPU（含 SKU 集合），供防腐层查询 SKU 价格。</summary>
    Task<SPU?> GetBySkuIdAsync(Guid skuId, CancellationToken ct = default);

    /// <summary>
    /// 分页查询商品列表，支持按店铺、状态、分类过滤与关键词模糊匹配。
    /// </summary>
    /// <param name="shopId">店铺过滤，可空表示不限。</param>
    /// <param name="status">商品状态过滤，可空表示不限。</param>
    /// <param name="categoryId">分类过滤，可空表示不限。</param>
    /// <param name="keyword">标题关键词，可空。</param>
    /// <param name="page">页码，从 1 起。</param>
    /// <param name="pageSize">每页条数，最大 100。</param>
    Task<(IReadOnlyList<SPU> Items, int Total)> QueryAsync(
        Guid? shopId = null,
        ProductStatus? status = null,
        Guid? categoryId = null,
        string? keyword = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default);
}
