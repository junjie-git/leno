namespace Leno.Infrastructure.Outbox;

/// <summary>
/// Outbox 分片策略抽象（4.4 Outbox 分片发布器）。
/// <para>
/// 按聚合根 ID 计算分片键，保证同一聚合根的集成事件始终落到同一分片，
/// 由同一发布器实例顺序发布，避免跨实例乱序。
/// </para>
/// <para>
/// 多实例部署时，每个实例仅负责一个或多个分片，配合
/// <c>SELECT ... WITH (UPDLOCK, ROWLOCK, READPAST)</c> 行级锁实现无损水平扩展：
/// 不同分片的消息互不竞争，发布吞吐随实例数线性扩展。
/// </para>
/// </summary>
public interface IShardingStrategy
{
    /// <summary>
    /// 根据聚合根 ID 计算分片号。
    /// </summary>
    /// <param name="aggregateRootId">聚合根 ID。</param>
    /// <param name="shardCount">分片总数，必须 &gt;= 1。</param>
    /// <returns>范围 [0, <paramref name="shardCount"/>-1] 内的分片号。</returns>
    /// <remarks>
    /// 实现必须保证：<br/>
    /// - 同一 <paramref name="aggregateRootId"/> 在 <paramref name="shardCount"/> 不变时始终返回相同分片号（一致性）；<br/>
    /// - 返回值在 [0, <paramref name="shardCount"/>-1] 范围内（边界）；<br/>
    /// - 不同聚合根 ID 尽量均匀分布到各分片（均衡）。
    /// </remarks>
    int ComputeShard(Guid aggregateRootId, int shardCount);
}
