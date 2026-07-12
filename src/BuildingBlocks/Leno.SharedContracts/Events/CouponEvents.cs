using Leno.SharedKernel.Abstractions;

namespace Leno.SharedContracts.Events;

/// <summary>
/// 积分兑换优惠券请求集成事件，积分域在兑换请求时发布。
/// 消费方：促销/优惠券域（创建优惠券）。
/// 同时实现 <see cref="IDomainEvent"/> 以便经发件箱模式在同一事务内持久化。
/// </summary>
public sealed class PointsExchangeCouponRequestedEvent : IntegrationEventBase, IDomainEvent
{
    /// <summary>兑换请求标识。</summary>
    public Guid ExchangeId { get; init; }

    /// <summary>用户标识。</summary>
    public Guid UserId { get; init; }

    /// <summary>优惠券模板标识。</summary>
    public Guid CouponTemplateId { get; init; }

    /// <summary>兑换所需积分数量。</summary>
    public int PointsRequired { get; init; }

    /// <summary>聚合根标识，用于发件箱归类。</summary>
    public Guid AggregateId => ExchangeId;

    public PointsExchangeCouponRequestedEvent() : base()
    {
    }

    public PointsExchangeCouponRequestedEvent(Guid exchangeId, Guid userId, Guid couponTemplateId, int pointsRequired)
        : base()
    {
        ExchangeId = exchangeId;
        UserId = userId;
        CouponTemplateId = couponTemplateId;
        PointsRequired = pointsRequired;
    }
}

/// <summary>
/// 优惠券兑换成功集成事件，促销/优惠券域在优惠券创建成功后发布。
/// 消费方：积分域（正式扣减积分）。
/// 同时实现 <see cref="IDomainEvent"/> 以便经发件箱模式在同一事务内持久化。
/// </summary>
public sealed class CouponExchangeSucceededEvent : IntegrationEventBase, IDomainEvent
{
    /// <summary>兑换请求标识。</summary>
    public Guid ExchangeId { get; init; }

    /// <summary>用户标识。</summary>
    public Guid UserId { get; init; }

    /// <summary>创建的优惠券标识。</summary>
    public Guid CouponId { get; init; }

    /// <summary>聚合根标识，用于发件箱归类。</summary>
    public Guid AggregateId => ExchangeId;

    public CouponExchangeSucceededEvent() : base()
    {
    }

    public CouponExchangeSucceededEvent(Guid exchangeId, Guid userId, Guid couponId)
        : base()
    {
        ExchangeId = exchangeId;
        UserId = userId;
        CouponId = couponId;
    }
}

/// <summary>
/// 优惠券兑换失败集成事件，促销/优惠券域在兑换失败时发布。
/// 消费方：积分域（释放冻结积分）。
/// 同时实现 <see cref="IDomainEvent"/> 以便经发件箱模式在同一事务内持久化。
/// </summary>
public sealed class CouponExchangeFailedEvent : IntegrationEventBase, IDomainEvent
{
    /// <summary>兑换请求标识。</summary>
    public Guid ExchangeId { get; init; }

    /// <summary>用户标识。</summary>
    public Guid UserId { get; init; }

    /// <summary>失败原因。</summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>聚合根标识，用于发件箱归类。</summary>
    public Guid AggregateId => ExchangeId;

    public CouponExchangeFailedEvent() : base()
    {
    }

    public CouponExchangeFailedEvent(Guid exchangeId, Guid userId, string reason)
        : base()
    {
        ExchangeId = exchangeId;
        UserId = userId;
        Reason = reason ?? string.Empty;
    }
}