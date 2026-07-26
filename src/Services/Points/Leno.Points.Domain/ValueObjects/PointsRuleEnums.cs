namespace Leno.Points.Domain.ValueObjects;

/// <summary>
/// 积分规则状态枚举，控制规则是否参与积分发放。
/// 流转：Enabled ↔ Disabled（运营启停双向切换）。
/// </summary>
public enum PointsRuleStatus
{
    /// <summary>启用：规则生效，参与积分发放。</summary>
    Enabled = 0,

    /// <summary>停用：规则不参与积分发放。</summary>
    Disabled = 1
}

/// <summary>
/// 积分规则行为类型枚举，标识规则对应的用户行为类别。
/// 与 <see cref="PointsSource"/> 区别：ActionType 用于规则配置分类，PointsSource 用于流水来源审计。
/// </summary>
public enum PointsActionType
{
    /// <summary>每日签到。</summary>
    CheckIn = 0,

    /// <summary>消费下单。</summary>
    Order = 1,

    /// <summary>评价商品。</summary>
    Review = 2,

    /// <summary>分享商品。</summary>
    Share = 3,

    /// <summary>首单完成。</summary>
    FirstOrder = 4,

    /// <summary>浏览商品。</summary>
    Browse = 5,

    /// <summary>邀请好友。</summary>
    Invite = 6,

    /// <summary>完善资料。</summary>
    Profile = 7,

    /// <summary>活动奖励（运营手动发放等）。</summary>
    Activity = 8
}
