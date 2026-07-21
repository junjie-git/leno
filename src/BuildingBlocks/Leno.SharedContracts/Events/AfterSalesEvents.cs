using Leno.SharedContracts.Events;

namespace Leno.SharedContracts.Events;

/// <summary>
/// 售后申请提交集成事件，评价与售后域在售后单创建时发布。
/// 消费方：卖家/运营处理队列、消息通知域。
/// Type 为 int 而非枚举，因共享契约层不可引用领域层枚举；发布方按 (int)AfterSalesType 转换。
/// 事件契约定义在共享层，变更需所有消费方协商。
/// </summary>
public sealed class AfterSalesSubmittedEvent : IntegrationEventBase
{
    /// <summary>售后单标识。</summary>
    public Guid AfterSalesId { get; init; }

    /// <summary>订单标识。</summary>
    public Guid OrderId { get; init; }

    /// <summary>订单行标识，整单售后时为空。</summary>
    public Guid? OrderLineId { get; init; }

    /// <summary>申请人（买家）标识。</summary>
    public Guid UserId { get; init; }

    /// <summary>被申请卖家标识，语义等同卖家与店铺管理域的 ShopId。</summary>
    public Guid SellerId { get; init; }

    /// <summary>售后类型（AfterSalesType 枚举的 int 值：0=ReturnRefund, 1=RefundOnly）。</summary>
    public int Type { get; init; }

    /// <summary>申请金额。</summary>
    public decimal RequestedAmount { get; init; }

    /// <summary>币种（ISO 4217），默认 CNY。</summary>
    public string Currency { get; init; } = "CNY";

    /// <summary>聚合根标识，用于发件箱归类。</summary>
    public Guid AggregateId => AfterSalesId;

    /// <summary>供 System.Text.Json 反序列化使用的无参构造。</summary>
    public AfterSalesSubmittedEvent() : base()
    {
    }

    public AfterSalesSubmittedEvent(
        Guid afterSalesId,
        Guid orderId,
        Guid? orderLineId,
        Guid userId,
        Guid sellerId,
        int type,
        decimal requestedAmount,
        string currency) : base()
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
/// 售后审核同意集成事件，评价与售后域在售后单审核通过时发布。
/// 消费方：消息通知域（通知买家退货/退款）。
/// Type 为 int 而非枚举，因共享契约层不可引用领域层枚举。
/// 事件契约定义在共享层，变更需所有消费方协商。
/// </summary>
public sealed class AfterSalesApprovedEvent : IntegrationEventBase
{
    /// <summary>售后单标识。</summary>
    public Guid AfterSalesId { get; init; }

    /// <summary>订单标识。</summary>
    public Guid OrderId { get; init; }

    /// <summary>申请人（买家）标识。</summary>
    public Guid UserId { get; init; }

    /// <summary>被申请卖家标识。</summary>
    public Guid SellerId { get; init; }

    /// <summary>审核同意金额。</summary>
    public decimal ApprovedAmount { get; init; }

    /// <summary>币种（ISO 4217），默认 CNY。</summary>
    public string Currency { get; init; } = "CNY";

    /// <summary>售后类型（AfterSalesType 枚举的 int 值）。</summary>
    public int Type { get; init; }

    /// <summary>聚合根标识，用于发件箱归类。</summary>
    public Guid AggregateId => AfterSalesId;

    /// <summary>供 System.Text.Json 反序列化使用的无参构造。</summary>
    public AfterSalesApprovedEvent() : base()
    {
    }

    public AfterSalesApprovedEvent(
        Guid afterSalesId,
        Guid orderId,
        Guid userId,
        Guid sellerId,
        decimal approvedAmount,
        string currency,
        int type) : base()
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
/// 售后驳回集成事件，评价与售后域在售后单被驳回时发布。
/// 消费方：消息通知域（通知买家驳回原因）。
/// </summary>
public sealed class AfterSalesRejectedEvent : IntegrationEventBase
{
    /// <summary>售后单标识。</summary>
    public Guid AfterSalesId { get; init; }

    /// <summary>订单标识。</summary>
    public Guid OrderId { get; init; }

