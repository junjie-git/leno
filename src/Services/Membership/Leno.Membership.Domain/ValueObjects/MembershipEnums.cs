namespace Leno.Membership.Domain.ValueObjects;

/// <summary>
/// 会员状态枚举。
/// 流转：Active ↔ Frozen（运营冻结/解冻）。
/// </summary>
public enum MemberStatus
{
    /// <summary>正常。</summary>
    Active = 0,

    /// <summary>已冻结。</summary>
    Frozen = 1
}

/// <summary>
/// 会员权益状态枚举。
/// 流转：Pending → Active（支付成功激活）；Active → Expired（到期）；Pending → Cancelled（取消未支付）。
/// </summary>
public enum MemberBenefitStatus
{
    /// <summary>待生效：下单未支付。</summary>
    Pending = 0,

    /// <summary>生效中：支付成功且在有效期内。</summary>
    Active = 1,

    /// <summary>已过期：超过有效期。</summary>
    Expired = 2,

    /// <summary>已取消：未支付订单取消。</summary>
    Cancelled = 3
}

/// <summary>
/// 会员套餐状态枚举，控制套餐是否可被购买。
/// </summary>
public enum PackageStatus
{
    /// <summary>启用。</summary>
    Enabled = 0,

    /// <summary>停用。</summary>
    Disabled = 1
}

/// <summary>
/// 会员等级定义状态枚举，控制等级定义是否参与等级评估与展示。
/// 流转：Enabled ↔ Disabled（运营启停）。
/// </summary>
public enum LevelDefinitionStatus
{
    /// <summary>启用：参与等级评估与买家端展示。</summary>
    Enabled = 0,

    /// <summary>停用：不参与等级评估，已有会员等级不受影响。</summary>
    Disabled = 1
}
