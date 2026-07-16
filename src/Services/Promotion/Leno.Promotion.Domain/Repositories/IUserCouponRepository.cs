using Leno.Promotion.Domain.Aggregates;
using Leno.Promotion.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using UserCouponAggregate = Leno.Promotion.Domain.Aggregates.UserCoupon;

namespace Leno.Promotion.Domain.Repositories;

/// <summary>
/// 用户优惠券仓储接口，管理 <see cref="UserCoupon"/> 聚合。
/// </summary>
public interface IUserCouponRepository : IRepository<UserCouponAggregate>
{
    /// <summary>
    /// 查询买家持有的全部用户券（按状态可选过滤），按领取时间倒序。
    /// </summary>
    Task<List<UserCouponAggregate>> GetByUserAsync(
        Guid userId,
        CouponStatus? status,
        CancellationToken ct = default);

    /// <summary>
    /// 查询买家是否已领取指定券模板（用于重复领取校验）。
    /// </summary>
    Task<bool> ExistsAsync(Guid userId, Guid couponId, CancellationToken ct = default);

    /// <summary>
    /// 按买家与券模板查询用户券（下单锁定时定位待锁定券，配合 (UserId, CouponId) 唯一索引至多返回一张）。
    /// </summary>
    Task<UserCouponAggregate?> GetByUserIdAndCouponIdAsync(Guid userId, Guid couponId, CancellationToken ct = default);

    /// <summary>
    /// 按锁定订单查询用户券（订单支付/取消事件消费时定位券）。
    /// </summary>
    Task<UserCouponAggregate?> GetByLockedOrderIdAsync(Guid orderId, CancellationToken ct = default);

    /// <summary>
    /// 按核销订单查询用户券（幂等校验：是否已核销）。
    /// </summary>
    Task<UserCouponAggregate?> GetByUsedOrderIdAsync(Guid orderId, CancellationToken ct = default);

    /// <summary>
    /// 分页查询已领取但未使用且已过期的用户券，用于定时任务批量标记过期。
    /// </summary>
    /// <param name="skip">跳过的记录数。</param>
    /// <param name="take">每次取回的记录数。</param>
    Task<List<UserCouponAggregate>> GetExpiredUnusedCouponsAsync(
        int skip,
        int take,
        CancellationToken ct = default);
}
