using System.Globalization;
using Leno.Notification.Infrastructure.Services;
using Leno.Promotion.Domain.Events;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Leno.Notification.Infrastructure.Consumers;

/// <summary>
/// 促销事件消费者，消费秒杀成功事件发送通知。
/// </summary>
public sealed class PromotionEventConsumer : IConsumer<SeckillOrderCreatedEvent>
{
    private readonly INotificationDispatcher _dispatcher;
    private readonly ILogger<PromotionEventConsumer> _logger;

    public PromotionEventConsumer(INotificationDispatcher dispatcher, ILogger<PromotionEventConsumer> logger)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(logger);
        _dispatcher = dispatcher;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<SeckillOrderCreatedEvent> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var evt = context.Message;
        _logger.LogInformation("消费秒杀成功事件 EventId={EventId} UserId={UserId} OrderId={OrderId}", evt.EventId, evt.UserId, evt.OrderId);

        var variables = new Dictionary<string, string>
        {
            ["orderId"] = evt.OrderId.ToString(),
            ["spuId"] = evt.SpuId.ToString(),
            ["skuId"] = evt.SkuId.ToString(),
            ["seckillPrice"] = evt.SeckillPrice.ToString("F2", CultureInfo.InvariantCulture),
            ["quantity"] = evt.Quantity.ToString(CultureInfo.InvariantCulture),
            ["currency"] = evt.Currency
        };

        await _dispatcher.DispatchAsync(evt.UserId, "SeckillOrderCreatedEvent", evt.EventId, variables, context.CancellationToken);
    }
}
