namespace Leno.ReviewAfterSales.Domain.ValueObjects;

/// <summary>
/// 评价审核状态枚举。
/// 流转：Pending → Approved；Approved → Hidden（运营隐藏违规评价）。
/// Hidden 为终态不可逆，买家侧不可见但聚合记录保留供审计。
/// </summary>
public enum ReviewStatus
{
    /// <summary>待审核。</summary>
    Pending = 0,

    /// <summary>已通过（对外可见，可追评与卖家回复）。</summary>
    Approved = 1,

    /// <summary>已隐藏（运营隐藏违规评价，买家侧不可见，记录保留供审计）。</summary>
    Hidden = 2
}
