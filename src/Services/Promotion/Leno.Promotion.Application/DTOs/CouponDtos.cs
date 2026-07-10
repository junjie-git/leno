using Leno.Promotion.Domain.ValueObjects;

namespace Leno.Promotion.Application.DTOs;

/// <summary>
/// 优惠券模板 DTO。
/// </summary>
public sealed class CouponDto
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public CouponType Type { get; init; }

    public decimal FaceValue { get; init; }

    public decimal MinSpend { get; init; }

    public CouponValidityType ValidityType { get; init; }

    public DateTime? ValidFrom { get; init; }

    public DateTime? ValidTo { get; init; }

    public int? ValidDays { get; init; }

    public int TotalQty { get; init; }

    public int IssuedQty { get; init; }

    public CouponTemplateStatus Status { get; init; }

    public DateTime CreatedAt { get; init; }
}

/// <summary>
/// 创建优惠券模板 DTO。
/// </summary>
public sealed class CreateCouponDto
{
    public string Name { get; init; } = string.Empty;

    public CouponType Type { get; init; }

    public decimal FaceValue { get; init; }

    public decimal MinSpend { get; init; }

    public CouponValidityType ValidityType { get; init; }

    public DateTime? ValidFrom { get; init; }

    public DateTime? ValidTo { get; init; }

    public int? ValidDays { get; init; }

    public int TotalQty { get; init; }
}

/// <summary>
/// 更新优惠券模板 DTO。
/// </summary>
public sealed class UpdateCouponDto
{
    public string Name { get; init; } = string.Empty;

    public CouponType Type { get; init; }

    public decimal FaceValue { get; init; }

    public decimal MinSpend { get; init; }

    public CouponValidityType ValidityType { get; init; }

    public DateTime? ValidFrom { get; init; }

    public DateTime? ValidTo { get; init; }

    public int? ValidDays { get; init; }
}

/// <summary>
/// 用户优惠券 DTO。
/// </summary>
public sealed class UserCouponDto
{
    public Guid Id { get; init; }

    public Guid UserId { get; init; }

    public Guid CouponId { get; init; }

    public CouponStatus Status { get; init; }

    public string Source { get; init; } = string.Empty;

    public DateTime ReceivedAt { get; init; }

    public DateTime? UsedAt { get; init; }

    public DateTime? ExpiredAt { get; init; }
}
