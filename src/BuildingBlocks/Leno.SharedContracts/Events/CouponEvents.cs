namespace Leno.SharedContracts.Events;

/// <summary>
/// 积分兑换优惠券请求集成事件，积分域在兑换请求时发布。
/// 消费方：促销/优惠券域（创建优惠券）。
/// </summary>
public sealed class PointsExchangeCouponRequestedEvent : IntegrationEventBase
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
/// </summary>
public sealed class CouponExchangeSucceededEvent : IntegrationEventBase
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
/// </summary>
public sealed class CouponExchangeFailedEvent : IntegrationEventBase
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

/// <summary>
/// 优惠券模板创建集成事件，运营创建优惠券模板后由促销域发布。
/// 消费方：促销域读模型同步（索引到 ES leno_coupons）。
/// 事件契约定义在共享层，变更需所有消费方协商。
/// </summary>
public sealed class CouponCreatedEvent : IntegrationEventBase
{
    /// <summary>优惠券模板标识。</summary>
    public Guid CouponId { get; init; }

    /// <summary>券名称。</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>券类型名称（FixedAmount/Percentage/FullReduction）。</summary>
    public string CouponType { get; init; } = string.Empty;

    /// <summary>面值（金额或折扣率）。</summary>
    public decimal FaceValue { get; init; }

    /// <summary>使用门槛（满 MinSpend 方可用券），0 表示无门槛。</summary>
    public decimal MinSpend { get; init; }

    /// <summary>固定时段有效期起始（UTC，可空表示相对天数类型）。</summary>
    public DateTime? ValidFrom { get; init; }

    /// <summary>固定时段有效期截止（UTC，可空表示相对天数类型）。</summary>
    public DateTime? ValidTo { get; init; }

    /// <summary>发放总量，-1 表示不限量。</summary>
    public int TotalQty { get; init; }

    /// <summary>券模板状态名称（Enabled/Disabled）。</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>聚合根标识，用于发件箱归类。</summary>
    public Guid AggregateId => CouponId;

    public CouponCreatedEvent() : base()
    {
    }

    public CouponCreatedEvent(
        Guid couponId,
        string name,
        string couponType,
        decimal faceValue,
        decimal minSpend,
        DateTime? validFrom,
        DateTime? validTo,
        int totalQty,
        string status) : base()
    {
        CouponId = couponId;
        Name = name ?? string.Empty;
        CouponType = couponType ?? string.Empty;
        FaceValue = faceValue;
        MinSpend = minSpend;
        ValidFrom = validFrom;
        ValidTo = validTo;
        TotalQty = totalQty;
        Status = status ?? string.Empty;
    }
}

/// <summary>
/// 优惠券模板停用集成事件，运营停用优惠券模板后由促销域发布。
/// 消费方：促销域读模型同步（从 ES leno_coupons 删除文档）。
/// 事件契约定义在共享层，变更需所有消费方协商。
/// </summary>
public sealed class CouponDisabledEvent : IntegrationEventBase
{
    /// <summary>优惠券模板标识。</summary>
    public Guid CouponId { get; init; }

    /// <summary>停用时间（UTC）。</summary>
    public DateTime DisabledAt { get; init; }

    /// <summary>聚合根标识，用于发件箱归类。</summary>
    public Guid AggregateId => CouponId;

    public CouponDisabledEvent() : base()
    {
    }

    public CouponDisabledEvent(Guid couponId, DateTime disabledAt) : base()
    {
        CouponId = couponId;
        DisabledAt = disabledAt;
    }
}