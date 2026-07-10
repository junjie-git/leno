using Leno.Promotion.Domain.ValueObjects;

namespace Leno.Promotion.Domain.Services;

/// <summary>
/// 促销防腐层接口，供订单域等下游上下文查询当前可用的满减活动优惠。
/// 实现位于基础设施层（只读查询促销活动表），订单域不直接依赖促销域领域模型。
/// </summary>
public interface IPromotionQueryService
{
    /// <summary>
    /// 查询当前对指定订单金额适用的满减活动优惠快照。
    /// 仅返回 Active 且在有效时间区间内、订单金额命中规则的活动。
    /// </summary>
    /// <param name="orderAmount">订单金额。</param>
    /// <param name="sellerId">卖家标识（可选，用于按卖家过滤活动，null 表示不限卖家）。</param>
    /// <returns>适用活动优惠快照集合。</returns>
    Task<IReadOnlyList<ApplicablePromotionSnapshot>> GetApplicablePromotionsAsync(
        decimal orderAmount,
        Guid? sellerId,
        CancellationToken ct = default);
}

/// <summary>
/// 适用促销活动优惠快照，由防腐层返回，供订单域试算优惠。
/// </summary>
public sealed class ApplicablePromotionSnapshot
{
    /// <summary>活动标识。</summary>
    public Guid ActivityId { get; init; }

    /// <summary>活动名称。</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>活动类型。</summary>
    public PromotionType Type { get; init; }

    /// <summary>命中规则的减免金额。</summary>
    public decimal DiscountAmount { get; init; }

    /// <summary>命中规则的门槛金额。</summary>
    public decimal ThresholdAmount { get; init; }

    /// <summary>活动结束时间（UTC）。</summary>
    public DateTime EndTime { get; init; }
}
