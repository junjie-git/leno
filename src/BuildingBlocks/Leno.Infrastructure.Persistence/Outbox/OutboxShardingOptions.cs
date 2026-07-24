namespace Leno.Infrastructure.Outbox;

/// <summary>
/// Outbox 分片发布器配置选项（4.4 Outbox 分片发布器）。
/// <para>
/// 通过 <c>Outbox:Sharding</c> 配置节绑定，或环境变量 <c>OUTBOX__SHARDING__SHARD_ID</c> /
/// <c>OUTBOX__SHARDING__SHARD_COUNT</c> 注入（双下划线分隔层级，符合 ASP.NET Core 环境变量约定）。
/// </para>
/// <para>
/// 配置示例：<br/>
/// <code>
/// "Outbox": {
///   "Sharding": {
///     "ShardCount": 8,
///     "ShardId": 3,
///     "BatchSize": 100,
///     "PollingIntervalSeconds": 3
///   }
/// }
/// </code>
/// </para>
/// </summary>
public sealed class OutboxShardingOptions
{
    /// <summary>配置节名称。</summary>
    public const string SectionName = "Outbox:Sharding";

    /// <summary>
    /// 分片总数。默认 1（单实例模式，兼容现有部署）。
    /// <para>
    /// 多实例部署时设置为实例数（或实例数的整数倍），所有实例的 <see cref="ShardCount"/> 必须一致；
    /// 每个实例通过 <see cref="ShardId"/> 声明自己负责的分片号。
    /// 增减实例时需同步调整所有实例的 <see cref="ShardCount"/> 并回填历史数据的 <c>shard_key</c>。
    /// </para>
    /// </summary>
    public int ShardCount { get; set; } = 1;

    /// <summary>
    /// 当前实例负责的分片号。默认 0。
    /// <para>
    /// 范围 [0, <see cref="ShardCount"/>-1]。<see cref="ShardedOutboxPublisher{TDbContext}"/>
    /// 仅处理 <c>OutboxMessage.ShardKey == ShardId</c> 的消息，避免多实例重复发布。
    /// </para>
    /// <para>
    /// 部署时通过环境变量 <c>OUTBOX__SHARDING__SHARD_ID</c> 为每个实例指定不同分片号。
    /// </para>
    /// </summary>
    public int ShardId { get; set; }

    /// <summary>
    /// 单次轮询拉取的最大消息数。默认 100。
    /// <para>
    /// 较大值提升单实例吞吐但延长事务持锁时间；较小值降低锁持有时间但增加轮询次数。
    /// 推荐值：50-200，根据下游 MQ 延迟与 DB 负载调整。
    /// </para>
    /// </summary>
    public int BatchSize { get; set; } = 100;

    /// <summary>
    /// 轮询间隔（秒）。默认 3。
    /// <para>
    /// <see cref="ShardedOutboxPublisher{TDbContext}"/> 每轮拉取本分片 pending 消息后等待此间隔再进入下一轮。
    /// 较小值降低发布延迟但增加 DB 负载；较大值降低 DB 负载但增加发布延迟。
    /// </para>
    /// </summary>
    public int PollingIntervalSeconds { get; set; } = 3;

    /// <summary>
    /// 最大重试次数。默认 5。
    /// <para>
    /// 超过此次数后消息进入 <see cref="OutboxMessageStatus.DeadLetter"/> 状态，不再自动重试。
    /// </para>
    /// </summary>
    public int MaxRetryCount { get; set; } = 5;

    /// <summary>
    /// Publishing 状态超时阈值（秒）。默认 300（5 分钟）。
    /// <para>
    /// 消息停留在 <see cref="OutboxMessageStatus.Publishing"/> 状态超过此阈值时，
    /// 由 <see cref="ShardedOutboxPublisher{TDbContext}"/> 在下次轮询时回退为 Pending 重试。
    /// 用于恢复因实例崩溃或标记失败而滞留的中间态消息。
    /// </para>
    /// </summary>
    public int PublishingStaleTimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// pending 积压告警阈值。默认 100。
    /// <para>
    /// 当前分片 pending 消息数超过此阈值时记录告警日志。
    /// </para>
    /// </summary>
    public int PendingAlertThreshold { get; set; } = 100;

    /// <summary>
    /// 校验配置合法性。
    /// </summary>
    /// <exception cref="InvalidOperationException">配置非法时抛出。</exception>
    public void Validate()
    {
        if (ShardCount < 1)
        {
            throw new InvalidOperationException(
                $"Outbox:Sharding:ShardCount 必须 >= 1，当前为 {ShardCount}");
        }

        if (ShardId < 0 || ShardId >= ShardCount)
        {
            throw new InvalidOperationException(
                $"Outbox:Sharding:ShardId 必须在 [0, {ShardCount - 1}] 范围内，当前为 {ShardId}");
        }

        if (BatchSize < 1)
        {
            throw new InvalidOperationException(
                $"Outbox:Sharding:BatchSize 必须 >= 1，当前为 {BatchSize}");
        }

        if (PollingIntervalSeconds < 1)
        {
            throw new InvalidOperationException(
                $"Outbox:Sharding:PollingIntervalSeconds 必须 >= 1，当前为 {PollingIntervalSeconds}");
        }

        if (MaxRetryCount < 1)
        {
            throw new InvalidOperationException(
                $"Outbox:Sharding:MaxRetryCount 必须 >= 1，当前为 {MaxRetryCount}");
        }
    }
}
