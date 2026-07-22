using Leno.SharedContracts.Events;

namespace Leno.SharedContracts.Events;

/// <summary>
/// 订单创建集成事件，订单域发布。
/// 消费方：购物车域（清空已结算项）、促销域（锁定优惠券）、积分与会员域（冻结积分）、消息通知域（下单成功通知）、MQ 延迟消息（30 分钟超时取消）。
/// 事件契约定义在共享层，变更需所有消费方协商。
/// </summary>
public sealed class OrderCreatedEvent : IntegrationEventBase
{
    /// <summary>订单标识。</summary>
    public Guid OrderId { get; init; }

    /// <summary>买家账号标识（用户域 UserId）。</summary>
    public Guid BuyerId { get; init; }

    /// <summary>卖家（店铺）标识，语义等同卖家与店铺管理域的 ShopId。</summary>
    public Guid SellerId { get; init; }

    /// <summary>订单总金额。</summary>
    public decimal TotalAmount { get; init; }

    /// <summary>币种（ISO 4217）。</summary>
    public string Currency { get; init; } = "CNY";

    /// <summary>创建时间（UTC）。</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>本次订单结算来源的购物车项标识列表，供购物车域清空已结算项。</summary>
    public IReadOnlyList<Guid> SourceCartItemIds { get; init; } = Array.Empty<Guid>();

    /// <summary>聚合根标识，用于发件箱归类。</summary>
    public Guid AggregateId => OrderId;

    /// <summary>供 System.Text.Json 反序列化使用的无参构造。</summary>
    public OrderCreatedEvent() : base()
    {
    }

    public OrderCreatedEvent(
        Guid orderId,
        Guid buyerId,
        Guid sellerId,
        decimal totalAmount,
        string currency,
        DateTime createdAt,
        IReadOnlyList<Guid> sourceCartItemIds) : base()
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
/// 订单完成集成事件，订单域发布，卖家域消费维护店铺销量与销售额。
/// 事件契约定义在共享层，变更需所有消费方协商。
/// </summary>
public sealed class OrderCompletedEvent : IntegrationEventBase
{
    /// <summary>订单标识。</summary>
    public Guid OrderId { get; init; }

    /// <summary>买家账号标识。</summary>
    public Guid UserId { get; init; }

    /// <summary>卖家（店铺）标识，语义等同卖家与店铺管理域的 ShopId。</summary>
    public Guid SellerId { get; init; }

    /// <summary>订单总金额。</summary>
    public decimal TotalAmount { get; init; }

    /// <summary>币种（ISO 4217）。</summary>
    public string Currency { get; init; } = "CNY";

    /// <summary>完成时间（UTC）。</summary>
    public DateTime CompletedAt { get; init; }

    /// <summary>聚合根标识，用于发件箱归类。</summary>
    public Guid AggregateId => OrderId;

    /// <summary>供 System.Text.Json 反序列化使用的无参构造。</summary>
    public OrderCompletedEvent() : base()
    {
    }

    public OrderCompletedEvent(Guid orderId, Guid userId, Guid sellerId, decimal totalAmount, string currency, DateTime completedAt)
        : base()
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
/// 订单支付成功集成事件，订单域消费 <c>PaymentSucceededIntegrationEvent</c> 后发布。
/// 消费方：促销域（核销优惠券）、积分与会员域（正式扣减冻结抵现积分/开通会员）、
/// 卖家域（通知发货）、消息通知域（支付成功通知）、库存（确认真实扣减）。
/// 事件契约定义在共享层，变更需所有消费方协商。
/// </summary>
public sealed class OrderPaidEvent : IntegrationEventBase
{
    /// <summary>订单标识。</summary>
    public Guid OrderId { get; init; }

    /// <summary>买家账号标识。</summary>
    public Guid UserId { get; init; }

    /// <summary>卖家（店铺）标识，语义等同卖家与店铺管理域的 ShopId。</summary>
    public Guid SellerId { get; init; }

    /// <summary>支付单标识。</summary>
    public Guid PaymentId { get; init; }

    /// <summary>支付渠道。</summary>
    public string Channel { get; init; } = string.Empty;

    /// <summary>支付时间（UTC）。</summary>
    public DateTime PaidAt { get; init; }

    /// <summary>第三方交易号。</summary>
    public string TradeNo { get; init; } = string.Empty;

    /// <summary>实付金额。</summary>
    public decimal Amount { get; init; }

    /// <summary>币种（ISO 4217）。</summary>
    public string Currency { get; init; } = "CNY";

