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
    /// 按 SKU 标识集合批量查询其所属 SPU（含 SKU 集合），单次 SQL 查询替代逐条调用。
    /// 用于消除 N+1 查询：调用方传入多个 skuId，仓储以 <c>WHERE EXISTS (SELECT 1 FROM SKU WHERE SKU.SpuId = SPU.Id AND SKU.Id IN @skuIds)</c> 单次返回。
    /// </summary>
    /// <param name="skuIds">SKU 标识集合，空集合返回空列表。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>包含任一指定 SKU 的 SPU 列表（去重，只读查询）。</returns>
    Task<IReadOnlyList<SPU>> GetBySkuIdsAsync(IReadOnlyCollection<Guid> skuIds, CancellationToken ct = default);

    /// <summary>
    /// 分页查询商品列表，支持按店铺、卖家、状态、分类过滤与关键词模糊匹配。
    /// </summary>
    /// <param name="shopId">店铺过滤，可空表示不限。</param>
    /// <param name="sellerId">卖家过滤，可空表示不限。</param>
    /// <param name="status">商品状态过滤，可空表示不限。</param>
    /// <param name="categoryId">分类过滤，可空表示不限。</param>
    /// <param name="keyword">标题关键词，可空。</param>
    /// <param name="page">页码，从 1 起。</param>
    /// <param name="pageSize">每页条数，最大 100。</param>
    Task<(IReadOnlyList<SPU> Items, int Total)> QueryAsync(
        Guid? shopId = null,
        Guid? sellerId = null,
        ProductStatus? status = null,
        Guid? categoryId = null,
        string? keyword = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default);
}
