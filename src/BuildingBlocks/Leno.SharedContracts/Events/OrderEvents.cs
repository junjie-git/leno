using Leno.SharedContracts.Events;

namespace Leno.SharedContracts.Events;

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
