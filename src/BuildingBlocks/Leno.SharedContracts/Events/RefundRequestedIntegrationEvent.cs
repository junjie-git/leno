using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;

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

    public RefundRequestedIntegrationEvent() : base() { }

    public RefundRequestedIntegrationEvent(
        Guid refundId, Guid orderId, Guid userId, Guid afterSalesId,
        Guid paymentId, decimal refundAmount, string currency, string channel) : base()
    {
        RefundId = refundId;
        OrderId = orderId;
        UserId = userId;
        AfterSalesId = afterSalesId;
        PaymentId = paymentId;
        RefundAmount = refundAmount;
        Currency = string.IsNullOrWhiteSpace(currency) ? "CNY" : currency;
        Channel = channel ?? string.Empty;
    }
}
