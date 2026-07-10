using Leno.ReviewAfterSales.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using ReviewAggregate = Leno.ReviewAfterSales.Domain.Aggregates.Review;

namespace Leno.ReviewAfterSales.Domain.Repositories;

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
}
