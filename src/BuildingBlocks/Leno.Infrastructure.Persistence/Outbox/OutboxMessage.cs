using System.Text.Json;
using Leno.SharedContracts.Events;

namespace Leno.Infrastructure.Outbox;

/// <summary>
/// 发件箱消息状态。
/// </summary>
public enum OutboxMessageStatus
{
    Pending,
    /// <summary>两阶段标记中间态：事务已提交置此状态，正在发布到 MQ，未确认完成。</summary>
    Publishing,
    Processed,
    DeadLetter
}

/// <summary>
/// 发件箱消息实体，聚合保存与事件记录在同一事务写入，保证原子性。
/// 后台进程 <see cref="OutboxPublisher{TDbContext}"/> 轮询发布。
/// </summary>
public class OutboxMessage
{
    public Guid Id { get; private set; }

    public string Type { get; private set; } = default!;

    public string Payload { get; private set; } = default!;

    public DateTime OccurredAt { get; private set; }

    public DateTime? ProcessedAt { get; private set; }

    /// <summary>进入 <see cref="OutboxMessageStatus.Publishing"/> 状态的时刻，用于扫描超时消息。</summary>
    public DateTime? PublishingStartedAt { get; private set; }

    public int RetryCount { get; private set; }

    public string? Error { get; private set; }

    public OutboxMessageStatus Status { get; private set; }

    /// <summary>事件模式版本号（M4.2），从 IntegrationEventBase.SchemaVersion 复制；非 IntegrationEventBase 派生事件默认 1。</summary>
    public int SchemaVersion { get; private set; }

    /// <summary>
    /// 聚合根 ID（4.4 Outbox 分片发布器）。
    /// 用于 <see cref="IShardingStrategy"/> 计算分片键，保证同一聚合根的事件始终落到同一分片，
    /// 从而由同一实例顺序发布，避免跨实例乱序。
    /// 历史数据无聚合根 ID 时默认 <see cref="Guid.Empty"/>，分片键按 0 计算（向后兼容）。
    /// </summary>
    public Guid AggregateRootId { get; private set; }

    /// <summary>
    /// 分片键（4.4 Outbox 分片发布器）。
    /// 由 <see cref="IShardingStrategy.ComputeShard"/> 按 <see cref="AggregateRootId"/> 计算，
    /// 范围 0..ShardCount-1。<see cref="ShardedOutboxPublisher{TDbContext}"/> 仅处理 ShardKey 等于当前实例分片号的消息。
    /// </summary>
    public int ShardKey { get; private set; }

    private OutboxMessage() { }

    public static OutboxMessage Create(IIntegrationEvent integrationEvent)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        var eventType = integrationEvent.GetType();
        // 优先存储 FullName（跨版本更稳定），resolver 兼容历史 AssemblyQualifiedName 数据
        return new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = eventType.FullName ?? eventType.AssemblyQualifiedName ?? eventType.Name,
            Payload = JsonSerializer.Serialize(integrationEvent, eventType),
            OccurredAt = integrationEvent.OccurredAt == default ? DateTime.UtcNow : integrationEvent.OccurredAt,
            Status = OutboxMessageStatus.Pending,
            SchemaVersion = integrationEvent is IntegrationEventBase baseEvt ? baseEvt.SchemaVersion : 1,
            AggregateRootId = Guid.Empty,
            ShardKey = 0
        };
    }

    /// <summary>
    /// 创建带分片信息的发件箱消息（4.4 Outbox 分片发布器）。
    /// </summary>
    /// <param name="integrationEvent">集成事件。</param>
    /// <param name="aggregateRootId">聚合根 ID，作为分片哈希输入；<see cref="Guid.Empty"/> 表示无聚合根（按 0 分片）。</param>
    /// <param name="shardingStrategy">分片策略；null 时落到分片 0（兼容单实例模式）。</param>
    /// <param name="shardCount">分片总数；&lt;=1 时落到分片 0。</param>
    public static OutboxMessage Create(
        IIntegrationEvent integrationEvent,
        Guid aggregateRootId,
        IShardingStrategy? shardingStrategy,
        int shardCount)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        var eventType = integrationEvent.GetType();
        var shardKey = shardingStrategy is null || shardCount <= 1
            ? 0
            : shardingStrategy.ComputeShard(aggregateRootId, shardCount);

        return new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = eventType.FullName ?? eventType.AssemblyQualifiedName ?? eventType.Name,
            Payload = JsonSerializer.Serialize(integrationEvent, eventType),
            OccurredAt = integrationEvent.OccurredAt == default ? DateTime.UtcNow : integrationEvent.OccurredAt,
            Status = OutboxMessageStatus.Pending,
            SchemaVersion = integrationEvent is IntegrationEventBase baseEvt ? baseEvt.SchemaVersion : 1,
            AggregateRootId = aggregateRootId,
            ShardKey = shardKey
        };
    }

    public void MarkAsProcessed()
    {
        Status = OutboxMessageStatus.Processed;
        ProcessedAt = DateTime.UtcNow;
        PublishingStartedAt = null;
        Error = null;
    }

    public void MarkAsFailed(string error, int maxRetryCount)
    {
        RetryCount++;
        Error = string.IsNullOrEmpty(error) ? "未知错误" : error;
        Status = RetryCount >= maxRetryCount ? OutboxMessageStatus.DeadLetter : OutboxMessageStatus.Pending;
        PublishingStartedAt = null;
    }

    /// <summary>
    /// 两阶段标记第一阶段：进入 <see cref="OutboxMessageStatus.Publishing"/> 中间态，并记录起始时间。
    /// 该状态在事务内提交，确保后续发布动作可被恢复扫描识别。
    /// </summary>
    public void MarkAsPublishing()
    {
        Status = OutboxMessageStatus.Publishing;
        PublishingStartedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// 重启扫描超时 <see cref="OutboxMessageStatus.Publishing"/> 消息时调用：
    /// 将消息回退至 <see cref="OutboxMessageStatus.Pending"/> 以便下次轮询重试，
    /// 由下游消费者幂等性保证不重复执行业务。
    /// </summary>
    public void ResetStalePublishing()
    {
        Status = OutboxMessageStatus.Pending;
        PublishingStartedAt = null;
    }
}
