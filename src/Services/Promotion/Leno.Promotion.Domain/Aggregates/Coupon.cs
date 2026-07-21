using Leno.Promotion.Domain.Exceptions;
using Leno.Promotion.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;

namespace Leno.Promotion.Domain.Aggregates;

/// <summary>
/// 优惠券模板聚合根，运营创建的券模板，管理发放总量与已发放量。
/// 聚合标识 <see cref="Entity.Id"/> 即对外 <c>CouponId</c>。
/// </summary>
public sealed class Coupon : AggregateRoot
{
    /// <summary>券名称。</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>券类型（固定金额/折扣/满减）。</summary>
    public CouponType Type { get; private set; }

    /// <summary>
    /// 面值。
    /// FixedAmount/FullReduction 类型为金额（元）；Percentage 类型为折扣率（0-100）。
    /// </summary>
    public decimal FaceValue { get; private set; }

    /// <summary>使用门槛（满 MinSpend 方可用券），0 表示无门槛。</summary>
    public decimal MinSpend { get; private set; }

    /// <summary>有效期类型（固定时段/相对天数）。</summary>
    public CouponValidityType ValidityType { get; private set; }

    /// <summary>固定时段有效期起始（ValidityType=FixedPeriod 时生效）。</summary>
    public DateTime? ValidFrom { get; private set; }

    /// <summary>固定时段有效期截止（ValidityType=FixedPeriod 时生效）。</summary>
    public DateTime? ValidTo { get; private set; }

    /// <summary>相对天数（ValidityType=RelativeDays 时生效），自领取之日起 ValidDays 天内有效。</summary>
    public int? ValidDays { get; private set; }

    /// <summary>发放总量，-1 表示不限量。</summary>
    public int TotalQty { get; private set; }

    /// <summary>已发放数量。</summary>
    public int IssuedQty { get; private set; }

    /// <summary>券模板状态（启用/停用）。</summary>
    public CouponTemplateStatus Status { get; private set; }

    /// <summary>EF Core 无参构造。</summary>
    private Coupon() { }

    private Coupon(Guid id) : base(id) { }

