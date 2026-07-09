using System.Text.Json;
using Leno.SharedContracts.Events;

namespace Leno.Infrastructure.Outbox;

/// <summary>
/// 发件箱消息状态。
/// </summary>
public enum OutboxMessageStatus
{
    Pending,
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

    public int RetryCount { get; private set; }

    public string? Error { get; private set; }

    public OutboxMessageStatus Status { get; private set; }

    private OutboxMessage() { }

    public static OutboxMessage Create(IIntegrationEvent integrationEvent)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        var eventType = integrationEvent.GetType();
        return new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = eventType.AssemblyQualifiedName ?? eventType.FullName ?? eventType.Name,
            Payload = JsonSerializer.Serialize(integrationEvent, eventType),
            OccurredAt = integrationEvent.OccurredAt == default ? DateTime.UtcNow : integrationEvent.OccurredAt,
            Status = OutboxMessageStatus.Pending
        };
    }

    public void MarkAsProcessed()
    {
        Status = OutboxMessageStatus.Processed;
        ProcessedAt = DateTime.UtcNow;
        Error = null;
    }

    public void MarkAsFailed(string error, int maxRetryCount)
    {
        RetryCount++;
        Error = string.IsNullOrEmpty(error) ? "未知错误" : error;
        Status = RetryCount >= maxRetryCount ? OutboxMessageStatus.DeadLetter : OutboxMessageStatus.Pending;
    }
}
