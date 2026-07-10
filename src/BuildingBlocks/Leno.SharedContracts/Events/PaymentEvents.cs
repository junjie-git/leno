using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;

namespace Leno.SharedContracts.Events;

/// <summary>
/// 支付成功集成事件，支付域在第三方支付成功回调后发布。
/// 消费方：订单域（标记已支付）、促销域（核销优惠券）、积分与会员域（正式扣减冻结积分/开通会员）、
/// 卖家域（通知发货）、库存（确认真实扣减）。
/// 同时实现 <see cref="IDomainEvent"/> 以便支付域经发件箱模式在同一事务内持久化。
/// 事件契约定义在共享层，变更需所有消费方协商。
/// </summary>
public sealed class PaymentSucceededEvent : IntegrationEventBase, IDomainEvent
{
    /// <summary>订单标识。</summary>
    public Guid OrderId { get; init; }

    /// <summary>支付单标识。</summary>
    public Guid PaymentId { get; init; }

    /// <summary>买家账号标识。</summary>
    public Guid UserId { get; init; }

    /// <summary>支付渠道。</summary>
    public string Channel { get; init; } = string.Empty;

    /// <summary>第三方交易号。</summary>
    public string TradeNo { get; init; } = string.Empty;

    /// <summary>实付金额。</summary>
    public decimal Amount { get; init; }

    /// <summary>币种（ISO 4217），默认 CNY。</summary>
    public string Currency { get; init; } = "CNY";

    /// <summary>支付时间（UTC）。</summary>
    public DateTime PaidAt { get; init; }

    /// <summary>聚合根标识，用于发件箱归类。</summary>
    public Guid AggregateId => PaymentId;

    /// <summary>供 System.Text.Json 反序列化使用的无参构造。</summary>
    public PaymentSucceededEvent() : base()
    {
    }

    public PaymentSucceededEvent(
        Guid orderId,
        Guid paymentId,
        Guid userId,
        string channel,
        string tradeNo,
        decimal amount,
        string currency,
        DateTime paidAt) : base()
    {
        OrderId = orderId;
        PaymentId = paymentId;
        UserId = userId;
        Channel = channel ?? string.Empty;
        TradeNo = tradeNo ?? string.Empty;
        Amount = amount;
        Currency = string.IsNullOrWhiteSpace(currency) ? "CNY" : currency;
        PaidAt = paidAt;
    }
}

/// <summary>
/// 支付失败集成事件，支付域在支付失败时发布。
/// 消费方：订单域（记录失败原因，订单保持待支付可重试）、消息通知域（支付失败通知）。
/// 同时实现 <see cref="IDomainEvent"/> 以便支付域经发件箱模式在同一事务内持久化。
/// 事件契约定义在共享层，变更需所有消费方协商。
/// </summary>
public sealed class PaymentFailedEvent : IntegrationEventBase, IDomainEvent
{
    /// <summary>订单标识。</summary>
    public Guid OrderId { get; init; }

    /// <summary>失败原因。</summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>失败时间（UTC）。</summary>
    public DateTime FailedAt { get; init; }

    /// <summary>聚合根标识，用于发件箱归类。</summary>
    public Guid AggregateId => OrderId;

    /// <summary>供 System.Text.Json 反序列化使用的无参构造。</summary>
    public PaymentFailedEvent() : base()
    {
    }

    public PaymentFailedEvent(Guid orderId, string reason, DateTime failedAt) : base()
    {
        OrderId = orderId;
        Reason = reason ?? string.Empty;
        FailedAt = failedAt;
    }
}

/// <summary>
/// 退款完成集成事件，支付域在退款到账后发布。
/// 消费方：订单域（更新订单退款状态）、卖家域（扣减结算货款）、消息通知域（退款到账通知）。
/// 同时实现 <see cref="IDomainEvent"/> 以便支付域经发件箱模式在同一事务内持久化。
/// 事件契约定义在共享层，变更需所有消费方协商。
/// </summary>
public sealed class RefundCompletedEvent : IntegrationEventBase, IDomainEvent
{
    /// <summary>订单标识。</summary>
    public Guid OrderId { get; init; }

    /// <summary>退款单标识。</summary>
    public Guid RefundId { get; init; }

    /// <summary>退款金额。</summary>
    public decimal RefundAmount { get; init; }

