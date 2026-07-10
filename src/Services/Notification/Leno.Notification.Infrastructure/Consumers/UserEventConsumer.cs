using Leno.Notification.Infrastructure.Services;
using Leno.SharedContracts.Events;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Leno.Notification.Infrastructure.Consumers;

/// <summary>
/// 用户事件消费者，消费用户注册事件发送欢迎通知。
/// </summary>
public sealed class UserEventConsumer : IConsumer<UserRegisteredEvent>
{
    private readonly INotificationDispatcher _dispatcher;
    private readonly ILogger<UserEventConsumer> _logger;

    public UserEventConsumer(INotificationDispatcher dispatcher, ILogger<UserEventConsumer> logger)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(logger);
        _dispatcher = dispatcher;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<UserRegisteredEvent> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var evt = context.Message;

        _logger.LogInformation("消费用户注册事件 EventId={EventId} UserId={UserId}", evt.EventId, evt.UserId);

        var variables = new Dictionary<string, string>
        {
            ["username"] = evt.Username,
            ["email"] = evt.Email ?? string.Empty,
            ["phoneNumber"] = evt.PhoneNumber ?? string.Empty
        };

        await _dispatcher.DispatchAsync(evt.UserId, "UserRegisteredEvent", evt.EventId, variables, context.CancellationToken);
    }
}
