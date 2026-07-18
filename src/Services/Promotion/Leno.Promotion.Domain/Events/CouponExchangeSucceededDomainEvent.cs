using Leno.SharedKernel.Abstractions;

namespace Leno.Promotion.Domain.Events;

/// <summary>
/// 优惠券兑换成功领域事件，由 <see cref="Aggregates.UserCoupon"/> 聚合在 RecordExchangeSucceeded 方法中收集。
/// mapper 翻译为 <see cref="Leno.SharedContracts.Events.CouponExchangeSucceededEvent"/> 集成事件对外发布。
/// 消费方：积分域（正式扣减积分）。
/// </summary>
public sealed class CouponExchangeSucceededDomainEvent : DomainEventBase
{
    /// <summary>兑换请求标识。</summary>
    public Guid ExchangeId { get; init; }

    /// <summary>用户标识。</summary>
    public Guid UserId { get; init; }

    /// <summary>创建的优惠券标识（本聚合根标识）。</summary>
    public Guid CouponId { get; init; }

    public CouponExchangeSucceededDomainEvent(Guid exchangeId, Guid userId, Guid couponId)
        : base(couponId)
    {
        ExchangeId = exchangeId;
        UserId = userId;
        CouponId = couponId;
    }
}
