namespace Leno.SellerShop.Domain.ValueObjects;

/// <summary>
/// 资质证照类型枚举。
/// </summary>
public enum QualificationType
{
    /// <summary>营业执照。</summary>
    BusinessLicense = 0,

    /// <summary>食品经营许可证。</summary>
    FoodLicense = 1,

    /// <summary>其他资质证照。</summary>
    Other = 2
}

/// <summary>
/// 资质证照审核状态枚举。
/// 状态流转：Pending → Approved / Rejected；Approved → Expired。
/// </summary>
public enum QualificationStatus
{
    /// <summary>待审核。</summary>
    Pending = 0,

    /// <summary>审核通过。</summary>
    Approved = 1,

    /// <summary>审核驳回。</summary>
    Rejected = 2,

    /// <summary>已过期。</summary>
    Expired = 3
}