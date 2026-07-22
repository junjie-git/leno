namespace Leno.SharedContracts.Enums;

/// <summary>
/// 售后类型共享枚举（跨 BC 契约）。
/// 值与售后域 <c>Leno.ReviewAfterSales.Domain.ValueObjects.AfterSalesType</c> 严格对齐，
/// 任何一方调整枚举值须双方协商并同步更新。
/// ReturnRefund 为退货退款（买家寄回商品后退款）；RefundOnly 为仅退款（不退货）。
/// </summary>
public enum AfterSalesTypeEnum
{
    /// <summary>退货退款。</summary>
    ReturnRefund = 0,

    /// <summary>仅退款。</summary>
    RefundOnly = 1
}
