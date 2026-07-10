using Leno.SharedKernel.Abstractions;

namespace Leno.Promotion.Domain.Events;

/// <summary>
/// 优惠券领取本地领域事件，用户领取券时由 <see cref="Aggregates.UserCoupon"/> 聚合附加。
/// 非跨上下文事件，仅在本上下文内消费（如本地读模型同步或后续通知预留）。
/// </summary>
public sealed class CouponIssuedEvent : DomainEventBase
{
    /// <summary>券模板标识。</summary>
    public Guid CouponId { get; }

    /// <summary>领取用户标识。</summary>
    public Guid UserId { get; }

    /// <summary>用户券标识（本事件所属聚合根）。</summary>
    public Guid UserCouponId { get; }

    /// <summary>领取时间（UTC）。</summary>
    public DateTime ReceivedAt { get; }

    public CouponIssuedEvent(Guid userCouponId, Guid couponId, Guid userId, DateTime receivedAt)
        : base(userCouponId)
    {
        UserCouponId = userCouponId;
        CouponId = couponId;
        UserId = userId;
        ReceivedAt = receivedAt;
    }
}
