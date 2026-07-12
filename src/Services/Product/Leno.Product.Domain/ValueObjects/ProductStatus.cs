namespace Leno.Product.Domain.ValueObjects;

/// <summary>
/// 商品 SPU 状态枚举。
/// 状态流转：Draft → PendingReview → OnSale → TakenDown；TakenDown → PendingReview（重新上架）；PendingReview → Rejected（驳回）。
/// </summary>
public enum ProductStatus
{
    /// <summary>草稿：卖家创建后的初始态，不对外可见。</summary>
    Draft = 0,

    /// <summary>待审核：卖家提交后等待运营审核。</summary>
    PendingReview = 1,

    /// <summary>已上架：运营审核通过后对外可售。</summary>
    OnSale = 2,

    /// <summary>已下架：卖家主动下架，买家侧不可见、不可售。</summary>
    TakenDown = 3,

    /// <summary>已驳回：运营审核驳回，终态，卖家需重新创建。</summary>
    Rejected = 4,

    /// <summary>店铺暂停：店铺事件驱动下架，买家侧不可见、不可售。</summary>
    ShopSuspended = 5
}

/// <summary>
/// SKU 状态枚举。
/// </summary>
public enum SkuStatus
{
    /// <summary>启用：可售。</summary>
    Active = 0,

    /// <summary>停用：不可售。</summary>
    Inactive = 1
}

/// <summary>
/// 分类状态枚举。
/// </summary>
public enum CategoryStatus
{
    /// <summary>启用：分类可见，可挂载商品。</summary>
    Enabled = 0,

    /// <summary>停用：分类在买家侧不展示。</summary>
    Disabled = 1
}

/// <summary>
/// 品牌状态枚举。
/// </summary>
public enum BrandStatus
{
    /// <summary>启用：品牌可被卖家选用。</summary>
    Enabled = 0,

    /// <summary>停用：品牌不在卖家发布选项中出现，已挂载商品保留显示。</summary>
    Disabled = 1
}
