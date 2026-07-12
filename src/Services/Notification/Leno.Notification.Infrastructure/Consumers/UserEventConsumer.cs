using Leno.Notification.Domain.Services;
using Leno.Notification.Domain.ValueObjects;
using Leno.SharedContracts.Events;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Leno.Notification.Infrastructure.Consumers;

/// <summary>
/// 用户事件消费者，消费用户注册事件发送欢迎通知。
/// </summary>
public sealed class UserEventConsumer : IConsumer<UserRegisteredEvent>
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<UserEventConsumer> _logger;

    public UserEventConsumer(INotificationService notificationService, ILogger<UserEventConsumer> logger)
    {
        ArgumentNullException.ThrowIfNull(notificationService);
        ArgumentNullException.ThrowIfNull(logger);
        _notificationService = notificationService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<UserRegisteredEvent> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var evt = context.Message;

        _logger.LogInformation("消费用户注册事件 EventId={EventId} UserId={UserId}", evt.EventId, evt.UserId);

        var request = new NotificationRequest
        {
            TemplateCode = EventTemplateMapping.GetTemplateCode(nameof(UserRegisteredEvent))!,
            UserId = evt.UserId,
            IdempotencyKey = evt.EventId.ToString(),
            Variables = new Dictionary<string, string>
            {
                ["username"] = evt.Username,
                ["email"] = evt.Email ?? string.Empty,
                ["phoneNumber"] = evt.PhoneNumber ?? string.Empty
            }
        };

        await _notificationService.SendAsync(request, context.CancellationToken);
    }
}