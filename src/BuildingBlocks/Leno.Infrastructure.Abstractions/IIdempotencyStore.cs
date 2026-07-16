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
}
