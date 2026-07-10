namespace Leno.SellerShop.Domain.ValueObjects;

/// <summary>
/// 店铺状态枚举。
/// 状态流转：PendingReview → Active → Suspended → Active；任意非关闭态 → Closed（终态）；PendingReview → Rejected。
/// </summary>
public enum ShopStatus
{
    /// <summary>待审核：卖家提交入驻申请后的初始态。</summary>
    PendingReview = 0,

    /// <summary>营业中：审核通过后可正常经营。</summary>
    Active = 1,

    /// <summary>暂停：运营暂停，商品不可售新单，既有订单正常履约。</summary>
    Suspended = 2,

    /// <summary>已驳回：入驻申请被运营驳回。</summary>
    Rejected = 3,

    /// <summary>已关闭：终态，店铺不可恢复经营，商品全部下架。</summary>
    Closed = 4
}

/// <summary>
/// 卖家档案状态枚举。
/// </summary>
public enum SellerStatus
{
    /// <summary>草稿：尚未提交审核。</summary>
    Draft = 0,

    /// <summary>待审核：已提交等待审核。</summary>
    PendingReview = 1,

    /// <summary>已通过：审核通过，卖家身份生效。</summary>
    Approved = 2,

    /// <summary>已驳回：审核驳回。</summary>
    Rejected = 3
}
