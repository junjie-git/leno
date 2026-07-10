using Leno.Promotion.Domain.Aggregates;
using Leno.Promotion.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using SeckillActivityAggregate = Leno.Promotion.Domain.Aggregates.SeckillActivity;

namespace Leno.Promotion.Domain.Repositories;

/// <summary>
/// 秒杀活动仓储接口，管理 <see cref="SeckillActivity"/> 聚合。
/// </summary>
public interface ISeckillActivityRepository : IRepository<SeckillActivityAggregate>
{
    /// <summary>
    /// 查询当前进行中（Active 且在时间区间内）的秒杀活动，供买家侧列表展示。
    /// </summary>
    Task<List<SeckillActivityAggregate>> GetActiveAsync(DateTime now, CancellationToken ct = default);

    /// <summary>
    /// 按状态分页查询秒杀活动（运营后台）。
    /// </summary>
    Task<List<SeckillActivityAggregate>> GetByStatusAsync(
        SeckillStatus? status,
        int page,
        int pageSize,
        CancellationToken ct = default);

    /// <summary>
    /// 按 SKU 标识查询当前进行中的秒杀活动（商品详情页展示秒杀价）。
    /// </summary>
    Task<SeckillActivityAggregate?> GetActiveBySkuIdAsync(Guid skuId, DateTime now, CancellationToken ct = default);
}