    /// <summary>币种（ISO 4217），默认 CNY。</summary>
    public string Currency { get; init; } = "CNY";

    /// <summary>退款完成时间（UTC）。</summary>
    public DateTime CompletedAt { get; init; }

    /// <summary>关联售后单标识，默认 Guid.Empty（兼容旧版消费方）。</summary>
    public Guid AfterSalesId { get; init; }

    /// <summary>聚合根标识，用于发件箱归类。</summary>
    public Guid AggregateId => RefundId;

    /// <summary>供 System.Text.Json 反序列化使用的无参构造。</summary>
    public RefundCompletedEvent() : base()
    {
    }

    public RefundCompletedEvent(Guid orderId, Guid refundId, decimal refundAmount, string currency, DateTime completedAt)
        : base()
    {
        OrderId = orderId;
        RefundId = refundId;
        RefundAmount = refundAmount;
        Currency = string.IsNullOrWhiteSpace(currency) ? "CNY" : currency;
        CompletedAt = completedAt;
    }

    /// <summary>
    /// 带售后单标识的构造重载，由支付域退款成功时发布，便于售后域关联退款单。
    /// </summary>
    public RefundCompletedEvent(Guid orderId, Guid refundId, Guid afterSalesId, decimal refundAmount, string currency, DateTime completedAt)
        : base()
    {
        OrderId = orderId;
        RefundId = refundId;
        AfterSalesId = afterSalesId;
        RefundAmount = refundAmount;
        Currency = string.IsNullOrWhiteSpace(currency) ? "CNY" : currency;
        CompletedAt = completedAt;
    }
}

/// <summary>
/// 支付单关闭集成事件，支付域在关闭支付单（超时/主动关闭）时发布。
/// 消费方：订单域（取消订单释放预占资源）。
/// 同时实现 <see cref="IDomainEvent"/> 以便支付域经发件箱模式在同一事务内持久化。
/// 事件契约定义在共享层，变更需所有消费方协商。
/// </summary>
public sealed class PaymentClosedEvent : IntegrationEventBase, IDomainEvent
{
    /// <summary>支付单标识。</summary>
    public Guid PaymentId { get; init; }

    /// <summary>订单标识。</summary>
    public Guid OrderId { get; init; }

    /// <summary>关闭原因。</summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>关闭时间（UTC）。</summary>
    public DateTime ClosedAt { get; init; }

    /// <summary>聚合根标识，用于发件箱归类。</summary>
    public Guid AggregateId => PaymentId;

    /// <summary>供 System.Text.Json 反序列化使用的无参构造。</summary>
    public PaymentClosedEvent() : base()
    {
    }

    public PaymentClosedEvent(Guid paymentId, Guid orderId, string reason, DateTime closedAt) : base()
    {
        PaymentId = paymentId;
        OrderId = orderId;
        Reason = reason ?? string.Empty;
        ClosedAt = closedAt;
    }
}

/// <summary>
/// 退款失败集成事件，支付域在退款失败时发布。
/// 消费方：售后域（更新售后单退款状态，可重试）、消息通知域（退款失败通知）。
/// 同时实现 <see cref="IDomainEvent"/> 以便支付域经发件箱模式在同一事务内持久化。
/// 事件契约定义在共享层，变更需所有消费方协商。
/// </summary>
public sealed class RefundFailedEvent : IntegrationEventBase, IDomainEvent
{
    /// <summary>退款单标识。</summary>
    public Guid RefundId { get; init; }

    /// <summary>订单标识。</summary>
    public Guid OrderId { get; init; }

    /// <summary>关联售后单标识。</summary>
    public Guid AfterSalesId { get; init; }

    /// <summary>失败原因。</summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>失败时间（UTC）。</summary>
    public DateTime FailedAt { get; init; }

    /// <summary>聚合根标识，用于发件箱归类。</summary>
    public Guid AggregateId => RefundId;

    /// <summary>供 System.Text.Json 反序列化使用的无参构造。</summary>
    public RefundFailedEvent() : base()
    {
    }

    public RefundFailedEvent(Guid refundId, Guid orderId, Guid afterSalesId, string reason, DateTime failedAt) : base()
    {
        RefundId = refundId;
        OrderId = orderId;
        AfterSalesId = afterSalesId;
        Reason = reason ?? string.Empty;
        FailedAt = failedAt;
    }
}
