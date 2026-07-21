using Leno.SharedKernel.Abstractions;

namespace Leno.PointsMembership.Domain.Events;

/// <summary>
/// 积分兑换优惠券请求领域事件，由 <see cref="Aggregates.PointsAccount.RequestExchangeCoupon"/> 内 Freeze 后追加。
/// 经发件箱模式在同一事务内持久化，由 IntegrationEventMapper 翻译为
/// <c>Leno.SharedContracts.Events.PointsExchangeCouponRequestedEvent</c> 集成事件发布给优惠券域。
/// </summary>
public sealed class PointsExchangeCouponRequestedDomainEvent : DomainEventBase
{
    /// <summary>兑换业务标识（聚合内同时作为 Freeze 的 orderId）。</summary>
    public Guid ExchangeId { get; init; }

    /// <summary>用户标识。</summary>
    public Guid UserId { get; init; }

    /// <summary>兑换目标优惠券模板标识。</summary>
    public Guid CouponTemplateId { get; init; }

    /// <summary>本次兑换所需积分数量。</summary>
    public int PointsRequired { get; init; }

    public PointsExchangeCouponRequestedDomainEvent(
        Guid exchangeId, Guid userId, Guid couponTemplateId, int pointsRequired)
        : base(exchangeId)
    {
        ExchangeId = exchangeId;
        UserId = userId;
        CouponTemplateId = couponTemplateId;
        PointsRequired = pointsRequired;
    }
}
