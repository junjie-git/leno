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

        // 前置校验：EventId 不能为 Guid.Empty，否则幂等去重失效
        if (evt.EventId == Guid.Empty)
        {
            Logger.LogWarning("集成事件 EventId 为 Guid.Empty，拒绝消费 Type={EventType}", typeof(T).Name);
            throw new InvalidOperationException(
                $"集成事件 {typeof(T).Name} 的 EventId 为 Guid.Empty，无法保证幂等性");
        }

        // IdempotencyKey 为空时回退到 EventId（向后兼容旧版事件 JSON 缺字段场景）
        var effectiveKey = string.IsNullOrEmpty(evt.IdempotencyKey)
            ? evt.EventId.ToString()
            : evt.IdempotencyKey;
        Logger.LogDebug("消费集成事件 EventId={EventId} IdempotencyKey={Key} Type={EventType}",
            evt.EventId, effectiveKey, typeof(T).Name);

        // 快速预检查：已处理则跳过（兼容所有 store 实现，包括不支持原子操作的 Mock）
        if (await IsProcessedAsync(evt.EventId, context.CancellationToken))
        {
            Logger.LogInformation("事件已处理，跳过重复消费 EventId={EventId} IdempotencyKey={Key} Type={EventType}",
                evt.EventId, effectiveKey, typeof(T).Name);
            return;
        }

        // 原子获取处理权：若 store 支持（SupportsAtomicProcessing），用 SET NX 原子标记"处理中"，
        // 保证只有一个消费者获取处理权，消除并发穿透。
        // 不支持原子操作的 store（如 Mock）走旧逻辑（已通过上方 IsProcessedAsync 预检查）。
        var acquired = await TryAcquireProcessingLockAsync(evt.EventId, context.CancellationToken);
        if (!acquired)
        {
            Logger.LogInformation("事件被其他消费者占用或已处理，跳过 EventId={EventId} IdempotencyKey={Key} Type={EventType}",
                evt.EventId, effectiveKey, typeof(T).Name);
            return;
        }

        Logger.LogInformation("开始消费集成事件 EventId={EventId} IdempotencyKey={Key} Type={EventType}",
            evt.EventId, effectiveKey, typeof(T).Name);

        try
        {
            await HandleAsync(evt, context.CancellationToken);
        }
        catch
        {
            // 处理失败：释放处理锁，允许后续重试
            await ReleaseProcessingLockAsync(evt.EventId, context.CancellationToken);
            throw;
        }

        await MarkAsProcessedAsync(evt.EventId, context.CancellationToken);

        Logger.LogInformation("集成事件消费完成 EventId={EventId} IdempotencyKey={Key} Type={EventType}",
            evt.EventId, effectiveKey, typeof(T).Name);
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
    /// 原子地尝试获取事件处理权。
    /// 若 <see cref="IIdempotencyStore.SupportsAtomicProcessing"/> 为 true，委托给
    /// <see cref="IIdempotencyStore.TryMarkAsProcessingAsync"/>（SET NX 原子操作）。
    /// 否则返回 true（向后兼容，已通过 <see cref="IsProcessedAsync"/> 预检查）。
    /// </summary>
    protected virtual Task<bool> TryAcquireProcessingLockAsync(Guid eventId, CancellationToken ct)
    {
        if (IdempotencyStore.SupportsAtomicProcessing)
        {
            return IdempotencyStore.TryMarkAsProcessingAsync(eventId, ct);
        }
        return Task.FromResult(true);
    }

    /// <summary>
    /// 释放处理锁（处理失败时调用，允许后续重试）。
    /// 仅在 <see cref="IIdempotencyStore.SupportsAtomicProcessing"/> 为 true 时有实际操作。
    /// </summary>
    protected virtual Task ReleaseProcessingLockAsync(Guid eventId, CancellationToken ct)
    {
        if (IdempotencyStore.SupportsAtomicProcessing)
        {
            return IdempotencyStore.ReleaseProcessingLockAsync(eventId, ct);
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// 标记事件已处理。默认委托给 <see cref="IIdempotencyStore"/>。
    /// 子类如需自定义幂等策略可重写，但 MUST 调用基类实现或同等保证。
    /// </summary>
    protected virtual Task MarkAsProcessedAsync(Guid eventId, CancellationToken ct)
        => IdempotencyStore.MarkAsProcessedAsync(eventId, ct);
}