    /// <summary>聚合根标识，用于发件箱归类。</summary>
    public Guid AggregateId => OrderId;

    /// <summary>供 System.Text.Json 反序列化使用的无参构造。</summary>
    public OrderPaidEvent() : base()
    {
    }

    public OrderPaidEvent(
        Guid orderId,
        Guid userId,
        Guid sellerId,
        Guid paymentId,
        string channel,
        DateTime paidAt,
        string tradeNo,
        decimal amount,
        string currency) : base()
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
/// 订单取消集成事件，订单域在待支付态或已支付态取消时发布（买家主动取消、支付超时自动取消或售后退款取消）。
/// 消费方：促销域（退还锁定优惠券）、积分与会员域（释放冻结抵现积分）、
/// 库存（释放预占）、消息通知域（取消通知）、卖家与店铺管理域（已支付取消回滚收入）。
/// 事件契约定义在共享层，变更需所有消费方协商。
/// </summary>
public sealed class OrderCancelledEvent : IntegrationEventBase
{
    /// <summary>订单标识。</summary>
    public Guid OrderId { get; init; }

    /// <summary>卖家（店铺）标识，语义等同卖家与店铺管理域的 ShopId。</summary>
    public Guid SellerId { get; init; }

    /// <summary>取消原因。</summary>
    public string CancelReason { get; init; } = string.Empty;

    /// <summary>取消时间（UTC）。</summary>
    public DateTime CancelledAt { get; init; }

    /// <summary>取消方（Buyer/System）。</summary>
    public string CancelledBy { get; init; } = string.Empty;

    /// <summary>需释放的冻结积分（供积分域回退），0 表示无冻结。</summary>
    public int PointsToRelease { get; init; }

    /// <summary>
    /// 是否为已支付订单的取消（退款）。true 表示订单已支付，消费方应回滚已计入的收入；
    /// false（默认）表示未支付订单的取消，仅做待处理订单数 -1。
    /// 旧版发布方未填充时默认 false，保持向后兼容。
    /// </summary>
    public bool WasPaid { get; init; }

    /// <summary>
    /// 退款金额（仅当 <see cref="WasPaid"/> 为 true 时有意义），用于卖家域回滚 TotalRevenue。
    /// 默认 0；旧版发布方未填充时为 0，消费方在 WasPaid=true 但 RefundAmount=0 时不回滚收入。
    /// </summary>
    public decimal RefundAmount { get; init; }

    /// <summary>聚合根标识，用于发件箱归类。</summary>
    public Guid AggregateId => OrderId;

    /// <summary>供 System.Text.Json 反序列化使用的无参构造。</summary>
    public OrderCancelledEvent() : base()
    {
    }

    public OrderCancelledEvent(
        Guid orderId,
        Guid sellerId,
        string cancelReason,
        DateTime cancelledAt,
        string cancelledBy,
        int pointsToRelease) : base()
    {
        OrderId = orderId;
        SellerId = sellerId;
        CancelReason = cancelReason ?? string.Empty;
        CancelledAt = cancelledAt;
        CancelledBy = cancelledBy ?? string.Empty;
        PointsToRelease = pointsToRelease;
    }

    /// <summary>
    /// 构造函数（含已支付取消字段）。
    /// 订单域在已支付订单取消/退款时使用此构造函数填充 WasPaid=true 与 RefundAmount。
    /// </summary>
    /// <param name="orderId">订单标识。</param>
    /// <param name="sellerId">卖家（店铺）标识。</param>
    /// <param name="cancelReason">取消原因。</param>
    /// <param name="cancelledAt">取消时间（UTC）。</param>
    /// <param name="cancelledBy">取消方（Buyer/System）。</param>
    /// <param name="pointsToRelease">需释放的冻结积分。</param>
    /// <param name="wasPaid">是否为已支付订单的取消（退款）。</param>
    /// <param name="refundAmount">退款金额（已支付取消时填入实际退款金额）。</param>
    public OrderCancelledEvent(
        Guid orderId,
        Guid sellerId,
        string cancelReason,
        DateTime cancelledAt,
        string cancelledBy,
        int pointsToRelease,
        bool wasPaid,
        decimal refundAmount) : base()
    {
        OrderId = orderId;
        SellerId = sellerId;
        CancelReason = cancelReason ?? string.Empty;
        CancelledAt = cancelledAt;
        CancelledBy = cancelledBy ?? string.Empty;
        PointsToRelease = pointsToRelease;
        WasPaid = wasPaid;
        RefundAmount = refundAmount;
    }
}

/// <summary>
/// 订单发货集成事件，订单域在卖家发货时发布。
/// 消费方：消息通知域（发货通知）、卖家域（待发货数 -1）。
/// 事件契约定义在共享层，变更需所有消费方协商。
/// </summary>
public sealed class OrderShippedEvent : IntegrationEventBase
{
    /// <summary>订单标识。</summary>
    public Guid OrderId { get; init; }

