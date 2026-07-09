using Leno.SharedContracts.Events;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Leno.Infrastructure.EventBus;

/// <summary>
/// 集成事件消费者基类，基于 MassTransit <see cref="IConsumer{T}"/>。
/// 通过 EventId 做幂等去重：处理前检查是否已消费，处理后标记已处理。
/// 消费失败抛出异常，由 MassTransit 重试策略处理，重试耗尽进入死信队列。
/// </summary>
/// <typeparam name="T">集成事件类型。</typeparam>
public abstract class IntegrationEventConsumerBase<T> : IConsumer<T>
    where T : class, IIntegrationEvent
{
    /// <summary>日志记录器。</summary>
    protected ILogger Logger { get; }

    protected IntegrationEventConsumerBase(ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        Logger = logger;
    }

    public async Task Consume(ConsumeContext<T> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var evt = context.Message;

        if (await IsProcessedAsync(evt.EventId, context.CancellationToken))
        {
            Logger.LogInformation("事件已处理，跳过重复消费 EventId={EventId} Type={EventType}",
                evt.EventId, typeof(T).Name);
            return;
        }

        Logger.LogInformation("开始消费集成事件 EventId={EventId} Type={EventType}",
            evt.EventId, typeof(T).Name);

        await HandleAsync(evt, context.CancellationToken);

        await MarkAsProcessedAsync(evt.EventId, context.CancellationToken);

        Logger.LogInformation("集成事件消费完成 EventId={EventId} Type={EventType}",
            evt.EventId, typeof(T).Name);
    }

    /// <summary>
    /// 子类实现具体业务处理逻辑。抛出异常将触发 MassTransit 重试与死信队列。
    /// 实现须保证幂等：同一事件重复处理不产生副作用。
    /// </summary>
    protected abstract Task HandleAsync(T integrationEvent, CancellationToken ct);

    /// <summary>
    /// 判断事件是否已处理（幂等去重）。默认返回 false，子类可基于 Redis 等存储覆盖。
    /// </summary>
    protected virtual Task<bool> IsProcessedAsync(Guid eventId, CancellationToken ct)
    {
        _ = eventId;
        _ = ct;
        return Task.FromResult(false);
    }

    /// <summary>
    /// 标记事件已处理。默认无操作，子类可基于 Redis 等存储覆盖。
    /// </summary>
    protected virtual Task MarkAsProcessedAsync(Guid eventId, CancellationToken ct)
    {
        _ = eventId;
        _ = ct;
        return Task.CompletedTask;
    }
}
