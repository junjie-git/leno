using System.Globalization;
using Leno.Notification.Infrastructure.Services;
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
    private readonly INotificationDispatcher _dispatcher;
    private readonly ILogger<AfterSalesEventConsumer> _logger;

    public AfterSalesEventConsumer(INotificationDispatcher dispatcher, ILogger<AfterSalesEventConsumer> logger)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(logger);
        _dispatcher = dispatcher;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<AfterSalesApprovedEvent> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var evt = context.Message;
        _logger.LogInformation("消费售后审核通过事件 EventId={EventId} AfterSalesId={AfterSalesId}", evt.EventId, evt.AfterSalesId);

        var variables = new Dictionary<string, string>
        {
            ["afterSalesId"] = evt.AfterSalesId.ToString(),
            ["orderId"] = evt.OrderId.ToString(),
            ["approvedAmount"] = evt.ApprovedAmount.ToString("F2", CultureInfo.InvariantCulture),
            ["currency"] = evt.Currency
        };

        await _dispatcher.DispatchAsync(evt.UserId, "AfterSalesApprovedEvent", evt.EventId, variables, context.CancellationToken);
    }

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<RefundCompletedEvent> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var evt = context.Message;
        _logger.LogInformation("消费退款完成事件 EventId={EventId} OrderId={OrderId}", evt.EventId, evt.OrderId);

        var variables = new Dictionary<string, string>
        {
            ["orderId"] = evt.OrderId.ToString(),
            ["refundAmount"] = evt.RefundAmount.ToString("F2", CultureInfo.InvariantCulture),
            ["currency"] = evt.Currency
        };

        // RefundCompletedEvent 无 UserId 字段，以 AfterSalesId 关联（实际应通过防腐层查询）
        // 退款完成事件由售后域发布，售后域已知 UserId，但事件契约未携带
        // 此处以 Guid.Empty 占位，通知记录将无法关联用户
        _logger.LogWarning("退款完成事件缺少 UserId，跳过通知发送 EventId={EventId} OrderId={OrderId}", evt.EventId, evt.OrderId);
        await Task.CompletedTask;
    }
}