    /// <summary>
    /// 工厂方法，创建优惠券模板，初始状态为 Enabled、已发放 0。
    /// </summary>
    /// <param name="couponId">券模板标识，由应用层生成。</param>
    /// <param name="name">券名称。</param>
    /// <param name="type">券类型。</param>
    /// <param name="faceValue">面值（金额或折扣率）。</param>
    /// <param name="minSpend">使用门槛，0 表示无门槛。</param>
    /// <param name="validityType">有效期类型。</param>
    /// <param name="validFrom">固定时段起始（FixedPeriod 必填）。</param>
    /// <param name="validTo">固定时段截止（FixedPeriod 必填）。</param>
    /// <param name="validDays">相对天数（RelativeDays 必填，&gt; 0）。</param>
    /// <param name="totalQty">发放总量，&lt; 0 表示不限量。</param>
    public static Coupon Create(
        Guid couponId,
        string name,
        CouponType type,
        decimal faceValue,
        decimal minSpend,
        CouponValidityType validityType,
        DateTime? validFrom,
        DateTime? validTo,
        int? validDays,
        int totalQty)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new PromotionDomainException("券名称不可为空", "COUPON_NAME_EMPTY");
        }

        ValidateFaceValue(type, faceValue);

        if (minSpend < 0)
        {
            throw new PromotionDomainException("使用门槛不可为负", "COUPON_MIN_SPEND_INVALID");
        }

        ValidateValidity(validityType, validFrom, validTo, validDays);

        if (totalQty < -1 || totalQty == 0)
        {
            throw new PromotionDomainException("发放总量须为正数或 -1（不限量）", "COUPON_TOTAL_QTY_INVALID");
        }

        return new Coupon(couponId == Guid.Empty ? Guid.NewGuid() : couponId)
        {
            Name = name,
            Type = type,
            FaceValue = faceValue,
            MinSpend = minSpend,
            ValidityType = validityType,
            ValidFrom = validFrom,
            ValidTo = validTo,
            ValidDays = validDays,
            TotalQty = totalQty,
            IssuedQty = 0,
            Status = CouponTemplateStatus.Enabled
        };
    }

    /// <summary>
    /// 更新券模板可编辑字段（不含发放量与已发放量）。
    /// </summary>
    public void Update(
        string name,
        CouponType type,
        decimal faceValue,
        decimal minSpend,
        CouponValidityType validityType,
        DateTime? validFrom,
        DateTime? validTo,
        int? validDays)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new PromotionDomainException("券名称不可为空", "COUPON_NAME_EMPTY");
        }

        ValidateFaceValue(type, faceValue);

        if (minSpend < 0)
        {
            throw new PromotionDomainException("使用门槛不可为负", "COUPON_MIN_SPEND_INVALID");
        }

        ValidateValidity(validityType, validFrom, validTo, validDays);

        Name = name;
        Type = type;
        FaceValue = faceValue;
        MinSpend = minSpend;
        ValidityType = validityType;
        ValidFrom = validFrom;
        ValidTo = validTo;
        ValidDays = validDays;
    }

    /// <summary>启用券模板。</summary>
    public void Enable()
    {
        if (Status == CouponTemplateStatus.Enabled)
        {
            throw new PromotionDomainException("券模板已启用", "COUPON_ALREADY_ENABLED");
        }

        Status = CouponTemplateStatus.Enabled;
    }

    /// <summary>停用券模板，停用后不可领取但已领取的仍可核销。</summary>
    public void Disable()
    {
        if (Status == CouponTemplateStatus.Disabled)
        {
            throw new PromotionDomainException("券模板已停用", "COUPON_ALREADY_DISABLED");
        }

        Status = CouponTemplateStatus.Disabled;
    }

    /// <summary>
    /// 发放指定数量的券，校验模板启用且有剩余量。
    /// </summary>
    /// <param name="quantity">发放数量，须 &gt; 0。</param>
    public void Issue(int quantity)
    {
        if (quantity <= 0)
        {
            throw new PromotionDomainException("发放数量须大于 0", "COUPON_ISSUE_QTY_INVALID");
        }

        if (Status != CouponTemplateStatus.Enabled)
        {
            throw new PromotionDomainException("券模板已停用，不可发放", "COUPON_DISABLED");
        }

        if (TotalQty > 0 && IssuedQty + quantity > TotalQty)
        {
            throw new PromotionDomainException(
                $"券发放量超出剩余量：已发放 {IssuedQty}，本次 {quantity}，总量 {TotalQty}",
                "COUPON_QTY_EXCEED");
        }

        IssuedQty += quantity;
    }

    private static void ValidateFaceValue(CouponType type, decimal faceValue)
    {
        if (faceValue <= 0)
        {
            throw new PromotionDomainException("面值须大于 0", "COUPON_FACE_VALUE_INVALID");
        }

        if (type == CouponType.Percentage && faceValue > 100)
        {
            throw new PromotionDomainException("折扣率不可超过 100", "COUPON_PERCENTAGE_INVALID");
        }
    }

    private static void ValidateValidity(
        CouponValidityType validityType,
        DateTime? validFrom,
        DateTime? validTo,
        int? validDays)
    {
        if (validityType == CouponValidityType.FixedPeriod)
        {
            if (!validFrom.HasValue || !validTo.HasValue)
            {
                throw new PromotionDomainException("固定时段有效期须填写起止时间", "COUPON_FIXED_PERIOD_INVALID");
            }

            if (validTo.Value <= validFrom.Value)
            {
                throw new PromotionDomainException("有效期截止须晚于起始", "COUPON_VALIDITY_TIME_INVALID");
            }
        }
        else
        {
            if (!validDays.HasValue || validDays.Value <= 0)
            {
                throw new PromotionDomainException("相对天数有效期须填写大于 0 的天数", "COUPON_VALID_DAYS_INVALID");
            }
        }
    }

    /// <summary>
    /// 计算单张用户券领取时的过期时间（UTC）。
    /// FixedPeriod 取模板 ValidTo；RelativeDays 取领取时间 + ValidDays。
    /// </summary>
    /// <param name="receivedAt">领取时间（UTC）。</param>
    public DateTime ComputeExpiredAt(DateTime receivedAt)
    {
        return ValidityType == CouponValidityType.FixedPeriod
            ? ValidTo!.Value
            : receivedAt.AddDays(ValidDays!.Value);
    }

    /// <summary>判断券模板当前是否可被领取（启用且未过期）。</summary>
    public bool IsReceivable(DateTime now)
    {
        if (Status != CouponTemplateStatus.Enabled)
        {
            return false;
        }

        if (TotalQty > 0 && IssuedQty >= TotalQty)
        {
            return false;
        }

        if (ValidityType == CouponValidityType.FixedPeriod && ValidTo.HasValue && now >= ValidTo.Value)
        {
            return false;
        }

        return true;
    }
}
