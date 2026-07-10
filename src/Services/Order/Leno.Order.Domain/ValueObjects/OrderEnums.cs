namespace Leno.Order.Domain.ValueObjects;

/// <summary>
/// 订单状态枚举。
/// 流转：PendingPayment → Paid → Shipped → Completed → Closed；
/// PendingPayment → Cancelled（待支付取消）；Paid/Shipped → Cancelled（强制取消异常单）。
/// </summary>
public enum OrderStatus
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

/// <summary>
/// 订单类型枚举。
/// Normal 为普通商品订单；Membership 为会员套餐订单（支付即完成，无售后窗口）；Seckill 为秒杀订单。
/// </summary>
public enum OrderType
{
    /// <summary>普通订单。</summary>
    Normal = 0,

    /// <summary>会员套餐订单。</summary>
    Membership = 1,

    /// <summary>秒杀订单。</summary>
    Seckill = 2
}

/// <summary>
/// 支付方式枚举。
/// </summary>
public enum PaymentMethod
{
    /// <summary>微信支付。</summary>
    WeChatPay = 0,

    /// <summary>支付宝。</summary>
    Alipay = 1,

    /// <summary>余额支付。</summary>
    Balance = 2
}

/// <summary>
/// 物流公司状态枚举，控制物流公司是否可用。
/// </summary>
public enum LogisticsCompanyStatus
{
    /// <summary>启用。</summary>
    Enabled = 0,

    /// <summary>停用。</summary>
    Disabled = 1
}

/// <summary>
/// 运费模板计价类型枚举。
/// ByWeight 按重量计价（数量视为重量）；ByPiece 按件计价。
/// </summary>
public enum FreightTemplateType
{
    /// <summary>按重量计价。</summary>
    ByWeight = 0,

    /// <summary>按件计价。</summary>
    ByPiece = 1
}

/// <summary>
/// 运费模板状态枚举，控制模板是否可被下单时引用。
/// </summary>
public enum FreightTemplateStatus
{
    /// <summary>启用。</summary>
    Enabled = 0,

    /// <summary>停用。</summary>
    Disabled = 1
}
