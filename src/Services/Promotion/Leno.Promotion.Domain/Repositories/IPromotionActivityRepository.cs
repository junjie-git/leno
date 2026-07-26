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
    /// 按多条件分页查询活动（运营后台），支持名称模糊、状态精确、时间区间过滤。
    /// </summary>
    /// <param name="name">名称模糊匹配关键词，null 或空白时忽略。</param>
    /// <param name="status">活动状态精确匹配，null 时忽略。</param>
    /// <param name="startTime">活动开始时间下界（>=），null 时忽略。</param>
    /// <param name="endTime">活动结束时间上界（<=），null 时忽略。</param>
    /// <param name="page">页码（从 1 开始）。</param>
    /// <param name="pageSize">每页条数。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>当前页的活动列表。</returns>
    Task<List<PromotionActivityAggregate>> QueryAsync(
        string? name,
        PromotionStatus? status,
        DateTime? startTime,
        DateTime? endTime,
        int page,
        int pageSize,
        CancellationToken ct = default);

    /// <summary>
    /// 按多条件统计活动总数（运营后台），筛选条件与 <see cref="QueryAsync"/> 一致。
    /// </summary>
    /// <param name="name">名称模糊匹配关键词，null 或空白时忽略。</param>
    /// <param name="status">活动状态精确匹配，null 时忽略。</param>
    /// <param name="startTime">活动开始时间下界（>=），null 时忽略。</param>
    /// <param name="endTime">活动结束时间上界（<=），null 时忽略。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>满足筛选条件的总记录数。</returns>
    Task<int> CountAsync(
        string? name,
        PromotionStatus? status,
        DateTime? startTime,
        DateTime? endTime,
        CancellationToken ct = default);
}
