using System.Globalization;
using Leno.Notification.Domain.Services;
using Leno.Notification.Domain.ValueObjects;
using Leno.PointsMembership.Domain.Events;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Leno.Notification.Infrastructure.Consumers;

/// <summary>
/// 积分与会员事件消费者，消费积分到账、会员升级、会员激活事件发送通知。
/// </summary>
public sealed class PointsEventConsumer :
    IConsumer<PointsEarnedEvent>,
    IConsumer<MemberLevelUpgradedEvent>,
    IConsumer<MembershipActivatedEvent>
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<PointsEventConsumer> _logger;

    public PointsEventConsumer(INotificationService notificationService, ILogger<PointsEventConsumer> logger)
    {
        ArgumentNullException.ThrowIfNull(notificationService);
        ArgumentNullException.ThrowIfNull(logger);
        _notificationService = notificationService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<PointsEarnedEvent> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var evt = context.Message;
        _logger.LogInformation("消费积分到账事件 EventId={EventId} UserId={UserId} Amount={Amount}", evt.EventId, evt.UserId, evt.Amount);

        var request = new NotificationRequest
        {
            TemplateCode = EventTemplateMapping.GetTemplateCode(nameof(PointsEarnedEvent))!,
            UserId = evt.UserId,
            IdempotencyKey = evt.EventId.ToString(),
            Variables = new Dictionary<string, string>
            {
                ["amount"] = evt.Amount.ToString(CultureInfo.InvariantCulture),
                ["source"] = evt.Source
            }
        };

        await _notificationService.SendAsync(request, context.CancellationToken);
    }

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<MemberLevelUpgradedEvent> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var evt = context.Message;
        _logger.LogInformation("消费会员升级事件 EventId={EventId} UserId={UserId} OldLevel={OldLevel} NewLevel={NewLevel}", evt.EventId, evt.UserId, evt.OldLevel, evt.NewLevel);

        var request = new NotificationRequest
        {
            TemplateCode = EventTemplateMapping.GetTemplateCode(nameof(MemberLevelUpgradedEvent))!,
            UserId = evt.UserId,
            IdempotencyKey = evt.EventId.ToString(),
            Variables = new Dictionary<string, string>
            {
                ["oldLevel"] = evt.OldLevel.ToString(CultureInfo.InvariantCulture),
                ["newLevel"] = evt.NewLevel.ToString(CultureInfo.InvariantCulture)
            }
        };

        await _notificationService.SendAsync(request, context.CancellationToken);
    }

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<MembershipActivatedEvent> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var evt = context.Message;
        _logger.LogInformation("消费会员激活事件 EventId={EventId} UserId={UserId} Level={Level}", evt.EventId, evt.UserId, evt.Level);

        var request = new NotificationRequest
        {
            TemplateCode = EventTemplateMapping.GetTemplateCode(nameof(MembershipActivatedEvent))!,
            UserId = evt.UserId,
            IdempotencyKey = evt.EventId.ToString(),
            Variables = new Dictionary<string, string>
            {
                ["level"] = evt.Level.ToString(CultureInfo.InvariantCulture),
                ["endTime"] = evt.EndTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                ["packageId"] = evt.PackageId.ToString()
            }
        };

        await _notificationService.SendAsync(request, context.CancellationToken);
    }
}