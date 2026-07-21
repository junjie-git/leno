using Leno.SharedKernel.Abstractions;

namespace Leno.ReviewAfterSales.Domain.Events;

/// <summary>
/// 售后申请提交领域事件，由 <see cref="Aggregates.AfterSales"/> 聚合在 Create 工厂方法中收集。
/// mapper 翻译为 <see cref="Leno.SharedContracts.Events.AfterSalesSubmittedEvent"/> 集成事件对外发布。
/// 消费方：卖家/运营处理队列、消息通知域。
/// </summary>
public sealed class AfterSalesSubmittedDomainEvent : DomainEventBase
{
    public Guid AfterSalesId { get; init; }
    public Guid OrderId { get; init; }
    public Guid? OrderLineId { get; init; }
    public Guid UserId { get; init; }
    public Guid SellerId { get; init; }
    public int Type { get; init; }
    public decimal RequestedAmount { get; init; }
    public string Currency { get; init; } = "CNY";

    public AfterSalesSubmittedDomainEvent(
        Guid afterSalesId, Guid orderId, Guid? orderLineId, Guid userId,
        Guid sellerId, int type, decimal requestedAmount, string currency)
        : base(afterSalesId)
    {
        AfterSalesId = afterSalesId;
        OrderId = orderId;
        OrderLineId = orderLineId;
        UserId = userId;
        SellerId = sellerId;
        Type = type;
        RequestedAmount = requestedAmount;
        Currency = string.IsNullOrWhiteSpace(currency) ? "CNY" : currency;
    }
}

/// <summary>
/// 售后审核同意领域事件，由 <see cref="Aggregates.AfterSales"/> 聚合在 Approve 方法中收集。
/// mapper 翻译为 <see cref="Leno.SharedContracts.Events.AfterSalesApprovedEvent"/> 集成事件对外发布。
/// 消费方：消息通知域（通知买家退货/退款）。
/// </summary>
public sealed class AfterSalesApprovedDomainEvent : DomainEventBase
{
    public Guid AfterSalesId { get; init; }
    public Guid OrderId { get; init; }
    public Guid UserId { get; init; }
    public Guid SellerId { get; init; }
    public decimal ApprovedAmount { get; init; }
    public string Currency { get; init; } = "CNY";
    public int Type { get; init; }

    public AfterSalesApprovedDomainEvent(
        Guid afterSalesId, Guid orderId, Guid userId, Guid sellerId,
        decimal approvedAmount, string currency, int type)
        : base(afterSalesId)
    {
        AfterSalesId = afterSalesId;
        OrderId = orderId;
        UserId = userId;
        SellerId = sellerId;
        ApprovedAmount = approvedAmount;
        Currency = string.IsNullOrWhiteSpace(currency) ? "CNY" : currency;
        Type = type;
    }
}

/// <summary>
/// 售后驳回领域事件，由 <see cref="Aggregates.AfterSales"/> 聚合在 Reject 方法中收集。
/// mapper 翻译为 <see cref="Leno.SharedContracts.Events.AfterSalesRejectedEvent"/> 集成事件对外发布。
/// 消费方：消息通知域（通知买家驳回原因）。
/// </summary>
public sealed class AfterSalesRejectedDomainEvent : DomainEventBase
{
    public Guid AfterSalesId { get; init; }
    public Guid OrderId { get; init; }
    public Guid UserId { get; init; }
    public string RejectReason { get; init; } = string.Empty;

    public AfterSalesRejectedDomainEvent(Guid afterSalesId, Guid orderId, Guid userId, string rejectReason)
        : base(afterSalesId)
    {
        AfterSalesId = afterSalesId;
        OrderId = orderId;
        UserId = userId;
        RejectReason = rejectReason ?? string.Empty;
    }
}

/// <summary>
/// 买家退货领域事件，由 <see cref="Aggregates.AfterSales"/> 聚合在 ReturnGoods 方法中收集。
/// mapper 翻译为 <see cref="Leno.SharedContracts.Events.AfterSalesReturnedEvent"/> 集成事件对外发布。
/// 消费方：消息通知域（通知卖家确认收货）。
/// </summary>
public sealed class AfterSalesReturnedDomainEvent : DomainEventBase
{
    public Guid AfterSalesId { get; init; }
    public Guid OrderId { get; init; }
    public Guid SellerId { get; init; }
    public string TrackingNo { get; init; } = string.Empty;

    public AfterSalesReturnedDomainEvent(Guid afterSalesId, Guid orderId, Guid sellerId, string trackingNo)
        : base(afterSalesId)
    {
        AfterSalesId = afterSalesId;
        OrderId = orderId;
        SellerId = sellerId;
        TrackingNo = trackingNo ?? string.Empty;
    }
}

