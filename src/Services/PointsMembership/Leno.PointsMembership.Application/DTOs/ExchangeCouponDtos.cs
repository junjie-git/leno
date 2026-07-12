namespace Leno.PointsMembership.Application.DTOs;

/// <summary>
/// 积分兑换优惠券入参 DTO。
/// </summary>
public sealed class ExchangeCouponDto
{
    public Guid UserId { get; init; }

    public Guid CouponTemplateId { get; init; }

    public int PointsRequired { get; init; }
}

/// <summary>
/// 积分兑换优惠券结果 DTO。
/// </summary>
public sealed class ExchangeCouponResultDto
{
    public Guid ExchangeId { get; init; }

    public Guid UserId { get; init; }

    public Guid CouponTemplateId { get; init; }

    public int PointsFrozen { get; init; }

    public string Status { get; init; } = "Pending";
}
