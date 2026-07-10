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
}