/// <summary>
/// 卖家确认收货领域事件，由 <see cref="Aggregates.AfterSales"/> 聚合在 ConfirmReturn 方法中收集。
/// mapper 翻译为 <see cref="Leno.SharedContracts.Events.AfterSalesReturnConfirmedEvent"/> 集成事件对外发布。
/// 消费方：消息通知域（通知买家退货已确认）、支付域（准备退款）。
/// </summary>
public sealed class AfterSalesReturnConfirmedDomainEvent : DomainEventBase
{
    public Guid AfterSalesId { get; init; }
    public Guid OrderId { get; init; }
    public Guid UserId { get; init; }
    public decimal RefundAmount { get; init; }

    public AfterSalesReturnConfirmedDomainEvent(Guid afterSalesId, Guid orderId, Guid userId, decimal refundAmount)
        : base(afterSalesId)
    {
        AfterSalesId = afterSalesId;
        OrderId = orderId;
        UserId = userId;
        RefundAmount = refundAmount;
    }
}

/// <summary>
/// 售后退款完成领域事件，由 <see cref="Aggregates.AfterSales"/> 聚合在 MarkRefundCompleted 方法中收集。
/// mapper 翻译为 <see cref="Leno.SharedContracts.Events.AfterSalesRefundCompletedEvent"/> 集成事件对外发布。
/// 消费方：订单域（回滚销量）、促销域（退还优惠券）、消息通知域（退款到账通知）。
/// 注意：P0-2.11 解除事件回环后，本事件不再翻译为 <see cref="Leno.SharedContracts.Events.RefundCompletedEvent"/>
/// （RefundCompletedEvent 仅由支付域发布），改翻译为独立的 AfterSalesRefundCompletedEvent，
/// 避免售后域消费自己发布的事件造成回环。
/// </summary>
public sealed class AfterSalesRefundCompletedDomainEvent : DomainEventBase
{
    public Guid OrderId { get; init; }
    public Guid UserId { get; init; }
    public Guid RefundId { get; init; }
    public Guid AfterSalesId { get; init; }
    public decimal RefundAmount { get; init; }
    public string Currency { get; init; } = "CNY";
    public DateTime CompletedAt { get; init; }
    /// <summary>第三方支付渠道返回的退款流水号，由 MarkRefundCompleted 透传，用于下游财务对账。</summary>
    public string ChannelRefundNo { get; init; } = string.Empty;

    public AfterSalesRefundCompletedDomainEvent(
        Guid orderId, Guid userId, Guid refundId, Guid afterSalesId,
        decimal refundAmount, string currency, DateTime completedAt, string channelRefundNo)
        : base(afterSalesId)
    {
        OrderId = orderId;
        UserId = userId;
        RefundId = refundId;
        AfterSalesId = afterSalesId;
        RefundAmount = refundAmount;
        Currency = string.IsNullOrWhiteSpace(currency) ? "CNY" : currency;
        CompletedAt = completedAt;
        ChannelRefundNo = channelRefundNo ?? string.Empty;
    }
}

/// <summary>
/// 售后退款请求领域事件，由 <see cref="Aggregates.AfterSales"/> 聚合在 AddRefundRequestedEvent 方法中收集。
/// mapper 翻译为 <see cref="Leno.SharedContracts.Events.RefundRequestedIntegrationEvent"/> 集成事件对外发布。
/// 消费方：支付域（创建退款单执行退款）。
/// 注意：RefundRequestedIntegrationEvent 同时由订单域发布，本事件表达售后域视角的退款请求事实。
/// </summary>
public sealed class AfterSalesRefundRequestedDomainEvent : DomainEventBase
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

    public AfterSalesRefundRequestedDomainEvent(
        Guid refundId, Guid orderId, Guid userId, Guid afterSalesId,
        Guid paymentId, decimal refundAmount, string currency, string channel, string refundReason)
        : base(afterSalesId)
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

/// <summary>
/// 评价提交领域事件，由 <see cref="Aggregates.Review"/> 聚合在 Create 工厂方法中收集。
/// mapper 翻译为 <see cref="Leno.SharedContracts.Events.ReviewSubmittedEvent"/> 集成事件对外发布。
/// 消费方：商品域（回写商品评分摘要 score、reviewCount、好评率）。
/// </summary>
public sealed class ReviewSubmittedDomainEvent : DomainEventBase
{
    public Guid ReviewId { get; init; }
    public Guid UserId { get; init; }
    public Guid SpuId { get; init; }
    public int Rating { get; init; }
    public double NewScore { get; init; }
    public int ReviewCount { get; init; }

    public ReviewSubmittedDomainEvent(
        Guid reviewId, Guid userId, Guid spuId, int rating, double newScore, int reviewCount)
        : base(reviewId)
    {
        ReviewId = reviewId;
        UserId = userId;
        SpuId = spuId;
        Rating = rating;
        NewScore = newScore;
        ReviewCount = reviewCount;
    }
}

