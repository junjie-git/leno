namespace Leno.Promotion.Domain.ValueObjects;

/// <summary>
/// 秒杀活动状态枚举。
/// 流转：Pending → Active；Active → Ended（库存售罄自动）；Active/Ended → Closed（运营手动）。
/// </summary>
public enum SeckillStatus
{
    /// <summary>待生效：运营创建后初始态，未激活。</summary>
    Pending = 0,

    /// <summary>进行中：已激活且在有效时间区间内。</summary>
    Active = 1,

    /// <summary>已结束：库存售罄或活动到期自动结束。</summary>
    Ended = 2,

    /// <summary>已关闭：运营手动关闭，终态。</summary>
    Closed = 3
}
