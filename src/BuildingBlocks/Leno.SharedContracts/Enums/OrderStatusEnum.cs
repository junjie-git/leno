namespace Leno.SharedContracts.Enums;

/// <summary>
/// 订单状态共享枚举（跨 BC 契约）。
/// 值与订单域 <c>Leno.Order.Domain.ValueObjects.OrderStatus</c> 严格对齐，
/// 任何一方调整枚举值须双方协商并同步更新。
/// 流转：PendingPayment → Paid → Shipped → Completed → Closed；
/// PendingPayment → Cancelled（待支付取消）；Paid/Shipped → Cancelled（强制取消异常单）。
/// </summary>
public enum OrderStatusEnum
{
    /// <summary>待支付。</summary>
    PendingPayment = 0,

    /// <summary>已支付。</summary>
    Paid = 1,

    /// <summary>已发货。</summary>
    Shipped = 2,

    /// <summary>已完成（已确认收货）。</summary>
    Completed = 3,

    /// <summary>已取消。</summary>
    Cancelled = 4,

    /// <summary>已关闭（售后窗口结束）。</summary>
    Closed = 5
}
