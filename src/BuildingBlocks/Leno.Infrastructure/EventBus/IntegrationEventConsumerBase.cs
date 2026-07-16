using Leno.Infrastructure.Abstractions;
using Leno.SharedContracts.Events;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Leno.Infrastructure.EventBus;

/// <summary>
/// 集成事件消费者基类，基于 MassTransit <see cref="IConsumer{T}"/>。
/// 通过 EventId + <see cref="IIdempotencyStore"/> 做幂等去重：处理前检查是否已消费，处理后标记已处理。
/// 消费失败抛出异常，由 MassTransit 重试策略处理，重试耗尽进入死信队列。
/// 子类 MUST 注入 <see cref="IIdempotencyStore"/>（默认 <see cref="RedisIdempotencyStore"/>），
/// 强制保证幂等去重，避免 Outbox 重复发布或重试导致业务副作用。
/// </summary>
/// <typeparam name="T">集成事件类型。</typeparam>
public abstract class IntegrationEventConsumerBase<T> : IConsumer<T>
    where T : class, IIntegrationEvent
{
    /// <summary>日志记录器。</summary>
    protected ILogger Logger { get; }

    /// <summary>幂等去重存储，子类 MUST 通过构造函数注入。</summary>
    protected IIdempotencyStore IdempotencyStore { get; }

    protected IntegrationEventConsumerBase(ILogger logger, IIdempotencyStore idempotencyStore)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(idempotencyStore);
        Logger = logger;
        IdempotencyStore = idempotencyStore;
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
    /// 实现须保证幂等：同一事件重复处理不产生副作用（与 <see cref="IIdempotencyStore"/> 共同保证最终幂等）。
    /// </summary>
    protected abstract Task HandleAsync(T integrationEvent, CancellationToken ct);

    /// <summary>
    /// 判断事件是否已处理（幂等去重）。默认委托给 <see cref="IIdempotencyStore"/>。
    /// 子类如需自定义幂等策略可重写，但 MUST 调用基类实现或同等保证。
    /// </summary>
    protected virtual Task<bool> IsProcessedAsync(Guid eventId, CancellationToken ct)
        => IdempotencyStore.IsProcessedAsync(eventId, ct);

    /// <summary>
    /// 标记事件已处理。默认委托给 <see cref="IIdempotencyStore"/>。
    /// 子类如需自定义幂等策略可重写，但 MUST 调用基类实现或同等保证。
    /// </summary>
    protected virtual Task MarkAsProcessedAsync(Guid eventId, CancellationToken ct)
        => IdempotencyStore.MarkAsProcessedAsync(eventId, ct);
}
