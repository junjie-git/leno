using Leno.Promotion.Domain.Events;
using Leno.Promotion.Domain.Exceptions;
using Leno.Promotion.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;

namespace Leno.Promotion.Domain.Aggregates;

public sealed class UserCoupon : AggregateRoot
{
    public Guid UserId { get; private set; }
    public Guid CouponId { get; private set; }
    public CouponStatus Status { get; private set; }
    public string Source { get; private set; } = "Manual";
    public DateTime ReceivedAt { get; private set; }
    public DateTime? UsedAt { get; private set; }
    public Guid? UsedOrderId { get; private set; }
    public Guid? LockedOrderId { get; private set; }
    public DateTime? ExpiredAt { get; private set; }

    /// <summary>
    /// 积分兑换请求标识（仅积分兑换来源券非空），用于幂等去重与 <see cref="GetByExchangeIdAsync"/> 查询。
    /// 非积分兑换来源券为 null。
    /// </summary>
    public Guid? ExchangeId { get; private set; }

    private UserCoupon() { }
    private UserCoupon(Guid id) : base(id) { }

    public static UserCoupon Receive(
        Guid userCouponId, Guid userId, Guid couponId, string source, DateTime expiredAt)
    {
        if (userId == Guid.Empty)
            throw new PromotionDomainException("UserId 不可为空", "USER_COUPON_USER_EMPTY");
        if (couponId == Guid.Empty)
            throw new PromotionDomainException("CouponId 不可为空", "USER_COUPON_COUPON_EMPTY");
        var now = DateTime.UtcNow;
        if (expiredAt <= now)
            throw new PromotionDomainException("券已过期，不可领取", "USER_COUPON_EXPIRED");
        var userCouponId2 = userCouponId == Guid.Empty ? Guid.NewGuid() : userCouponId;
        var userCoupon = new UserCoupon(userCouponId2)
        {
            UserId = userId, CouponId = couponId, Status = CouponStatus.Unused,
            Source = string.IsNullOrWhiteSpace(source) ? "Manual" : source,
            ReceivedAt = now, ExpiredAt = expiredAt
        };
        userCoupon.AddDomainEvent(new CouponIssuedEvent(userCouponId2, couponId, userId, now));
        return userCoupon;
    }

    /// <summary>
    /// 工厂方法（积分兑换专用），创建用户券并绑定兑换标识，同时发布兑换成功领域事件。
    /// 领域事件经发件箱翻译为 <see cref="Leno.SharedContracts.Events.CouponExchangeSucceededEvent"/> 集成事件对外发布，
    /// 消费方：积分域正式扣减积分。
    /// </summary>
    /// <param name="userCouponId">用户券标识。</param>
    /// <param name="userId">用户标识。</param>
    /// <param name="couponId">券模板标识。</param>
    /// <param name="source">来源标记。</param>
    /// <param name="expiredAt">过期时间。</param>
    /// <param name="exchangeId">积分兑换请求标识，用于幂等去重与事件关联。</param>
    public static UserCoupon Receive(
        Guid userCouponId, Guid userId, Guid couponId, string source, DateTime expiredAt, Guid exchangeId)
    {
        if (exchangeId == Guid.Empty)
        {
            throw new PromotionDomainException("ExchangeId 不可为空", "USER_COUPON_EXCHANGE_EMPTY");
        }

        var userCoupon = Receive(userCouponId, userId, couponId, source, expiredAt);
        userCoupon.ExchangeId = exchangeId;
        userCoupon.RecordExchangeSucceeded(exchangeId);
        return userCoupon;
    }

    public void Lock(Guid orderId)
    {
        if (orderId == Guid.Empty)
            throw new PromotionDomainException("OrderId 不可为空", "USER_COUPON_ORDER_EMPTY");
        if (Status != CouponStatus.Unused)
            throw new PromotionDomainException($"当前状态 {Status} 不可锁定，仅 Unused 可锁定", "USER_COUPON_LOCK_INVALID");
        EnsureNotExpired();
        Status = CouponStatus.Locked;
        LockedOrderId = orderId;
    }

    public void Consume(Guid orderId)
    {
        if (orderId == Guid.Empty)
            throw new PromotionDomainException("OrderId 不可为空", "USER_COUPON_ORDER_EMPTY");
        if (Status != CouponStatus.Locked)
            throw new PromotionDomainException($"当前状态 {Status} 不可核销，仅 Locked 可核销", "USER_COUPON_CONSUME_INVALID");
        if (LockedOrderId.HasValue && LockedOrderId.Value != orderId)
            throw new PromotionDomainException("核销订单与锁定订单不一致", "USER_COUPON_ORDER_MISMATCH");
        Status = CouponStatus.Used;
        UsedAt = DateTime.UtcNow;
        UsedOrderId = orderId;
    }

    public void Release()
    {
        if (Status != CouponStatus.Locked)
            throw new PromotionDomainException($"当前状态 {Status} 不可释放，仅 Locked 可释放", "USER_COUPON_RELEASE_INVALID");
        LockedOrderId = null;
        if (ExpiredAt.HasValue && ExpiredAt.Value <= DateTime.UtcNow)
        {
            Status = CouponStatus.Expired;
            return;
        }
        Status = CouponStatus.Unused;
    }

    public void Expire()
    {
        if (Status == CouponStatus.Used || Status == CouponStatus.Expired)
            throw new PromotionDomainException($"当前状态 {Status} 不可标记过期", "USER_COUPON_EXPIRE_INVALID");
        Status = CouponStatus.Expired;
        ExpiredAt = DateTime.UtcNow;
        LockedOrderId = null;
    }

    public void Return()
    {
        if (Status != CouponStatus.Used)
            throw new PromotionDomainException($"当前状态 {Status} 不可退还，仅 Used 可退还", "USER_COUPON_RETURN_INVALID");
        if (IsExpiredAt(DateTime.UtcNow))
        {
            Status = CouponStatus.Expired;
            UsedOrderId = null;
            UsedAt = null;
            LockedOrderId = null;
            return;
        }
        Status = CouponStatus.Unused;
        UsedOrderId = null;
        UsedAt = null;
        LockedOrderId = null;
    }

    /// <summary>
    /// 记录积分兑换成功领域事件（由积分兑换工厂 <see cref="Receive(Guid, Guid, Guid, string, DateTime, Guid)"/> 内部调用）。
    /// 事件经发件箱翻译为 CouponExchangeSucceededEvent 集成事件对外发布。
    /// </summary>
    /// <param name="exchangeId">积分兑换请求标识。</param>
    public void RecordExchangeSucceeded(Guid exchangeId)
    {
        if (exchangeId == Guid.Empty)
        {
            throw new PromotionDomainException("ExchangeId 不可为空", "USER_COUPON_EXCHANGE_EMPTY");
        }

        AddDomainEvent(new CouponExchangeSucceededDomainEvent(exchangeId, UserId, Id));
    }

    public bool IsExpiredAt(DateTime now) => ExpiredAt.HasValue && now >= ExpiredAt.Value;

    private void EnsureNotExpired()
    {
        if (IsExpiredAt(DateTime.UtcNow))
            throw new PromotionDomainException("券已过期", "USER_COUPON_EXPIRED");
    }
}