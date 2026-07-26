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
    /// 按多条件分页查询秒杀活动（运营后台），支持名称模糊与状态精确过滤。
    /// </summary>
    /// <param name="name">名称模糊匹配关键词，null 或空白时忽略。</param>
    /// <param name="status">活动状态精确匹配，null 时忽略。</param>
    /// <param name="page">页码（从 1 开始）。</param>
    /// <param name="pageSize">每页条数。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>当前页的秒杀活动列表。</returns>
    Task<List<SeckillActivityAggregate>> QueryAsync(
        string? name,
        SeckillStatus? status,
        int page,
        int pageSize,
        CancellationToken ct = default);

    /// <summary>
    /// 按多条件统计秒杀活动总数（运营后台），筛选条件与 <see cref="QueryAsync"/> 一致。
    /// </summary>
    /// <param name="name">名称模糊匹配关键词，null 或空白时忽略。</param>
    /// <param name="status">活动状态精确匹配，null 时忽略。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>满足筛选条件的总记录数。</returns>
    Task<int> CountAsync(
        string? name,
        SeckillStatus? status,
        CancellationToken ct = default);

    /// <summary>
    /// 按 SKU 标识查询当前进行中的秒杀活动（商品详情页展示秒杀价）。
    /// </summary>
    Task<SeckillActivityAggregate?> GetActiveBySkuIdAsync(Guid skuId, DateTime now, CancellationToken ct = default);
}
