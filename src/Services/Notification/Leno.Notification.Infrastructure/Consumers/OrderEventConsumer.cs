using System.Globalization;
using Leno.Notification.Domain.Services;
using Leno.Notification.Domain.ValueObjects;
using Leno.SharedContracts.Events;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Leno.Notification.Infrastructure.Consumers;

/// <summary>
/// 订单事件消费者，消费下单成功、发货、确认收货事件发送对应通知。
/// </summary>
public sealed class OrderEventConsumer :
    IConsumer<OrderCreatedEvent>,
    IConsumer<OrderShippedEvent>,
    IConsumer<OrderCompletedEvent>
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<OrderEventConsumer> _logger;

    public OrderEventConsumer(INotificationService notificationService, ILogger<OrderEventConsumer> logger)
    {
        ArgumentNullException.ThrowIfNull(notificationService);
        ArgumentNullException.ThrowIfNull(logger);
        _notificationService = notificationService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<OrderCreatedEvent> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var evt = context.Message;
        _logger.LogInformation("消费订单创建事件 EventId={EventId} OrderId={OrderId}", evt.EventId, evt.OrderId);

        var request = new NotificationRequest
        {
            TemplateCode = EventTemplateMapping.GetTemplateCode(nameof(OrderCreatedEvent))!,
            UserId = evt.BuyerId,
            IdempotencyKey = evt.EventId.ToString(),
            Variables = new Dictionary<string, string>
            {
                ["orderId"] = evt.OrderId.ToString(),
                ["totalAmount"] = evt.TotalAmount.ToString("F2", CultureInfo.InvariantCulture),
                ["currency"] = evt.Currency
            }
        };

        await _notificationService.SendAsync(request, context.CancellationToken);
    }

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<OrderShippedEvent> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var evt = context.Message;
        _logger.LogInformation("消费订单发货事件 EventId={EventId} OrderId={OrderId}", evt.EventId, evt.OrderId);

        var request = new NotificationRequest
        {
            TemplateCode = EventTemplateMapping.GetTemplateCode(nameof(OrderShippedEvent))!,
            UserId = evt.UserId,
            IdempotencyKey = evt.EventId.ToString(),
            Variables = new Dictionary<string, string>
            {
                ["orderId"] = evt.OrderId.ToString(),
                ["logisticsNo"] = evt.LogisticsNo
            }
        };

        await _notificationService.SendAsync(request, context.CancellationToken);
    }

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<OrderCompletedEvent> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var evt = context.Message;
        _logger.LogInformation("消费订单完成事件 EventId={EventId} OrderId={OrderId}", evt.EventId, evt.OrderId);

        var request = new NotificationRequest
        {
            TemplateCode = EventTemplateMapping.GetTemplateCode(nameof(OrderCompletedEvent))!,
            UserId = evt.UserId,
            IdempotencyKey = evt.EventId.ToString(),
            Variables = new Dictionary<string, string>
            {
                ["orderId"] = evt.OrderId.ToString(),
                ["totalAmount"] = evt.TotalAmount.ToString("F2", CultureInfo.InvariantCulture),
                ["currency"] = evt.Currency
            }
        };

        await _notificationService.SendAsync(request, context.CancellationToken);
    }
}