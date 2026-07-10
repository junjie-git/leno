using Leno.Promotion.Domain.Events;
using Leno.Promotion.Domain.Exceptions;
using Leno.Promotion.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;

namespace Leno.Promotion.Domain.Aggregates;

/// <summary>
/// 用户优惠券聚合根，描述用户领取的一张券的生命周期。
/// 聚合标识 <see cref="Entity.Id"/> 即对外 <c>UserCouponId</c>。
/// 跨聚合的重复领取校验与剩余量校验由应用层协调，本聚合负责状态机不变量。
/// </summary>
public sealed class UserCoupon : AggregateRoot
{
    /// <summary>所属买家账号标识。</summary>
    public Guid UserId { get; private set; }

    /// <summary>关联的券模板标识。</summary>
    public Guid CouponId { get; private set; }

    /// <summary>用户券状态。</summary>
    public CouponStatus Status { get; private set; }

    /// <summary>领取来源（如 Manual/Activity/Campaign）。</summary>
    public string Source { get; private set; } = "Manual";

    /// <summary>领取时间（UTC）。</summary>
    public DateTime ReceivedAt { get; private set; }

    /// <summary>核销时间（UTC），已使用态填充。</summary>
    public DateTime? UsedAt { get; private set; }

    /// <summary>核销时关联的订单标识。</summary>
    public Guid? UsedOrderId { get; private set; }

    /// <summary>锁定时关联的订单标识。</summary>
    public Guid? LockedOrderId { get; private set; }

    /// <summary>过期时间（UTC）。</summary>
    public DateTime? ExpiredAt { get; private set; }

    /// <summary>EF Core 无参构造。</summary>
    private UserCoupon() { }

    private UserCoupon(Guid id) : base(id) { }

    /// <summary>
    /// 工厂方法，领取一张券。
    /// 跨聚合的重复领取与剩余量校验由应用层在调用前完成，本方法仅校验本聚合可判定的不变量。
    /// </summary>
    /// <param name="userCouponId">用户券标识，由应用层生成。</param>
    /// <param name="userId">买家标识。</param>
    /// <param name="couponId">券模板标识。</param>
    /// <param name="source">领取来源。</param>
    /// <param name="expiredAt">过期时间（UTC），由券模板 ComputeExpiredAt 计算。</param>
    public static UserCoupon Receive(
        Guid userCouponId,
        Guid userId,
        Guid couponId,
        string source,
        DateTime expiredAt)
    {
        if (userId == Guid.Empty)
        {
            throw new PromotionDomainException("UserId 不可为空", "USER_COUPON_USER_EMPTY");
        }

        if (couponId == Guid.Empty)
        {
            throw new PromotionDomainException("CouponId 不可为空", "USER_COUPON_COUPON_EMPTY");
        }

        var now = DateTime.UtcNow;
        if (expiredAt <= now)
        {
            throw new PromotionDomainException("券已过期，不可领取", "USER_COUPON_EXPIRED");
        }

        var userCouponId2 = userCouponId == Guid.Empty ? Guid.NewGuid() : userCouponId;
        var userCoupon = new UserCoupon(userCouponId2)
        {
            UserId = userId,
            CouponId = couponId,
            Status = CouponStatus.Unused,
            Source = string.IsNullOrWhiteSpace(source) ? "Manual" : source,
            ReceivedAt = now,
            ExpiredAt = expiredAt
        };

        userCoupon.AddDomainEvent(new CouponIssuedEvent(userCouponId2, couponId, userId, now));
        return userCoupon;
    }

    /// <summary>
    /// 下单锁定，待支付期间不可他用。仅 Unused 态可锁定。
    /// </summary>
    /// <param name="orderId">关联订单标识。</param>
    public void Lock(Guid orderId)
    {
        if (orderId == Guid.Empty)
        {
            throw new PromotionDomainException("OrderId 不可为空", "USER_COUPON_ORDER_EMPTY");
        }

        if (Status != CouponStatus.Unused)
        {
            throw new PromotionDomainException(
                $"当前状态 {Status} 不可锁定，仅 Unused 可锁定",
                "USER_COUPON_LOCK_INVALID");
        }

        EnsureNotExpired();

        Status = CouponStatus.Locked;
        LockedOrderId = orderId;
    }

    /// <summary>
    /// 支付成功核销，置已使用态。仅 Locked 态可核销。
    /// </summary>
    /// <param name="orderId">关联订单标识，须与锁定订单一致。</param>
    public void Consume(Guid orderId)
    {
        if (orderId == Guid.Empty)
        {
            throw new PromotionDomainException("OrderId 不可为空", "USER_COUPON_ORDER_EMPTY");
        }

        if (Status != CouponStatus.Locked)
        {
            throw new PromotionDomainException(
                $"当前状态 {Status} 不可核销，仅 Locked 可核销",
                "USER_COUPON_CONSUME_INVALID");
        }

        if (LockedOrderId.HasValue && LockedOrderId.Value != orderId)
        {
            throw new PromotionDomainException(
                "核销订单与锁定订单不一致",
                "USER_COUPON_ORDER_MISMATCH");
        }

        Status = CouponStatus.Used;
        UsedAt = DateTime.UtcNow;
        UsedOrderId = orderId;
    }

    /// <summary>
    /// 订单取消退还，回到未使用态。仅 Locked 态可释放。
    /// 若释放时已超过有效期，则直接置过期态。
    /// </summary>
    public void Release()
    {
        if (Status != CouponStatus.Locked)
        {
            throw new PromotionDomainException(
                $"当前状态 {Status} 不可释放，仅 Locked 可释放",
                "USER_COUPON_RELEASE_INVALID");
        }

        LockedOrderId = null;

        if (ExpiredAt.HasValue && ExpiredAt.Value <= DateTime.UtcNow)
        {
            Status = CouponStatus.Expired;
            return;
        }

        Status = CouponStatus.Unused;
    }

    /// <summary>
    /// 标记过期，仅 Unused/Locked 态可标记过期。
    /// </summary>
    public void Expire()
    {
        if (Status == CouponStatus.Used || Status == CouponStatus.Expired)
        {
            throw new PromotionDomainException(
                $"当前状态 {Status} 不可标记过期",
                "USER_COUPON_EXPIRE_INVALID");
        }

        Status = CouponStatus.Expired;
        ExpiredAt = DateTime.UtcNow;
        LockedOrderId = null;
    }

    /// <summary>判断当前是否在有效期内。</summary>
    public bool IsExpiredAt(DateTime now) => ExpiredAt.HasValue && now >= ExpiredAt.Value;

    private void EnsureNotExpired()
    {
        if (IsExpiredAt(DateTime.UtcNow))
        {
            throw new PromotionDomainException("券已过期", "USER_COUPON_EXPIRED");
        }
    }
}
