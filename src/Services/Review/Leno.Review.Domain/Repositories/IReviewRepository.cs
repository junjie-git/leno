using Leno.Review.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using ReviewAggregate = Leno.Review.Domain.Aggregates.Review;

namespace Leno.Review.Domain.Repositories;

/// <summary>
/// 评价仓储接口，管理 <see cref="Aggregates.Review"/> 聚合。
/// 继承 <see cref="IRepository{T}"/> 获得 GetByIdAsync/AddAsync/UpdateAsync/RemoveAsync 基础能力。
/// </summary>
public interface IReviewRepository : IRepository<ReviewAggregate>
{
    /// <summary>
    /// 按订单行标识查询主评价。
    /// </summary>
    /// <param name="orderLineId">订单行标识。</param>
    Task<ReviewAggregate?> GetByOrderLineAsync(Guid orderLineId, CancellationToken ct = default);

    /// <summary>
    /// 判断该订单行是否已存在主评价。
    /// </summary>
    /// <param name="orderLineId">订单行标识。</param>
    Task<bool> ExistsByOrderLineAsync(Guid orderLineId, CancellationToken ct = default);

    /// <summary>
    /// 分页条件查询评价列表。
    /// </summary>
    /// <param name="spuId">商品 SPU 标识过滤，为空不过滤。</param>
    /// <param name="userId">评价人标识过滤，为空不过滤。</param>
    /// <param name="status">审核状态过滤，为空不过滤。</param>
    /// <param name="page">页码（从 1 起）。</param>
    /// <param name="pageSize">每页大小。</param>
    Task<List<ReviewAggregate>> QueryAsync(Guid? spuId, Guid? userId, ReviewStatus? status, int page, int pageSize, CancellationToken ct = default);

    /// <summary>
    /// 条件查询评价总数（配合 <see cref="QueryAsync"/> 分页）。
    /// </summary>
    Task<int> CountAsync(Guid? spuId, Guid? userId, ReviewStatus? status, CancellationToken ct = default);

    /// <summary>
    /// 按 SPU 标识查询全部评价（不分页，跨 BC 内部查询用）。
    /// 可选审核状态过滤；为空返回所有状态评价。
    /// </summary>
    /// <param name="spuId">商品 SPU 标识。</param>
    /// <param name="status">审核状态过滤，为空不过滤。</param>
    Task<List<ReviewAggregate>> GetBySpuIdAsync(Guid spuId, ReviewStatus? status = null, CancellationToken ct = default);

    /// <summary>
    /// 按 SPU 标识查询评价聚合快照（合并审计 3.4：SQL 聚合替代内存计算）。
    /// 仅聚合 Approved 状态评价；无可见评价返回 null。
    /// </summary>
    /// <param name="spuId">商品 SPU 标识。</param>
    Task<ProductRatingSnapshot?> GetRatingSnapshotAsync(Guid spuId, CancellationToken ct = default);

    /// <summary>
    /// 按订单标识查询全部评价（不分页，跨 BC 内部查询用）。
    /// 可选审核状态过滤；为空返回所有状态评价。
    /// </summary>
    /// <param name="orderId">订单标识。</param>
    /// <param name="status">审核状态过滤，为空不过滤。</param>
    Task<List<ReviewAggregate>> GetByOrderIdAsync(Guid orderId, ReviewStatus? status = null, CancellationToken ct = default);
}