    /// <summary>申请人（买家）标识。</summary>
    public Guid UserId { get; init; }

    /// <summary>驳回原因。</summary>
    public string RejectReason { get; init; } = string.Empty;

    /// <summary>聚合根标识，用于发件箱归类。</summary>
    public Guid AggregateId => AfterSalesId;

    public AfterSalesRejectedEvent() : base()
    {
    }

    public AfterSalesRejectedEvent(Guid afterSalesId, Guid orderId, Guid userId, string rejectReason) : base()
    {
        AfterSalesId = afterSalesId;
        OrderId = orderId;
        UserId = userId;
        RejectReason = rejectReason ?? string.Empty;
    }
}

/// <summary>
/// 买家退货集成事件，评价与售后域在买家寄回商品后发布。
/// 消费方：消息通知域（通知卖家确认收货）。
/// </summary>
public sealed class AfterSalesReturnedEvent : IntegrationEventBase
{
    /// <summary>售后单标识。</summary>
    public Guid AfterSalesId { get; init; }

    /// <summary>订单标识。</summary>
    public Guid OrderId { get; init; }

    /// <summary>被申请卖家标识。</summary>
    public Guid SellerId { get; init; }

    /// <summary>退货物流单号。</summary>
    public string TrackingNo { get; init; } = string.Empty;

    /// <summary>聚合根标识，用于发件箱归类。</summary>
    public Guid AggregateId => AfterSalesId;

    public AfterSalesReturnedEvent() : base()
    {
    }

    public AfterSalesReturnedEvent(Guid afterSalesId, Guid orderId, Guid sellerId, string trackingNo) : base()
    {
        AfterSalesId = afterSalesId;
        OrderId = orderId;
        SellerId = sellerId;
        TrackingNo = trackingNo ?? string.Empty;
    }
}

/// <summary>
/// 卖家确认收货集成事件，评价与售后域在卖家确认收到退货后发布。
/// 消费方：消息通知域（通知买家退货已确认）、支付域（准备退款）。
/// </summary>
public sealed class AfterSalesReturnConfirmedEvent : IntegrationEventBase
{
    /// <summary>售后单标识。</summary>
    public Guid AfterSalesId { get; init; }

    /// <summary>订单标识。</summary>
    public Guid OrderId { get; init; }

    /// <summary>申请人（买家）标识。</summary>
    public Guid UserId { get; init; }

    /// <summary>确认退款金额。</summary>
    public decimal RefundAmount { get; init; }

    /// <summary>聚合根标识，用于发件箱归类。</summary>
    public Guid AggregateId => AfterSalesId;

    public AfterSalesReturnConfirmedEvent() : base()
    {
    }

    public AfterSalesReturnConfirmedEvent(Guid afterSalesId, Guid orderId, Guid userId, decimal refundAmount) : base()
    {
        AfterSalesId = afterSalesId;
        OrderId = orderId;
        UserId = userId;
        RefundAmount = refundAmount;
    }
}

/// <summary>
/// 售后退款失败集成事件，评价与售后域在退款失败时发布（与支付域 RefundFailedEvent 区分）。
/// 消费方：消息通知域（通知买家退款失败并可重试）、订单域（更新售后状态视图）。
/// 事件契约定义在共享层，变更需所有消费方协商。
/// </summary>
public sealed class AfterSalesRefundFailedEvent : IntegrationEventBase
{
    /// <summary>售后单标识。</summary>
    public Guid AfterSalesId { get; init; }

    /// <summary>订单标识。</summary>
    public Guid OrderId { get; init; }

    /// <summary>申请人（买家）标识。</summary>
    public Guid UserId { get; init; }

    /// <summary>失败原因。</summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>聚合根标识，用于发件箱归类。</summary>
    public Guid AggregateId => AfterSalesId;

    public AfterSalesRefundFailedEvent() : base()
    {
    }

