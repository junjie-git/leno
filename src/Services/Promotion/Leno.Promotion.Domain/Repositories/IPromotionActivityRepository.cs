using Leno.Promotion.Domain.Aggregates;
using Leno.Promotion.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using PromotionActivityAggregate = Leno.Promotion.Domain.Aggregates.PromotionActivity;

namespace Leno.Promotion.Domain.Repositories;

/// <summary>
/// 满减/促销活动仓储接口，管理 <see cref="PromotionActivity"/> 聚合。
/// </summary>
public interface IPromotionActivityRepository : IRepository<PromotionActivityAggregate>
{
    /// <summary>
    /// 查询当前 Active 且在有效时间区间内的满减活动（含规则），供防腐层试算。
    /// </summary>
    Task<List<PromotionActivityAggregate>> GetActiveAsync(DateTime now, CancellationToken ct = default);

    /// <summary>
    /// 按状态分页查询活动（运营后台）。
    /// </summary>
    Task<List<PromotionActivityAggregate>> GetByStatusAsync(
        PromotionStatus? status,
        int page,
        int pageSize,
        CancellationToken ct = default);
}
