namespace Leno.SharedContracts.Enums;

/// <summary>
/// 评价审核状态共享枚举（跨 BC 契约）。
/// 值与评价域 <c>Leno.ReviewAfterSales.Domain.ValueObjects.ReviewStatus</c> 严格对齐，
/// 任何一方调整枚举值须双方协商并同步更新。
/// 流转：Pending → Approved；Approved → Hidden（运营隐藏违规评价）。
/// Hidden 为终态不可逆，买家侧不可见但聚合记录保留供审计。
/// </summary>
public enum ReviewStatusEnum
{
    /// <summary>待审核。</summary>
    Pending = 0,

    /// <summary>已通过（对外可见，可追评与卖家回复）。</summary>
    Approved = 1,

    /// <summary>已隐藏（运营隐藏违规评价，买家侧不可见，记录保留供审计）。</summary>
    Hidden = 2
}