    public AfterSalesRefundFailedEvent(Guid afterSalesId, Guid orderId, Guid userId, string reason) : base()
    {
        AfterSalesId = afterSalesId;
        OrderId = orderId;
        UserId = userId;
        Reason = reason ?? string.Empty;
    }
}

/// <summary>
/// 售后单撤销集成事件，评价与售后域在买家撤销售后单时发布。
/// 消费方：消息通知域（通知卖家售后单已撤销）、促销域（释放售后单关联的优惠券锁定）。
/// 事件契约定义在共享层，变更需所有消费方协商。
/// </summary>
public sealed class AfterSalesCancelledEvent : IntegrationEventBase
{
    /// <summary>售后单标识。</summary>
    public Guid AfterSalesId { get; init; }

    /// <summary>订单标识。</summary>
    public Guid OrderId { get; init; }

    /// <summary>申请人（买家）标识。</summary>
    public Guid UserId { get; init; }

    /// <summary>被申请卖家标识。</summary>
    public Guid SellerId { get; init; }

    /// <summary>撤销原因。</summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>聚合根标识，用于发件箱归类。</summary>
    public Guid AggregateId => AfterSalesId;

    public AfterSalesCancelledEvent() : base()
    {
    }

    public AfterSalesCancelledEvent(Guid afterSalesId, Guid orderId, Guid userId, Guid sellerId, string reason) : base()
    {
        AfterSalesId = afterSalesId;
        OrderId = orderId;
        UserId = userId;
        SellerId = sellerId;
        Reason = reason ?? string.Empty;
    }
}

/// <summary>
/// 售后退款完成集成事件，由评价与售后域在售后单退款完成时发布（P0-2.11 解除事件回环）。
/// 消费方：订单域（回滚销量）、促销域（退还优惠券）、消息通知域（退款到账通知）。
/// 注意：本事件与支付域 <see cref="RefundCompletedEvent"/> 区分：
/// - <see cref="RefundCompletedEvent"/> 仅由支付域 RefundOrder 聚合在第三方退款到账后发布；
/// - <see cref="AfterSalesRefundCompletedEvent"/> 由售后域在消费 <see cref="RefundCompletedEvent"/> 标记售后单完成后发布，
///   表达售后域视角的退款完成事实，避免售后域消费自己发布的 <see cref="RefundCompletedEvent"/> 造成回环。
/// 事件契约定义在共享层，变更需所有消费方协商。
/// </summary>
public sealed class AfterSalesRefundCompletedEvent : IntegrationEventBase
{
    /// <summary>售后单标识。</summary>
    public Guid AfterSalesId { get; init; }

    /// <summary>订单标识。</summary>
    public Guid OrderId { get; init; }

    /// <summary>申请人（买家）标识。</summary>
    public Guid UserId { get; init; }

    /// <summary>退款单标识。</summary>
    public Guid RefundId { get; init; }

    /// <summary>退款金额。</summary>
    public decimal RefundAmount { get; init; }

    /// <summary>币种（ISO 4217），默认 CNY。</summary>
    public string Currency { get; init; } = "CNY";

    /// <summary>退款完成时间（UTC）。</summary>
    public DateTime CompletedAt { get; init; }

    /// <summary>
    /// 第三方支付渠道返回的退款流水号（如微信 refund_id、支付宝 trade_no）。
    /// 默认 string.Empty 保持向后兼容；由售后域从支付域 <see cref="RefundCompletedEvent.ChannelRefundNo"/> 透传。
    /// </summary>
    public string ChannelRefundNo { get; init; } = string.Empty;

    /// <summary>聚合根标识，用于发件箱归类。</summary>
    public Guid AggregateId => AfterSalesId;

    /// <summary>供 System.Text.Json 反序列化使用的无参构造。</summary>
    public AfterSalesRefundCompletedEvent() : base()
    {
    }

    public AfterSalesRefundCompletedEvent(
        Guid afterSalesId,
        Guid orderId,
        Guid userId,
        Guid refundId,
        decimal refundAmount,
        string currency,
        DateTime completedAt,
        string channelRefundNo) : base()
    {
        AfterSalesId = afterSalesId;
        OrderId = orderId;
        UserId = userId;
        RefundId = refundId;
        RefundAmount = refundAmount;
        Currency = string.IsNullOrWhiteSpace(currency) ? "CNY" : currency;
        CompletedAt = completedAt;
        ChannelRefundNo = channelRefundNo ?? string.Empty;
    }
}
