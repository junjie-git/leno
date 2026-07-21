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
    IConsumer<OrderCompletedEvent>,
    IConsumer<OrderCancelledEvent>
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

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<OrderCancelledEvent> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var evt = context.Message;
        _logger.LogInformation("消费订单取消事件 EventId={EventId} OrderId={OrderId}", evt.EventId, evt.OrderId);

        // OrderCancelledEvent 契约当前未携带 BuyerId，使用事件中的 SellerId 作为通知接收人 fallback。
        // 若 SellerId 也为 Guid.Empty（如会员订阅订单），记录警告并跳过发送，
        // 避免 Guid.Empty 触发 NotificationRecord.Create 的 NOTIFICATION_USER_EMPTY 异常，
        // 该异常会导致 MassTransit 重试 3 次后死信，整条事件链路失效。
        if (evt.SellerId == Guid.Empty)
        {
            _logger.LogWarning(
                "订单取消事件缺少接收人标识 EventId={EventId} OrderId={OrderId}，跳过通知发送",
                evt.EventId, evt.OrderId);
            return;
        }

        var request = new NotificationRequest
        {
            TemplateCode = EventTemplateMapping.GetTemplateCode(nameof(OrderCancelledEvent))!,
            UserId = evt.SellerId,
            IdempotencyKey = evt.EventId.ToString(),
            Variables = new Dictionary<string, string>
            {
                ["orderId"] = evt.OrderId.ToString(),
                ["cancelReason"] = evt.CancelReason,
                ["cancelledBy"] = evt.CancelledBy
            }
        };

        await _notificationService.SendAsync(request, context.CancellationToken);
    }
}