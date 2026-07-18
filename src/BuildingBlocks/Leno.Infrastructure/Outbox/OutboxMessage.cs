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
            SchemaVersion = integrationEvent is IntegrationEventBase baseEvt ? baseEvt.SchemaVersion : 1
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
