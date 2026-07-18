using Leno.SharedKernel.Abstractions;

namespace Leno.Payment.Domain.Events;

/// <summary>
/// 支付成功领域事件，由 <see cref="Aggregates.PaymentOrder"/> 聚合在 MarkSucceeded 方法中收集。
/// mapper 翻译为 <see cref="Leno.SharedContracts.Events.PaymentSucceededEvent"/> 集成事件对外发布。
/// 消费方：订单域（标记已支付）、促销域（核销优惠券）、积分与会员域（正式扣减冻结积分/开通会员）、
/// 卖家域（通知发货）、库存（确认真实扣减）。
/// </summary>
public sealed class PaymentSucceededDomainEvent : DomainEventBase
{
    public Guid OrderId { get; init; }
    public Guid PaymentId { get; init; }
    public Guid UserId { get; init; }
    public string Channel { get; init; } = string.Empty;
    public string TradeNo { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string Currency { get; init; } = "CNY";
    public DateTime PaidAt { get; init; }

    public PaymentSucceededDomainEvent(
        Guid orderId, Guid paymentId, Guid userId, string channel,
        string tradeNo, decimal amount, string currency, DateTime paidAt)
        : base(paymentId)
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
/// 支付失败领域事件，由 <see cref="Aggregates.PaymentOrder"/> 聚合在 MarkFailed 方法中收集。
/// mapper 翻译为 <see cref="Leno.SharedContracts.Events.PaymentFailedEvent"/> 集成事件对外发布。
/// 消费方：订单域（记录失败原因，订单保持待支付可重试）、消息通知域（支付失败通知）。
/// </summary>
public sealed class PaymentFailedDomainEvent : DomainEventBase
{
    public Guid OrderId { get; init; }
    public Guid UserId { get; init; }
    public string Reason { get; init; } = string.Empty;
    public DateTime FailedAt { get; init; }

    public PaymentFailedDomainEvent(Guid orderId, Guid userId, string reason, DateTime failedAt)
        : base(orderId)
    {
        OrderId = orderId;
        UserId = userId;
        Reason = reason ?? string.Empty;
        FailedAt = failedAt;
    }
}

/// <summary>
/// 支付单关闭领域事件，由 <see cref="Aggregates.PaymentOrder"/> 聚合在 MarkClosed 方法中收集。
/// mapper 翻译为 <see cref="Leno.SharedContracts.Events.PaymentClosedEvent"/> 集成事件对外发布。
/// 消费方：订单域（取消订单释放预占资源）。
/// </summary>
public sealed class PaymentClosedDomainEvent : DomainEventBase
{
    public Guid PaymentId { get; init; }
    public Guid OrderId { get; init; }
    public string Reason { get; init; } = string.Empty;
    public DateTime ClosedAt { get; init; }

    public PaymentClosedDomainEvent(Guid paymentId, Guid orderId, string reason, DateTime closedAt)
        : base(paymentId)
    {
        PaymentId = paymentId;
        OrderId = orderId;
        Reason = reason ?? string.Empty;
        ClosedAt = closedAt;
    }
}

/// <summary>
/// 退款完成领域事件，由 <see cref="Aggregates.RefundOrder"/> 聚合在 MarkSucceeded 方法中收集。
/// mapper 翻译为 <see cref="Leno.SharedContracts.Events.RefundCompletedEvent"/> 集成事件对外发布。
/// 消费方：订单域（更新订单退款状态）、卖家域（扣减结算货款）、消息通知域（退款到账通知）。
/// </summary>
public sealed class RefundCompletedDomainEvent : DomainEventBase
{
    public Guid OrderId { get; init; }
    public Guid UserId { get; init; }
    public Guid RefundId { get; init; }
    public Guid AfterSalesId { get; init; }
    public decimal RefundAmount { get; init; }
    public string Currency { get; init; } = "CNY";
    public DateTime CompletedAt { get; init; }

    public RefundCompletedDomainEvent(
        Guid orderId, Guid userId, Guid refundId, Guid afterSalesId,
        decimal refundAmount, string currency, DateTime completedAt)
        : base(refundId)
    {
        OrderId = orderId;
        UserId = userId;
        RefundId = refundId;
        AfterSalesId = afterSalesId;
        RefundAmount = refundAmount;
        Currency = string.IsNullOrWhiteSpace(currency) ? "CNY" : currency;
        CompletedAt = completedAt;
    }
}

/// <summary>
/// 退款失败领域事件，由 <see cref="Aggregates.RefundOrder"/> 聚合在 MarkFailed 方法中收集。
/// mapper 翻译为 <see cref="Leno.SharedContracts.Events.RefundFailedEvent"/> 集成事件对外发布。
/// 消费方：售后域（更新售后单退款状态，可重试）、消息通知域（退款失败通知）。
/// </summary>
public sealed class RefundFailedDomainEvent : DomainEventBase
{
    public Guid RefundId { get; init; }
    public Guid OrderId { get; init; }
    public Guid AfterSalesId { get; init; }
    public string Reason { get; init; } = string.Empty;
    public DateTime FailedAt { get; init; }

    public RefundFailedDomainEvent(Guid refundId, Guid orderId, Guid afterSalesId, string reason, DateTime failedAt)
        : base(refundId)
    {
        RefundId = refundId;
        OrderId = orderId;
        AfterSalesId = afterSalesId;
        Reason = reason ?? string.Empty;
        FailedAt = failedAt;
    }
}

/// <summary>
/// 支付渠道配置变更领域事件，由 <see cref="Aggregates.PaymentChannelConfig"/> 聚合在 Enable/Disable/UpdateConfigValue 方法中收集。
/// mapper 翻译为 <see cref="Leno.SharedContracts.Events.PaymentChannelConfigChangedEvent"/> 集成事件对外发布。
/// 消费方：渠道适配器（刷新缓存配置）、运维监控（配置变更通知）。
/// </summary>
public sealed class PaymentChannelConfigChangedDomainEvent : DomainEventBase
{
    public Guid ConfigId { get; init; }
    public string Channel { get; init; } = string.Empty;
    public string ConfigName { get; init; } = string.Empty;
    public string ChangeType { get; init; } = string.Empty;

    public PaymentChannelConfigChangedDomainEvent(Guid configId, string channel, string configName, string changeType)
        : base(configId)
    {
        ConfigId = configId;
        Channel = channel ?? string.Empty;
        ConfigName = configName ?? string.Empty;
        ChangeType = changeType ?? string.Empty;
    }
}
