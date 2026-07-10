using System.Globalization;
using Leno.Notification.Infrastructure.Services;
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
    private readonly INotificationDispatcher _dispatcher;
    private readonly ILogger<OrderEventConsumer> _logger;

    public OrderEventConsumer(INotificationDispatcher dispatcher, ILogger<OrderEventConsumer> logger)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(logger);
        _dispatcher = dispatcher;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<OrderCreatedEvent> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var evt = context.Message;
        _logger.LogInformation("消费订单创建事件 EventId={EventId} OrderId={OrderId}", evt.EventId, evt.OrderId);

        var variables = new Dictionary<string, string>
        {
            ["orderId"] = evt.OrderId.ToString(),
            ["totalAmount"] = evt.TotalAmount.ToString("F2", CultureInfo.InvariantCulture),
            ["currency"] = evt.Currency
        };

        await _dispatcher.DispatchAsync(evt.BuyerId, "OrderCreatedEvent", evt.EventId, variables, context.CancellationToken);
    }

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<OrderShippedEvent> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var evt = context.Message;
        _logger.LogInformation("消费订单发货事件 EventId={EventId} OrderId={OrderId}", evt.EventId, evt.OrderId);

        var variables = new Dictionary<string, string>
        {
            ["orderId"] = evt.OrderId.ToString(),
            ["logisticsNo"] = evt.LogisticsNo
        };

        await _dispatcher.DispatchAsync(evt.UserId, "OrderShippedEvent", evt.EventId, variables, context.CancellationToken);
    }

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<OrderCompletedEvent> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var evt = context.Message;
        _logger.LogInformation("消费订单完成事件 EventId={EventId} OrderId={OrderId}", evt.EventId, evt.OrderId);

        var variables = new Dictionary<string, string>
        {
            ["orderId"] = evt.OrderId.ToString(),
            ["totalAmount"] = evt.TotalAmount.ToString("F2", CultureInfo.InvariantCulture),
            ["currency"] = evt.Currency
        };

        await _dispatcher.DispatchAsync(evt.UserId, "OrderCompletedEvent", evt.EventId, variables, context.CancellationToken);
    }
}