/// <summary>
/// 评价审核通过领域事件，由 <see cref="Aggregates.Review"/> 聚合在 Approve 方法中收集。
/// mapper 翻译为 <see cref="Leno.SharedContracts.Events.ReviewApprovedEvent"/> 集成事件对外发布。
/// 消费方：积分域（驱动积分发放）、商品域（重算评分摘要）、消息通知域。
/// </summary>
public sealed class ReviewApprovedDomainEvent : DomainEventBase
{
    public Guid ReviewId { get; init; }
    public Guid UserId { get; init; }
    public Guid SpuId { get; init; }
    public int Rating { get; init; }

    public ReviewApprovedDomainEvent(Guid reviewId, Guid userId, Guid spuId, int rating)
        : base(reviewId)
    {
        ReviewId = reviewId;
        UserId = userId;
        SpuId = spuId;
        Rating = rating;
    }
}

/// <summary>
/// 评价隐藏领域事件，由 <see cref="Aggregates.Review"/> 聚合在 Hide 方法中收集。
/// mapper 翻译为 <see cref="Leno.SharedContracts.Events.ReviewHiddenEvent"/> 集成事件对外发布。
/// 消费方：商品域（重算评分摘要，隐藏后从统计中移除）。
/// </summary>
public sealed class ReviewHiddenDomainEvent : DomainEventBase
{
    public Guid ReviewId { get; init; }
    public Guid SpuId { get; init; }
    public int Rating { get; init; }

    public ReviewHiddenDomainEvent(Guid reviewId, Guid spuId, int rating)
        : base(reviewId)
    {
        ReviewId = reviewId;
        SpuId = spuId;
        Rating = rating;
    }
}

/// <summary>
/// 评价审核结果领域事件，表达运营审核（通过/隐藏）后的结果事实。
/// mapper 翻译为 <see cref="Leno.SharedContracts.Events.ReviewModeratedEvent"/> 集成事件对外发布。
/// 消费方：商品域（重算评分摘要）、消息通知域。
/// Status 为 int 而非枚举，与集成事件契约保持一致；发布方按 (int)ReviewStatus 转换。
/// </summary>
public sealed class ReviewModeratedDomainEvent : DomainEventBase
{
    public Guid ReviewId { get; init; }
    public int Status { get; init; }
    public string Action { get; init; } = string.Empty;

    public ReviewModeratedDomainEvent(Guid reviewId, int status, string action)
        : base(reviewId)
    {
        ReviewId = reviewId;
        Status = status;
        Action = action ?? string.Empty;
    }
}

/// <summary>
/// 售后退款失败领域事件，由 <see cref="Aggregates.AfterSales"/> 聚合在 MarkRefundFailed 方法中收集。
/// mapper 翻译为 <see cref="Leno.SharedContracts.Events.AfterSalesRefundFailedEvent"/> 集成事件对外发布。
/// 消费方：消息通知域（通知买家退款失败并可重试）、订单域（更新售后状态视图）。
/// </summary>
public sealed class AfterSalesRefundFailedDomainEvent : DomainEventBase
{
    public Guid AfterSalesId { get; init; }
    public Guid OrderId { get; init; }
    public Guid UserId { get; init; }
    public string Reason { get; init; } = string.Empty;

    public AfterSalesRefundFailedDomainEvent(Guid afterSalesId, Guid orderId, Guid userId, string reason)
        : base(afterSalesId)
    {
        AfterSalesId = afterSalesId;
        OrderId = orderId;
        UserId = userId;
        Reason = reason ?? string.Empty;
    }
}

/// <summary>
/// 售后单撤销领域事件，由 <see cref="Aggregates.AfterSales"/> 聚合在 Cancel 方法中收集。
/// mapper 翻译为 <see cref="Leno.SharedContracts.Events.AfterSalesCancelledEvent"/> 集成事件对外发布。
/// 消费方：消息通知域（通知卖家售后单已撤销）、促销域（释放售后单关联的优惠券锁定）。
/// </summary>
public sealed class AfterSalesCancelledDomainEvent : DomainEventBase
{
    public Guid AfterSalesId { get; init; }
    public Guid OrderId { get; init; }
    public Guid UserId { get; init; }
    public Guid SellerId { get; init; }
    public string Reason { get; init; } = string.Empty;

    public AfterSalesCancelledDomainEvent(Guid afterSalesId, Guid orderId, Guid userId, Guid sellerId, string reason)
        : base(afterSalesId)
    {
        AfterSalesId = afterSalesId;
        OrderId = orderId;
        UserId = userId;
        SellerId = sellerId;
        Reason = reason ?? string.Empty;
    }
}
