namespace Leno.Infrastructure.Abstractions;

/// <summary>
/// 幂等去重存储抽象，用于集成事件消费幂等性保证。
/// 默认实现 <c>RedisIdempotencyStore</c> 基于 Redis SET NX + 24h TTL。
/// 消费前调用 <see cref="IsProcessedAsync"/> 检查是否已处理；处理成功后调用 <see cref="MarkAsProcessedAsync"/> 标记。
/// </summary>
public interface IIdempotencyStore
{
    /// <summary>
    /// 判断指定事件是否已处理。
    /// 返回 true 表示已处理，消费方应直接跳过业务逻辑。
    /// </summary>
    /// <param name="eventId">集成事件唯一标识。</param>
    /// <param name="ct">取消令牌。</param>
    Task<bool> IsProcessedAsync(Guid eventId, CancellationToken ct = default);

    /// <summary>
    /// 标记事件已处理。实现应使用 SET NX 保证原子性，TTL 默认 24 小时。
    /// 重复标记同一 EventId 应为幂等无副作用操作。
    /// </summary>
    /// <param name="eventId">集成事件唯一标识。</param>
    /// <param name="ct">取消令牌。</param>
    Task MarkAsProcessedAsync(Guid eventId, CancellationToken ct = default);

    /// <summary>
    /// 指示此实现是否支持原子处理权获取（<see cref="TryMarkAsProcessingAsync"/>）。
    /// 默认为 false（向后兼容，走 IsProcessedAsync → HandleAsync → MarkAsProcessedAsync 三步流程）。
    /// <see cref="RedisIdempotencyStore"/> 覆盖为 true，使用 SET NX 原子获取处理权，消除并发穿透。
    /// </summary>
    bool SupportsAtomicProcessing => false;

    /// <summary>
    /// 原子地尝试将事件标记为"处理中"。
    /// 使用 Redis SET NX 原子操作，返回 true 表示获取到处理权（当前消费者应执行 HandleAsync），
    /// 返回 false 表示已有其他消费者正在处理或已处理完成（当前消费者应跳过）。
    /// <para>默认实现返回 true（允许处理），仅在 <see cref="SupportsAtomicProcessing"/> 为 true 时被调用。</para>
    /// </summary>
    /// <param name="eventId">事件唯一标识。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>true=获取处理权，false=已被其他消费者占用。</returns>
    Task<bool> TryMarkAsProcessingAsync(Guid eventId, CancellationToken ct = default)
        => Task.FromResult(true);

    /// <summary>
    /// 释放处理锁（处理失败时调用，允许后续重试）。
    /// <para>默认实现为空操作，仅在 <see cref="SupportsAtomicProcessing"/> 为 true 时被调用。</para>
    /// </summary>
    /// <param name="eventId">事件唯一标识。</param>
    /// <param name="ct">取消令牌。</param>
    Task ReleaseProcessingLockAsync(Guid eventId, CancellationToken ct = default)
        => Task.CompletedTask;
}
