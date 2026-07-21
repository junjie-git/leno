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
    /// 按状态分页查询券模板（运营后台）。
    /// </summary>
    Task<List<CouponAggregate>> GetByStatusAsync(
        CouponTemplateStatus? status,
        int page,
        int pageSize,
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
