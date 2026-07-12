namespace Leno.PointsMembership.Domain.ValueObjects;

/// <summary>
/// 积分来源枚举，标识积分获取的渠道。
/// </summary>
public enum PointsSource
{
    /// <summary>签到获取。</summary>
    CheckIn = 0,

    /// <summary>消费返积分。</summary>
    Consumption = 1,

    /// <summary>活动奖励。</summary>
    Activity = 2,

    /// <summary>退款回补。</summary>
    Refund = 3,

    /// <summary>积分抵扣（订单使用积分）。</summary>
    Offset = 4,

    /// <summary>评价返积分。</summary>
    Review = 5,

    /// <summary>新人注册积分。</summary>
    NewUser = 6,

    /// <summary>积分兑换优惠券。</summary>
    CouponExchange = 7
}

/// <summary>
/// 积分流水交易类型枚举，区分积分账户的资金流向。
/// </summary>
public enum PointsTxType
{
    /// <summary>获取积分。</summary>
    Earn = 0,

    /// <summary>冻结积分（下单预占）。</summary>
    Freeze = 1,

    /// <summary>确认扣减（支付成功核销冻结）。</summary>
    ConfirmDeduct = 2,

    /// <summary>释放冻结（订单取消回退）。</summary>
    Release = 3,

    /// <summary>退款回补积分。</summary>
    Refund = 4,

    /// <summary>直接消费积分（扣减）。</summary>
    Consume = 5,

    /// <summary>积分扣回（退款扣回已发放积分）。</summary>
    Revert = 6,

    /// <summary>积分兑换优惠券。</summary>
    CouponExchange = 7
}
