using System.Globalization;
using Leno.Notification.Domain.Services;
using Leno.Notification.Domain.ValueObjects;
using Leno.SharedContracts.Events;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Leno.Notification.Infrastructure.Consumers;

/// <summary>
/// 售后事件消费者，消费售后审核通过与退款完成事件发送通知。
/// </summary>
public sealed class AfterSalesEventConsumer :
    IConsumer<AfterSalesApprovedEvent>,
    IConsumer<RefundCompletedEvent>
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<AfterSalesEventConsumer> _logger;

    public AfterSalesEventConsumer(INotificationService notificationService, ILogger<AfterSalesEventConsumer> logger)
    {
        ArgumentNullException.ThrowIfNull(notificationService);
        ArgumentNullException.ThrowIfNull(logger);
        _notificationService = notificationService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<AfterSalesApprovedEvent> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var evt = context.Message;
        _logger.LogInformation("消费售后审核通过事件 EventId={EventId} AfterSalesId={AfterSalesId}", evt.EventId, evt.AfterSalesId);

        var request = new NotificationRequest
        {
            TemplateCode = EventTemplateMapping.GetTemplateCode(nameof(AfterSalesApprovedEvent))!,
            UserId = evt.UserId,
            IdempotencyKey = evt.EventId.ToString(),
            Variables = new Dictionary<string, string>
            {
                ["afterSalesId"] = evt.AfterSalesId.ToString(),
                ["orderId"] = evt.OrderId.ToString(),
                ["approvedAmount"] = evt.ApprovedAmount.ToString("F2", CultureInfo.InvariantCulture),
                ["currency"] = evt.Currency
            }
        };

        await _notificationService.SendAsync(request, context.CancellationToken);
    }

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<RefundCompletedEvent> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var evt = context.Message;
        _logger.LogInformation("消费退款完成事件 EventId={EventId} OrderId={OrderId}", evt.EventId, evt.OrderId);

        var request = new NotificationRequest
        {
            TemplateCode = EventTemplateMapping.GetTemplateCode(nameof(RefundCompletedEvent))!,
            UserId = evt.UserId,
            IdempotencyKey = evt.EventId.ToString(),
            Variables = new Dictionary<string, string>
            {
                ["orderId"] = evt.OrderId.ToString(),
                ["refundId"] = evt.RefundId.ToString(),
                ["refundAmount"] = evt.RefundAmount.ToString("F2", CultureInfo.InvariantCulture),
                ["currency"] = evt.Currency
            }
        };

        await _notificationService.SendAsync(request, context.CancellationToken);
    }
}