using Leno.SharedKernel.Abstractions;

namespace Leno.Order.Domain.Events;

/// <summary>
/// 订单创建领域事件，由 <see cref="Aggregates.Order"/> 聚合在 Create 工厂方法中收集。
/// mapper 翻译为 <see cref="Leno.SharedContracts.Events.OrderCreatedEvent"/> 集成事件对外发布。
/// 消费方：购物车域（清空已结算项）、促销域（锁定优惠券）、积分与会员域（冻结积分）、消息通知域（下单成功通知）、MQ 延迟消息（30 分钟超时取消）。
/// </summary>
public sealed class OrderCreatedDomainEvent : DomainEventBase
{
    public Guid OrderId { get; init; }
    public Guid BuyerId { get; init; }
    public Guid SellerId { get; init; }
    public decimal TotalAmount { get; init; }
    public string Currency { get; init; } = "CNY";
    public DateTime CreatedAt { get; init; }
    public IReadOnlyList<Guid> SourceCartItemIds { get; init; } = Array.Empty<Guid>();

    public OrderCreatedDomainEvent(
        Guid orderId, Guid buyerId, Guid sellerId, decimal totalAmount,
        string currency, DateTime createdAt, IReadOnlyList<Guid> sourceCartItemIds)
        : base(orderId)
    {
        OrderId = orderId;
        BuyerId = buyerId;
        SellerId = sellerId;
        TotalAmount = totalAmount;
        Currency = string.IsNullOrWhiteSpace(currency) ? "CNY" : currency;
        CreatedAt = createdAt;
        SourceCartItemIds = sourceCartItemIds ?? Array.Empty<Guid>();
    }
}

/// <summary>
/// 订单支付成功领域事件，由 <see cref="Aggregates.Order"/> 聚合在 MarkAsPaid 方法中收集。
/// mapper 翻译为 <see cref="Leno.SharedContracts.Events.OrderPaidEvent"/> 集成事件对外发布。
/// 消费方：促销域（核销优惠券）、积分与会员域（正式扣减冻结抵现积分/开通会员）、卖家域（通知发货）、消息通知域（支付成功通知）、库存（确认真实扣减）。
/// </summary>
public sealed class OrderPaidDomainEvent : DomainEventBase
{
    public Guid OrderId { get; init; }
    public Guid UserId { get; init; }
    public Guid SellerId { get; init; }
    public Guid PaymentId { get; init; }
    public string Channel { get; init; } = string.Empty;
    public DateTime PaidAt { get; init; }
    public string TradeNo { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string Currency { get; init; } = "CNY";

    public OrderPaidDomainEvent(
        Guid orderId, Guid userId, Guid sellerId, Guid paymentId,
        string channel, DateTime paidAt, string tradeNo, decimal amount, string currency)
        : base(orderId)
    {
        OrderId = orderId;
        UserId = userId;
        SellerId = sellerId;
        PaymentId = paymentId;
        Channel = channel ?? string.Empty;
        PaidAt = paidAt;
        TradeNo = tradeNo ?? string.Empty;
        Amount = amount;
        Currency = string.IsNullOrWhiteSpace(currency) ? "CNY" : currency;
    }
}

/// <summary>
/// 订单发货领域事件，由 <see cref="Aggregates.Order"/> 聚合在 Ship 方法中收集。
/// mapper 翻译为 <see cref="Leno.SharedContracts.Events.OrderShippedEvent"/> 集成事件对外发布。
/// 消费方：消息通知域（发货通知）、卖家域（待发货数 -1）。
/// </summary>
public sealed class OrderShippedDomainEvent : DomainEventBase
{
    public Guid OrderId { get; init; }
    public Guid UserId { get; init; }
    public Guid SellerId { get; init; }
    public string LogisticsNo { get; init; } = string.Empty;
    public DateTime ShippedAt { get; init; }

    public OrderShippedDomainEvent(Guid orderId, Guid userId, Guid sellerId, string logisticsNo, DateTime shippedAt)
        : base(orderId)
    {
        OrderId = orderId;
        UserId = userId;
        SellerId = sellerId;
        LogisticsNo = logisticsNo ?? string.Empty;
        ShippedAt = shippedAt;
    }
}

/// <summary>
/// 订单完成领域事件，由 <see cref="Aggregates.Order"/> 聚合在 ConfirmReceipt/CompleteMembershipOrder 方法中收集。
/// mapper 翻译为 <see cref="Leno.SharedContracts.Events.OrderCompletedEvent"/> 集成事件对外发布。
/// 消费方：卖家域（维护店铺销量与销售额）。
/// </summary>
public sealed class OrderCompletedDomainEvent : DomainEventBase
{
    public Guid OrderId { get; init; }
    public Guid UserId { get; init; }
    public Guid SellerId { get; init; }
    public decimal TotalAmount { get; init; }
    public string Currency { get; init; } = "CNY";
    public DateTime CompletedAt { get; init; }

    public OrderCompletedDomainEvent(
        Guid orderId, Guid userId, Guid sellerId, decimal totalAmount, string currency, DateTime completedAt)
        : base(orderId)
    {
        OrderId = orderId;
        UserId = userId;
        SellerId = sellerId;
        TotalAmount = totalAmount;
        Currency = string.IsNullOrWhiteSpace(currency) ? "CNY" : currency;
        CompletedAt = completedAt;
    }
}

/// <summary>
/// 订单取消领域事件，由 <see cref="Aggregates.Order"/> 聚合在 Cancel/ForceCancel 方法中收集。
/// mapper 翻译为 <see cref="Leno.SharedContracts.Events.OrderCancelledEvent"/> 集成事件对外发布。
/// 消费方：促销域（退还锁定优惠券）、积分与会员域（释放冻结抵现积分）、库存（释放预占）、消息通知域（取消通知）。
/// </summary>
public sealed class OrderCancelledDomainEvent : DomainEventBase
{
    public Guid OrderId { get; init; }
    public Guid SellerId { get; init; }
    public string CancelReason { get; init; } = string.Empty;
    public DateTime CancelledAt { get; init; }
    public string CancelledBy { get; init; } = string.Empty;
    public int PointsToRelease { get; init; }

