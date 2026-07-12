using System.Globalization;
using Leno.Notification.Domain.Services;
using Leno.Notification.Domain.ValueObjects;
using Leno.SharedContracts.Events;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Leno.Notification.Infrastructure.Consumers;

/// <summary>
/// 支付事件消费者，消费支付成功/失败事件发送对应通知。
/// </summary>
public sealed class PaymentEventConsumer :
    IConsumer<PaymentSucceededEvent>,
    IConsumer<PaymentFailedEvent>
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<PaymentEventConsumer> _logger;

    public PaymentEventConsumer(INotificationService notificationService, ILogger<PaymentEventConsumer> logger)
    {
        ArgumentNullException.ThrowIfNull(notificationService);
        ArgumentNullException.ThrowIfNull(logger);
        _notificationService = notificationService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<PaymentSucceededEvent> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var evt = context.Message;
        _logger.LogInformation("消费支付成功事件 EventId={EventId} OrderId={OrderId}", evt.EventId, evt.OrderId);

        var request = new NotificationRequest
        {
            TemplateCode = EventTemplateMapping.GetTemplateCode(nameof(PaymentSucceededEvent))!,
            UserId = evt.UserId,
            IdempotencyKey = evt.EventId.ToString(),
            Variables = new Dictionary<string, string>
            {
                ["orderId"] = evt.OrderId.ToString(),
                ["amount"] = evt.Amount.ToString("F2", CultureInfo.InvariantCulture),
                ["currency"] = evt.Currency,
                ["tradeNo"] = evt.TradeNo,
                ["channel"] = evt.Channel
            }
        };

        await _notificationService.SendAsync(request, context.CancellationToken);
    }

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<PaymentFailedEvent> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var evt = context.Message;
        _logger.LogInformation("消费支付失败事件 EventId={EventId} OrderId={OrderId}", evt.EventId, evt.OrderId);

        var request = new NotificationRequest
        {
            TemplateCode = EventTemplateMapping.GetTemplateCode(nameof(PaymentFailedEvent))!,
            UserId = evt.UserId,
            IdempotencyKey = evt.EventId.ToString(),
            Variables = new Dictionary<string, string>
            {
                ["orderId"] = evt.OrderId.ToString(),
                ["reason"] = evt.Reason,
                ["failedAt"] = evt.FailedAt.ToString("u", CultureInfo.InvariantCulture)
            }
        };

        await _notificationService.SendAsync(request, context.CancellationToken);
    }
}