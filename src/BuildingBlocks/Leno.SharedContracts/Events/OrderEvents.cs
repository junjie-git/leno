using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;

namespace Leno.SharedContracts.Events;

/// <summary>
/// 订单创建集成事件，订单域发布。
/// 消费方：购物车域（清空已结算项）、促销域（锁定优惠券）、积分与会员域（冻结积分）、消息通知域（下单成功通知）、MQ 延迟消息（30 分钟超时取消）。
/// 同时实现 <see cref="IDomainEvent"/> 以便订单域经发件箱模式在同一事务内持久化。
/// 事件契约定义在共享层，变更需所有消费方协商。
/// </summary>
public sealed class OrderCreatedEvent : IntegrationEventBase, IDomainEvent
{
    /// <summary>订单标识。</summary>
    public Guid OrderId { get; init; }

    /// <summary>买家账号标识（用户域 UserId）。</summary>
    public Guid BuyerId { get; init; }

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
        decimal totalAmount,
        string currency,
        DateTime createdAt,
        IReadOnlyList<Guid> sourceCartItemIds) : base()
    {
        OrderId = orderId;
        BuyerId = buyerId;
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

    /// <summary>卖家（店铺）标识，语义等同卖家与店铺管理域的 ShopId。</summary>
    public Guid SellerId { get; init; }

    /// <summary>订单总金额。</summary>
    public decimal TotalAmount { get; init; }

    /// <summary>币种（ISO 4217）。</summary>
    public string Currency { get; init; } = "CNY";

    /// <summary>完成时间（UTC）。</summary>
    public DateTime CompletedAt { get; init; }

    /// <summary>供 System.Text.Json 反序列化使用的无参构造。</summary>
    public OrderCompletedEvent() : base()
    {
    }

    public OrderCompletedEvent(Guid orderId, Guid sellerId, decimal totalAmount, string currency, DateTime completedAt)
        : base()
    {
        OrderId = orderId;
        SellerId = sellerId;
        TotalAmount = totalAmount;
        Currency = string.IsNullOrWhiteSpace(currency) ? "CNY" : currency;
        CompletedAt = completedAt;
    }
}
