namespace Leno.Points.Application.DTOs;

/// <summary>
/// 积分兑换优惠券入参 DTO。
/// </summary>
public sealed class ExchangeCouponDto
{
    /// <summary>发起兑换的用户标识。</summary>
    public Guid UserId { get; init; }

    /// <summary>优惠券模板标识。</summary>
    public Guid CouponTemplateId { get; init; }

    /// <summary>本次兑换需要的积分数量。</summary>
    public int PointsRequired { get; init; }
}

/// <summary>
/// 积分兑换优惠券结果 DTO。
/// </summary>
public sealed class ExchangeCouponResultDto
{
    /// <summary>兑换记录标识。</summary>
    public Guid ExchangeId { get; init; }

    /// <summary>发起兑换的用户标识。</summary>
    public Guid UserId { get; init; }

    /// <summary>优惠券模板标识。</summary>
    public Guid CouponTemplateId { get; init; }

    /// <summary>本次冻结/扣减的积分数量。</summary>
    public int PointsFrozen { get; init; }

    /// <summary>兑换状态，初始为 Pending。</summary>
    public string Status { get; init; } = "Pending";
}
