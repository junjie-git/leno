namespace Leno.SharedContracts.Integration.Dto;

/// <summary>
/// 优惠计算结果共享 DTO（D2.6 ACL 模式去重）。
/// 各 BC 的 PromotionAntiCorruptionService.CalculateDiscountAsync 统一返回此类型，
/// 消除 Order / Cart 2 BC 重复定义，并提供按 SKU 分摊明细（旧 Order 实现仅返回 decimal 总额）。
/// </summary>
public sealed class DiscountCalculationResultDto
{
    /// <summary>买家标识。</summary>
    public Guid UserId { get; init; }

    /// <summary>优惠总金额。</summary>
    public decimal TotalDiscountAmount { get; init; }

    /// <summary>币种（ISO 4217，如 "CNY"）。</summary>
    public string Currency { get; init; } = "CNY";

    /// <summary>按 SKU 的优惠分摊明细列表，用于订单行记录分摊金额。</summary>
    public List<DiscountAllocationDto> Allocations { get; init; } = [];

    /// <summary>计算时间（UTC）。</summary>
    public DateTime CalculatedAt { get; init; }

    /// <summary>本次计算命中的优惠券标识列表（可为空）。</summary>
    public List<Guid> AppliedCouponIds { get; init; } = [];
}

/// <summary>
/// 优惠分摊明细共享 DTO。
/// </summary>
public sealed class DiscountAllocationDto
{
    /// <summary>SKU 标识。</summary>
    public Guid SkuId { get; init; }

    /// <summary>该 SKU 分摊到的优惠金额。</summary>
    public decimal Allocation { get; init; }
}

/// <summary>
/// 优惠券锁定结果共享 DTO（D2.6 ACL 模式去重）。
/// 下单时锁定选定优惠券返回此结果，包含锁定成功/失败及失败原因。
/// </summary>
public sealed class CouponLockResultDto
{
    /// <summary>买家标识。</summary>
    public Guid UserId { get; init; }

    /// <summary>优惠券模板标识。</summary>
    public Guid CouponId { get; init; }

    /// <summary>关联订单标识。</summary>
    public Guid OrderId { get; init; }

    /// <summary>用户优惠券记录标识（UserCouponId），锁定成功后填充。</summary>
    public Guid UserCouponId { get; init; }

    /// <summary>锁定时间（UTC）。</summary>
    public DateTime LockedAt { get; init; }

    /// <summary>操作是否成功。</summary>
    public bool Success { get; init; } = true;

    /// <summary>失败原因码（成功时为空字符串），如 "COUPON_ALREADY_USED" / "COUPON_EXPIRED"。</summary>
    public string FailureCode { get; init; } = string.Empty;

    /// <summary>失败原因描述（成功时为空字符串）。</summary>
    public string FailureMessage { get; init; } = string.Empty;
}
