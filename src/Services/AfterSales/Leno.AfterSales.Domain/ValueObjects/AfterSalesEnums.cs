namespace Leno.AfterSales.Domain.ValueObjects;

/// <summary>
/// 售后类型枚举。
/// ReturnRefund 为退货退款（买家寄回商品后退款）；RefundOnly 为仅退款（不退货）。
/// </summary>
public enum AfterSalesType
{
    /// <summary>退货退款。</summary>
    ReturnRefund = 0,

    /// <summary>仅退款。</summary>
    RefundOnly = 1
}

/// <summary>
/// 售后状态枚举。
/// 流转：
///   Pending → Approved/Rejected/Cancelled；
///   Approved → ReturnGoods（退货退款）→ ConfirmReturn → Refunding → Completed/Failed；
///   或 Approved → Refunding（仅退款）→ Completed/Failed。
/// Rejected、Completed、Failed、Cancelled 为终态。
/// </summary>
public enum AfterSalesStatus
{
    /// <summary>待审核。</summary>
    Pending = 0,

    /// <summary>已同意（待买家退货/待退款处理）。</summary>
    Approved = 1,

    /// <summary>已驳回（终态）。</summary>
    Rejected = 2,

    /// <summary>退款中。</summary>
    Refunding = 3,

    /// <summary>已完成（退款成功，终态）。</summary>
    Completed = 4,

    /// <summary>退款失败（终态）。</summary>
    Failed = 5,

    /// <summary>已撤销（终态）。</summary>
    Cancelled = 6,

    /// <summary>已退货（买家已寄回商品，待卖家确认收货）。</summary>
    ReturnGoods = 7,

    /// <summary>已确认收货（卖家已确认收到退货，待退款）。</summary>
    ConfirmReturn = 8
}
