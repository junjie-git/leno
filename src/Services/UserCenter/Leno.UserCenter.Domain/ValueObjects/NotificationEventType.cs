namespace Leno.UserCenter.Domain.ValueObjects;

/// <summary>
/// 通知事件类型枚举，对应通知偏好配置的事件分组。
/// 业务侧按事件类型与渠道组合控制通知触达。
/// 从 UserAuth BC 迁入 UserCenter BC（Task A6）。
/// </summary>
public enum NotificationEventType
{
    /// <summary>订单状态变更（订单通知分组）。</summary>
    OrderStatus = 1,

    /// <summary>物流更新（订单通知分组）。</summary>
    LogisticsUpdate = 2,

    /// <summary>优惠券到账（促销通知分组）。</summary>
    CouponArrival = 3,

    /// <summary>秒杀提醒（促销通知分组）。</summary>
    SeckillReminder = 4,

    /// <summary>积分到账（积分通知分组）。</summary>
    PointsEarned = 5,

    /// <summary>积分过期提醒（积分通知分组）。</summary>
    PointsExpiring = 6,

    /// <summary>系统通知（系统通知分组）。</summary>
    SystemNotice = 7
}
