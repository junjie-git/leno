namespace Leno.Promotion.Domain.ValueObjects;

/// <summary>
/// 优惠券类型枚举。
/// </summary>
public enum CouponType
{
    /// <summary>固定金额券：核销抵扣固定金额。</summary>
    FixedAmount = 0,

    /// <summary>折扣券：按面值百分比折扣（FaceValue 为 0-100 的折扣率）。</summary>
    Percentage = 1,

    /// <summary>满减券：满 MinSpend 减 FaceValue。</summary>
    FullReduction = 2
}

/// <summary>
/// 优惠券有效期类型枚举。
/// </summary>
public enum CouponValidityType
{
    /// <summary>固定时段：以 ValidFrom ~ ValidTo 为有效期。</summary>
    FixedPeriod = 0,

    /// <summary>相对天数：自领取之日起 ValidDays 天内有效。</summary>
    RelativeDays = 1
}

/// <summary>
/// 优惠券模板状态枚举（运营启停）。
/// </summary>
public enum CouponTemplateStatus
{
    /// <summary>启用：可被领取与核销。</summary>
    Enabled = 0,

    /// <summary>停用：不可领取，已领取的仍可核销。</summary>
    Disabled = 1
}

/// <summary>
/// 用户优惠券状态枚举，描述用户领取后的券生命周期。
/// 流转：Unused → Locked（下单锁定）→ Used（支付核销）；Locked → Unused（取消释放）；Unused/Locked → Expired（过期）。
/// </summary>
public enum CouponStatus
{
    /// <summary>未使用：可被下单锁定或已过期标记。</summary>
    Unused = 0,

    /// <summary>已锁定：下单时锁定，待支付期间不可他用。</summary>
    Locked = 1,

    /// <summary>已使用：支付成功核销，终态。</summary>
    Used = 2,

    /// <summary>已过期：超过有效期，终态。</summary>
    Expired = 3
}