    public OrderCancelledDomainEvent(
        Guid orderId, Guid sellerId, string cancelReason, DateTime cancelledAt,
        string cancelledBy, int pointsToRelease)
        : base(orderId)
    {
        OrderId = orderId;
        SellerId = sellerId;
        CancelReason = cancelReason ?? string.Empty;
        CancelledAt = cancelledAt;
        CancelledBy = cancelledBy ?? string.Empty;
        PointsToRelease = pointsToRelease;
    }
}

/// <summary>
/// 订单售后窗口关闭领域事件，由 <see cref="Aggregates.Order"/> 聚合在 CloseAfterSalesWindow 方法中收集。
/// mapper 翻译为 <see cref="Leno.SharedContracts.Events.OrderAfterSalesWindowClosedEvent"/> 集成事件对外发布。
/// 消费方：积分与会员域（确认发放购物积分）、卖家域（结算货款）。
/// </summary>
public sealed class OrderAfterSalesWindowClosedDomainEvent : DomainEventBase
{
    public Guid OrderId { get; init; }
    public Guid UserId { get; init; }
    public decimal PaidAmount { get; init; }
    public DateTime ClosedAt { get; init; }

    public OrderAfterSalesWindowClosedDomainEvent(Guid orderId, Guid userId, decimal paidAmount, DateTime closedAt)
        : base(orderId)
    {
        OrderId = orderId;
        UserId = userId;
        PaidAmount = paidAmount;
        ClosedAt = closedAt;
    }
}

/// <summary>
/// 秒杀订单确认领域事件，由 <see cref="Aggregates.Order"/> 聚合在 MarkSeckillOrderCreated 方法中收集。
/// mapper 翻译为 <see cref="Leno.SharedContracts.Events.SeckillOrderConfirmedIntegrationEvent"/> 集成事件对外发布。
/// 消费方：促销域（标记预占记录为已履约，补偿任务跳过）。
/// </summary>
public sealed class SeckillOrderConfirmedDomainEvent : DomainEventBase
{
    public Guid ActivityId { get; init; }
    public Guid OrderId { get; init; }

    public SeckillOrderConfirmedDomainEvent(Guid activityId, Guid orderId)
        : base(orderId)
    {
        ActivityId = activityId;
        OrderId = orderId;
    }
}

/// <summary>
/// 支付请求领域事件，由 <see cref="Aggregates.Order"/> 聚合在 MarkPaymentInitiated 方法中收集。
/// mapper 翻译为 <see cref="Leno.SharedContracts.Events.PaymentRequestedIntegrationEvent"/> 集成事件对外发布。
/// 消费方：支付域（创建支付单并拉起第三方支付）。
/// </summary>
public sealed class PaymentRequestedDomainEvent : DomainEventBase
{
    public Guid OrderId { get; init; }
    public Guid UserId { get; init; }
    public decimal Amount { get; init; }
    public string Currency { get; init; } = "CNY";
    public string Channel { get; init; } = string.Empty;
    public DateTime RequestedAt { get; init; }

    public PaymentRequestedDomainEvent(
        Guid orderId, Guid userId, decimal amount, string currency, string channel, DateTime requestedAt)
        : base(orderId)
    {
        OrderId = orderId;
        UserId = userId;
        Amount = amount;
        Currency = string.IsNullOrWhiteSpace(currency) ? "CNY" : currency;
        Channel = channel ?? string.Empty;
        RequestedAt = requestedAt;
    }
}

/// <summary>
/// 退款请求领域事件，由 <see cref="Aggregates.Order"/> 聚合在 AddForceCancelRefundRequestedEvent 方法中收集。
/// mapper 翻译为 <see cref="Leno.SharedContracts.Events.RefundRequestedIntegrationEvent"/> 集成事件对外发布。
/// 消费方：支付域（创建退款单）。
/// </summary>
public sealed class RefundRequestedDomainEvent : DomainEventBase
{
    public Guid RefundId { get; init; }
    public Guid OrderId { get; init; }
    public Guid UserId { get; init; }
    public Guid AfterSalesId { get; init; }
    public Guid PaymentId { get; init; }
    public decimal RefundAmount { get; init; }
    public string Currency { get; init; } = "CNY";
    public string Channel { get; init; } = string.Empty;
    public string RefundReason { get; init; } = string.Empty;

    public RefundRequestedDomainEvent(
        Guid refundId, Guid orderId, Guid userId, Guid afterSalesId,
        Guid paymentId, decimal refundAmount, string currency, string channel, string refundReason)
        : base(orderId)
    {
        RefundId = refundId;
        OrderId = orderId;
        UserId = userId;
        AfterSalesId = afterSalesId;
        PaymentId = paymentId;
        RefundAmount = refundAmount;
        Currency = string.IsNullOrWhiteSpace(currency) ? "CNY" : currency;
        Channel = channel ?? string.Empty;
        RefundReason = refundReason ?? string.Empty;
    }
}
