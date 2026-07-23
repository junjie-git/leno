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
    /// <param name="userId">用户标识。</param>
    /// <param name="status">券状态过滤（null 表示不限状态）。</param>
    /// <param name="now">
    /// 当前 UTC 时间。传入非 null 值时，SQL 层下推 <c>ExpiredAt &gt; now</c> 过滤已过期券，消除内存过滤。
    /// 传 null（缺省）时不过滤过期时间，调用方自行在内存中处理（向后兼容）。
    /// </param>
    /// <param name="ct">取消令牌。</param>
    Task<List<UserCouponAggregate>> GetByUserAsync(
        Guid userId,
        CouponStatus? status,
        DateTime? now = null,
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
    /// 按锁定订单批量查询用户券列表（订单取消/释放时定位全部锁定券，支持一单多券）。
    /// </summary>
    Task<List<UserCouponAggregate>> GetAllByLockedOrderIdAsync(Guid orderId, CancellationToken ct = default);

    /// <summary>
    /// 按核销订单查询用户券（幂等校验：是否已核销）。
    /// </summary>
    Task<UserCouponAggregate?> GetByUsedOrderIdAsync(Guid orderId, CancellationToken ct = default);

    /// <summary>
    /// 按积分兑换请求标识查询用户券（积分兑换消费者幂等去重）。
    /// 仅积分兑换来源券持有 ExchangeId，非兑换券返回 null。
    /// </summary>
    /// <param name="exchangeId">积分兑换请求标识。</param>
    Task<UserCouponAggregate?> GetByExchangeIdAsync(Guid exchangeId, CancellationToken ct = default);

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
