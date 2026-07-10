namespace Leno.Promotion.Domain.ValueObjects;

/// <summary>
/// 促销活动类型枚举。
/// </summary>
public enum PromotionType
{
    /// <summary>满减活动：满 N 减 M。</summary>
    FullReduction = 0,

    /// <summary>优惠券活动：发放优惠券核销抵扣。</summary>
    Coupon = 1,

    /// <summary>秒杀活动：限时限量特价。</summary>
    Seckill = 2
}

/// <summary>
/// 促销活动状态枚举。
/// 状态流转：Pending → Active；Active → Paused；Paused → Active；Active/Paused → Closed。
/// </summary>
public enum PromotionStatus
{
    /// <summary>待生效：运营创建后初始态，未到开始时间或未激活。</summary>
    Pending = 0,

    /// <summary>进行中：已激活且在有效时间区间内。</summary>
    Active = 1,

    /// <summary>已暂停：运营手动暂停，买家侧不可见不可用。</summary>
    Paused = 2,

    /// <summary>已关闭：运营手动关闭或活动到期，终态。</summary>
    Closed = 3
}
