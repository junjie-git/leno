using Leno.Promotion.Domain.Aggregates;
using Leno.Promotion.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using CouponAggregate = Leno.Promotion.Domain.Aggregates.Coupon;

namespace Leno.Promotion.Domain.Repositories;

/// <summary>
/// 优惠券模板仓储接口，管理 <see cref="Coupon"/> 聚合。
/// </summary>
public interface ICouponRepository : IRepository<CouponAggregate>
{
    /// <summary>
    /// 查询当前可领取的券模板（Enabled 且未过期且有剩余量），供买家领券页展示。
    /// </summary>
    Task<List<CouponAggregate>> GetReceivableAsync(DateTime now, CancellationToken ct = default);

    /// <summary>
    /// 按多条件分页查询券模板（运营后台），支持名称模糊、类型精确、状态精确过滤。
    /// </summary>
    /// <param name="name">名称模糊匹配关键词，null 或空白时忽略。</param>
    /// <param name="type">券类型精确匹配，null 时忽略。</param>
    /// <param name="status">券模板状态精确匹配，null 时忽略。</param>
    /// <param name="page">页码（从 1 开始）。</param>
    /// <param name="pageSize">每页条数。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>当前页的券模板列表。</returns>
    Task<List<CouponAggregate>> QueryAsync(
        string? name,
        CouponType? type,
        CouponTemplateStatus? status,
        int page,
        int pageSize,
        CancellationToken ct = default);

    /// <summary>
    /// 按多条件统计券模板总数（运营后台），筛选条件与 <see cref="QueryAsync"/> 一致。
    /// </summary>
    /// <param name="name">名称模糊匹配关键词，null 或空白时忽略。</param>
    /// <param name="type">券类型精确匹配，null 时忽略。</param>
    /// <param name="status">券模板状态精确匹配，null 时忽略。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>满足筛选条件的总记录数。</returns>
    Task<int> CountAsync(
        string? name,
        CouponType? type,
        CouponTemplateStatus? status,
        CancellationToken ct = default);

    /// <summary>
    /// 按标识集合批量查询券模板，单次 DB 往返，AsNoTracking 只读场景使用。
    /// 用于促销试算等循环内需要按多 CouponId 加载模板的场景，消除 N+1 查询。
    /// </summary>
    /// <param name="ids">券模板标识集合，去重后查询。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>命中的券模板列表（顺序不保证），空集合返回空列表。</returns>
    Task<List<CouponAggregate>> GetByIdsAsync(
        IEnumerable<Guid> ids,
        CancellationToken ct = default);
}
