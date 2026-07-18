using Leno.SharedContracts.Events;

namespace Leno.SharedContracts.Events;

public sealed class RefundRequestedIntegrationEvent : IntegrationEventBase
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

    /// <summary>聚合根标识，用于发件箱归类，映射至售后单标识。</summary>
    public Guid AggregateId => AfterSalesId;

    public RefundRequestedIntegrationEvent() : base() { }

    public RefundRequestedIntegrationEvent(
        Guid refundId, Guid orderId, Guid userId, Guid afterSalesId,
        Guid paymentId, decimal refundAmount, string currency, string channel,
        string refundReason = "") : base()
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
