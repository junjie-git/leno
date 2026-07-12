using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;

namespace Leno.SharedContracts.Events;

/// <summary>
/// 售后申请提交集成事件，评价与售后域在售后单创建时发布。
/// 消费方：卖家/运营处理队列、消息通知域。
/// Type 为 int 而非枚举，因共享契约层不可引用领域层枚举；发布方按 (int)AfterSalesType 转换。
/// 同时实现 <see cref="IDomainEvent"/> 以便售后域经发件箱模式在同一事务内持久化。
/// 事件契约定义在共享层，变更需所有消费方协商。
/// </summary>
public sealed class AfterSalesSubmittedEvent : IntegrationEventBase, IDomainEvent
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
/// 同时实现 <see cref="IDomainEvent"/> 以便售后域经发件箱模式在同一事务内持久化。
/// 事件契约定义在共享层，变更需所有消费方协商。
/// </summary>
public sealed class AfterSalesApprovedEvent : IntegrationEventBase, IDomainEvent
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
/// 同时实现 <see cref="IDomainEvent"/> 以便售后域经发件箱模式在同一事务内持久化。
/// </summary>
public sealed class AfterSalesRejectedEvent : IntegrationEventBase, IDomainEvent
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
/// 同时实现 <see cref="IDomainEvent"/> 以便售后域经发件箱模式在同一事务内持久化。
/// </summary>
public sealed class AfterSalesReturnedEvent : IntegrationEventBase, IDomainEvent
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
/// 同时实现 <see cref="IDomainEvent"/> 以便售后域经发件箱模式在同一事务内持久化。
/// </summary>
public sealed class AfterSalesReturnConfirmedEvent : IntegrationEventBase, IDomainEvent
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