    /// <summary>买家账号标识。</summary>
    public Guid UserId { get; init; }

    /// <summary>卖家（店铺）标识。</summary>
    public Guid SellerId { get; init; }

    /// <summary>物流单号。</summary>
    public string LogisticsNo { get; init; } = string.Empty;

    /// <summary>发货时间（UTC）。</summary>
    public DateTime ShippedAt { get; init; }

    /// <summary>聚合根标识，用于发件箱归类。</summary>
    public Guid AggregateId => OrderId;

    /// <summary>供 System.Text.Json 反序列化使用的无参构造。</summary>
    public OrderShippedEvent() : base()
    {
    }

    public OrderShippedEvent(Guid orderId, Guid userId, Guid sellerId, string logisticsNo, DateTime shippedAt)
        : base()
    {
        OrderId = orderId;
        UserId = userId;
        SellerId = sellerId;
        LogisticsNo = logisticsNo ?? string.Empty;
        ShippedAt = shippedAt;
    }
}

/// <summary>
/// 订单售后窗口关闭集成事件，订单域在售后窗口结束时发布。
/// 消费方：积分与会员域（确认发放购物积分）、卖家域（结算货款）。
/// 事件契约定义在共享层，变更需所有消费方协商。
/// </summary>
public sealed class OrderAfterSalesWindowClosedEvent : IntegrationEventBase
{
    /// <summary>订单标识。</summary>
    public Guid OrderId { get; init; }

    /// <summary>买家账号标识。</summary>
    public Guid UserId { get; init; }

    /// <summary>实付金额，用于积分域计算消费返积分（1 元 = 1 积分）。</summary>
    public decimal PaidAmount { get; init; }

    /// <summary>售后窗口关闭时间（UTC）。</summary>
    public DateTime ClosedAt { get; init; }

    /// <summary>聚合根标识，用于发件箱归类。</summary>
    public Guid AggregateId => OrderId;

    /// <summary>供 System.Text.Json 反序列化使用的无参构造。</summary>
    public OrderAfterSalesWindowClosedEvent() : base()
    {
    }

    public OrderAfterSalesWindowClosedEvent(Guid orderId, Guid userId, decimal paidAmount, DateTime closedAt)
        : base()
    {
        OrderId = orderId;
        UserId = userId;
        PaidAmount = paidAmount;
        ClosedAt = closedAt;
    }
}

/// <summary>
/// 支付请求集成事件，订单域在待支付订单发起支付时发布。
/// 消费方：支付域（创建支付单并拉起第三方支付）。
/// 事件契约定义在共享层，变更需所有消费方协商。
/// </summary>
public sealed class PaymentRequestedIntegrationEvent : IntegrationEventBase
{
    /// <summary>订单标识。</summary>
    public Guid OrderId { get; init; }

    /// <summary>买家账号标识。</summary>
    public Guid UserId { get; init; }

    /// <summary>应付金额。</summary>
    public decimal Amount { get; init; }

    /// <summary>币种（ISO 4217），默认 CNY。</summary>
    public string Currency { get; init; } = "CNY";

    /// <summary>支付渠道。</summary>
    public string Channel { get; init; } = string.Empty;

    /// <summary>请求时间（UTC）。</summary>
    public DateTime RequestedAt { get; init; }

    /// <summary>聚合根标识，用于发件箱归类。</summary>
    public Guid AggregateId => OrderId;

    /// <summary>供 System.Text.Json 反序列化使用的无参构造。</summary>
    public PaymentRequestedIntegrationEvent() : base()
    {
    }

    public PaymentRequestedIntegrationEvent(
        Guid orderId,
        Guid userId,
        decimal amount,
        string currency,
        string channel,
        DateTime requestedAt) : base()
    {
        OrderId = orderId;
        UserId = userId;
        Amount = amount;
        Currency = string.IsNullOrWhiteSpace(currency) ? "CNY" : currency;
        Channel = channel ?? string.Empty;
        RequestedAt = requestedAt;
    }
}
