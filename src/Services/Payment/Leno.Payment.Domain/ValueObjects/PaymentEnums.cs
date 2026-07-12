namespace Leno.Payment.Domain.ValueObjects;

/// <summary>
/// 支付渠道枚举。
/// </summary>
public enum PaymentChannel
{
    /// <summary>微信支付。</summary>
    WeChatPay = 0,

    /// <summary>支付宝。</summary>
    Alipay = 1
}

/// <summary>
/// 支付单状态枚举。
/// 流转：Pending → ChannelOrdered → Paid；Pending/ChannelOrdered → Failed；Pending/ChannelOrdered/Failed → Closed。
/// </summary>
public enum PaymentStatus
{
    /// <summary>待渠道下单（已创建支付单，尚未请求渠道）。</summary>
    Pending = 0,

    /// <summary>渠道已下单（已请求渠道拿到预支付参数）。</summary>
    ChannelOrdered = 1,

    /// <summary>已支付。</summary>
    Paid = 2,

    /// <summary>支付失败。</summary>
    Failed = 3,

    /// <summary>已关闭。</summary>
    Closed = 4
}

/// <summary>
/// 退款单状态枚举。
/// 流转：Refunding → Succeeded；Refunding → Failed。
/// </summary>
public enum RefundStatus
{
    /// <summary>退款中（已发起退款请求）。</summary>
    Refunding = 0,

    /// <summary>退款成功。</summary>
    Succeeded = 1,

    /// <summary>退款失败。</summary>
    Failed = 2
}

/// <summary>
/// 对账差异类型枚举。
/// </summary>
public enum ReconciliationDiffType
{
    /// <summary>渠道有记录但系统无记录（长款）。</summary>
    ChannelOnly = 0,

    /// <summary>系统有记录但渠道无记录（短款）。</summary>
    SystemOnly = 1,

    /// <summary>金额不一致。</summary>
    AmountMismatch = 2
}

/// <summary>
/// 对账差异状态枚举。
/// </summary>
public enum ReconciliationDiffStatus
{
    /// <summary>待处理。</summary>
    Pending = 0,

    /// <summary>已修复。</summary>
    Resolved = 1,

    /// <summary>已忽略。</summary>
    Ignored = 2
}
