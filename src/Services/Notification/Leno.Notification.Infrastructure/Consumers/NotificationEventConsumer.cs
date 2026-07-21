using System.Globalization;
using Leno.Notification.Domain.Services;
using Leno.Notification.Domain.ValueObjects;
using Leno.SharedContracts.Events;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Leno.Notification.Infrastructure.Consumers;

/// <summary>
/// 统一通知事件消费者，处理所有 12 种集成事件，将其映射为通知发送请求。
/// await 通知发送完成，异常冒泡到 MassTransit 触发重试；变量补全失败或缺少映射时不阻塞队列。
/// </summary>
/// <remarks>
/// [Obsolete] 该消费者已从 DI 注册中移除（P0-3 修复），不应再注册或使用。
/// 各 BC 已有专用 Consumer（OrderEventConsumer/UserEventConsumer 等）处理对应事件，
/// 同时注册本类会导致每条集成事件被消费两次（重复订阅）。
/// 保留类本身仅为兼容既有测试，新代码请使用专用 Consumer。
/// </remarks>
[Obsolete("已从 AddNotificationConsumers 中移除注册，使用各 BC 专用 Consumer 替代。详见 P0-3 修复。")]
public sealed class NotificationEventConsumer :
    IConsumer<OrderCreatedEvent>,
    IConsumer<OrderShippedEvent>,
    IConsumer<OrderCompletedEvent>,
    IConsumer<PaymentSucceededEvent>,
    IConsumer<PaymentFailedEvent>,
    IConsumer<AfterSalesApprovedEvent>,
    IConsumer<RefundCompletedEvent>,
    IConsumer<SeckillOrderCreatedIntegrationEvent>,
    IConsumer<PointsEarnedIntegrationEvent>,
    IConsumer<MemberLevelChangedIntegrationEvent>,
    IConsumer<PaidMemberSubscribedIntegrationEvent>,
    IConsumer<UserRegisteredEvent>
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<NotificationEventConsumer> _logger;

    public NotificationEventConsumer(INotificationService notificationService, ILogger<NotificationEventConsumer> logger)
    {
        ArgumentNullException.ThrowIfNull(notificationService);
        ArgumentNullException.ThrowIfNull(logger);
        _notificationService = notificationService;
        _logger = logger;
    }

    public Task Consume(ConsumeContext<OrderCreatedEvent> context) =>
        HandleAsync(context.Message, nameof(OrderCreatedEvent), evt => evt.BuyerId, evt => new Dictionary<string, string>
        {
            ["orderId"] = evt.OrderId.ToString(),
            ["totalAmount"] = evt.TotalAmount.ToString("F2", CultureInfo.InvariantCulture),
            ["currency"] = evt.Currency
        });

    public Task Consume(ConsumeContext<OrderShippedEvent> context) =>
        HandleAsync(context.Message, nameof(OrderShippedEvent), evt => evt.UserId, evt => new Dictionary<string, string>
        {
            ["orderId"] = evt.OrderId.ToString(),
            ["logisticsNo"] = evt.LogisticsNo
        });

    public Task Consume(ConsumeContext<OrderCompletedEvent> context) =>
        HandleAsync(context.Message, nameof(OrderCompletedEvent), evt => evt.UserId, evt => new Dictionary<string, string>
        {
            ["orderId"] = evt.OrderId.ToString(),
            ["totalAmount"] = evt.TotalAmount.ToString("F2", CultureInfo.InvariantCulture),
            ["currency"] = evt.Currency
        });

    public Task Consume(ConsumeContext<PaymentSucceededEvent> context) =>
        HandleAsync(context.Message, nameof(PaymentSucceededEvent), evt => evt.UserId, evt => new Dictionary<string, string>
        {
            ["orderId"] = evt.OrderId.ToString(),
            ["amount"] = evt.Amount.ToString("F2", CultureInfo.InvariantCulture),
            ["currency"] = evt.Currency,
            ["tradeNo"] = evt.TradeNo,
            ["channel"] = evt.Channel
        });

    public Task Consume(ConsumeContext<PaymentFailedEvent> context) =>
        HandleAsync(context.Message, nameof(PaymentFailedEvent), evt => evt.UserId, evt => new Dictionary<string, string>
        {
            ["orderId"] = evt.OrderId.ToString(),
            ["reason"] = evt.Reason,
            ["failedAt"] = evt.FailedAt.ToString("u", CultureInfo.InvariantCulture)
        });

    public Task Consume(ConsumeContext<AfterSalesApprovedEvent> context) =>
        HandleAsync(context.Message, nameof(AfterSalesApprovedEvent), evt => evt.UserId, evt => new Dictionary<string, string>
        {
            ["afterSalesId"] = evt.AfterSalesId.ToString(),
            ["orderId"] = evt.OrderId.ToString(),
            ["approvedAmount"] = evt.ApprovedAmount.ToString("F2", CultureInfo.InvariantCulture),
            ["currency"] = evt.Currency
        });

    public Task Consume(ConsumeContext<RefundCompletedEvent> context) =>
        HandleAsync(context.Message, nameof(RefundCompletedEvent), evt => evt.UserId, evt => new Dictionary<string, string>
        {
            ["orderId"] = evt.OrderId.ToString(),
            ["refundId"] = evt.RefundId.ToString(),
            ["refundAmount"] = evt.RefundAmount.ToString("F2", CultureInfo.InvariantCulture),
            ["currency"] = evt.Currency
        });

    public Task Consume(ConsumeContext<SeckillOrderCreatedIntegrationEvent> context) =>
        HandleAsync(context.Message, nameof(SeckillOrderCreatedIntegrationEvent), evt => evt.UserId, evt => new Dictionary<string, string>
        {
            ["orderId"] = evt.OrderId.ToString(),
            ["spuId"] = evt.SpuId.ToString(),
            ["skuId"] = evt.SkuId.ToString(),
            ["seckillPrice"] = evt.SeckillPrice.ToString("F2", CultureInfo.InvariantCulture),
            ["quantity"] = evt.Quantity.ToString(CultureInfo.InvariantCulture),
            ["currency"] = evt.Currency
        });

    public Task Consume(ConsumeContext<PointsEarnedIntegrationEvent> context) =>
        HandleAsync(context.Message, nameof(PointsEarnedIntegrationEvent), evt => evt.UserId, evt => new Dictionary<string, string>
        {
            ["amount"] = evt.Amount.ToString(CultureInfo.InvariantCulture),
            ["source"] = evt.Source
        });

    public Task Consume(ConsumeContext<MemberLevelChangedIntegrationEvent> context) =>
        HandleAsync(context.Message, nameof(MemberLevelChangedIntegrationEvent), evt => evt.UserId, evt => new Dictionary<string, string>
        {
            ["oldLevel"] = evt.OldLevel.ToString(CultureInfo.InvariantCulture),
            ["newLevel"] = evt.NewLevel.ToString(CultureInfo.InvariantCulture)
        });

    public Task Consume(ConsumeContext<PaidMemberSubscribedIntegrationEvent> context) =>
        HandleAsync(context.Message, nameof(PaidMemberSubscribedIntegrationEvent), evt => evt.UserId, evt => new Dictionary<string, string>
        {
            ["level"] = evt.Level.ToString(CultureInfo.InvariantCulture),
            ["endTime"] = evt.EndTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["packageId"] = evt.PackageId.ToString()
        });

    public Task Consume(ConsumeContext<UserRegisteredEvent> context) =>
        HandleAsync(context.Message, nameof(UserRegisteredEvent), evt => evt.UserId, evt => new Dictionary<string, string>
        {
            ["username"] = evt.Username,
            ["email"] = evt.Email ?? string.Empty,
            ["phoneNumber"] = evt.PhoneNumber ?? string.Empty
        });

    /// <summary>
    /// 通用事件处理：映射事件类型到模板编码，构建变量，await 调用通知服务。
    /// 缺少模板映射或变量补全失败时仅记录警告，不阻塞消息队列（这些是配置/数据问题，重试无意义）。
    /// 通知发送失败时异常冒泡到 MassTransit，由重试策略 + 死信队列处理。
    /// </summary>
    private async Task HandleAsync<TEvent>(
        TEvent evt,
        string eventType,
        Func<TEvent, Guid> getUserId,
        Func<TEvent, Dictionary<string, string>> buildVariables)
        where TEvent : IntegrationEventBase
    {
        _logger.LogInformation("消费事件 EventType={EventType} EventId={EventId}", eventType, evt.EventId);

        var templateCode = EventTemplateMapping.GetTemplateCode(eventType);
        if (templateCode is null)
        {
            _logger.LogWarning("事件类型 {EventType} 未找到模板映射，跳过通知发送 EventId={EventId}", eventType, evt.EventId);
            return;
        }

        Dictionary<string, string> variables;
        try
        {
            variables = buildVariables(evt);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "事件 {EventType} 变量补全失败，跳过通知发送 EventId={EventId}", eventType, evt.EventId);
            return;
        }

        var request = new NotificationRequest
        {
            TemplateCode = templateCode,
            UserId = getUserId(evt),
            Variables = variables,
            IdempotencyKey = evt.EventId.ToString(),
            BusinessRef = string.Empty
        };

        // await SendAsync: 异常冒泡到 MassTransit 重试；IdempotencyKey 已设置，重试不会重复发送
        await SendAsync(request, eventType, evt.EventId).ConfigureAwait(false);
    }

    /// <summary>
    /// 调用通知服务发送通知。异常冒泡到 MassTransit，由重试策略 + 死信队列处理。
    /// IdempotencyKey 已在调用前设置为 EventId，重试不会重复发送。
    /// </summary>
    private async Task SendAsync(NotificationRequest request, string eventType, Guid eventId)
    {
        await _notificationService.SendAsync(request).ConfigureAwait(false);
        _logger.LogInformation("通知发送成功 EventType={EventType} EventId={EventId} TemplateCode={TemplateCode}",
            eventType, eventId, request.TemplateCode);
    }
}
