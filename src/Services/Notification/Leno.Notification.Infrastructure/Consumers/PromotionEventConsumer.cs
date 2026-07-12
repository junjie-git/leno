using System.Globalization;
using Leno.Notification.Domain.Services;
using Leno.Notification.Domain.ValueObjects;
using Leno.Promotion.Domain.Events;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Leno.Notification.Infrastructure.Consumers;

/// <summary>
/// 促销事件消费者，消费秒杀成功事件发送通知。
/// </summary>
public sealed class PromotionEventConsumer : IConsumer<SeckillOrderCreatedEvent>
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<PromotionEventConsumer> _logger;

    public PromotionEventConsumer(INotificationService notificationService, ILogger<PromotionEventConsumer> logger)
    {
        ArgumentNullException.ThrowIfNull(notificationService);
        ArgumentNullException.ThrowIfNull(logger);
        _notificationService = notificationService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<SeckillOrderCreatedEvent> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var evt = context.Message;
        _logger.LogInformation("消费秒杀成功事件 EventId={EventId} UserId={UserId} OrderId={OrderId}", evt.EventId, evt.UserId, evt.OrderId);

        var request = new NotificationRequest
        {
            TemplateCode = EventTemplateMapping.GetTemplateCode(nameof(SeckillOrderCreatedEvent))!,
            UserId = evt.UserId,
            IdempotencyKey = evt.EventId.ToString(),
            Variables = new Dictionary<string, string>
            {
                ["orderId"] = evt.OrderId.ToString(),
                ["spuId"] = evt.SpuId.ToString(),
                ["skuId"] = evt.SkuId.ToString(),
                ["seckillPrice"] = evt.SeckillPrice.ToString("F2", CultureInfo.InvariantCulture),
                ["quantity"] = evt.Quantity.ToString(CultureInfo.InvariantCulture),
                ["currency"] = evt.Currency
            }
        };

        await _notificationService.SendAsync(request, context.CancellationToken);
    }
}