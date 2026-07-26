using Leno.SystemAdmin.Domain.Exceptions;
using Leno.SystemAdmin.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;

namespace Leno.SystemAdmin.Domain.Aggregates;

/// <summary>
/// Outbox 消息域聚合根，对应各域 outbox_messages 表的只读投影。
/// 由 <see cref="Services.IOutboxQueryService"/> 跨域查询后构建，不在 SystemAdmin 库持久化。
/// </summary>
public sealed class OutboxMessageEntry : AggregateRoot
{
    private const int MaxContextLength = 128;
    private const int MaxEventTypeLength = 512;
    private const int MaxStatusLength = 32;

    /// <summary>所属限界上下文，如 Order、Payment。</summary>
    public string Context { get; private set; } = string.Empty;

    /// <summary>聚合根标识（来源事件关联的聚合 ID）。</summary>
    public Guid AggregateId { get; private set; }

    /// <summary>事件类型全名。</summary>
    public string EventType { get; private set; } = string.Empty;

    /// <summary>消息载荷（JSON）。</summary>
    public string Payload { get; private set; } = string.Empty;

    /// <summary>状态字符串：Pending / Publishing / Processed / DeadLetter。</summary>
    public string Status { get; private set; } = string.Empty;

    /// <summary>重试次数。</summary>
    public int RetryCount { get; private set; }

    /// <summary>错误信息，可空。</summary>
    public string? Error { get; private set; }

    /// <summary>创建时间（UTC）。</summary>
    public new DateTime CreatedAt { get; private set; }

    /// <summary>处理时间（UTC），可空。</summary>
    public DateTime? ProcessedAt { get; private set; }

    /// <summary>EF Core 无参构造。</summary>
    private OutboxMessageEntry() { }

    private OutboxMessageEntry(Guid id) : base(id) { }

    /// <summary>
    /// 工厂方法，校验字段并构建 Outbox 消息只读投影。
    /// </summary>
    /// <param name="id">消息标识。</param>
    /// <param name="context">所属限界上下文。</param>
    /// <param name="aggregateId">聚合根标识。</param>
    /// <param name="eventType">事件类型全名。</param>
    /// <param name="payload">消息载荷（JSON）。</param>
    /// <param name="status">状态字符串。</param>
    /// <param name="retryCount">重试次数。</param>
    /// <param name="error">错误信息，可空。</param>
    /// <param name="createdAt">创建时间（UTC）。</param>
    /// <param name="processedAt">处理时间（UTC），可空。</param>
    public static OutboxMessageEntry Create(
        Guid id,
        string context,
        Guid aggregateId,
        string eventType,
        string payload,
        string status,
        int retryCount,
        string? error,
        DateTime createdAt,
        DateTime? processedAt)
    {
        if (id == Guid.Empty)
        {
            throw new SystemAdminDomainException("Outbox 消息标识不可为空", "OUTBOX_MESSAGE_ID_EMPTY");
        }
        ValidateContext(context);
        ValidateEventType(eventType);
        ValidatePayload(payload);
        ValidateStatus(status);
        if (retryCount < 0)
        {
            throw new SystemAdminDomainException("重试次数不可为负数", "OUTBOX_MESSAGE_RETRY_NEGATIVE");
        }

        return new OutboxMessageEntry(id)
        {
            Context = context.Trim(),
            AggregateId = aggregateId,
            EventType = eventType.Trim(),
            Payload = payload,
            Status = status.Trim(),
            RetryCount = retryCount,
            Error = string.IsNullOrWhiteSpace(error) ? null : error.Trim(),
            CreatedAt = createdAt,
            ProcessedAt = processedAt
        };
    }

    /// <summary>
    /// 判断该消息是否处于积压状态（Pending 或 Publishing 或 DeadLetter 且未处理）。
    /// </summary>
    public bool IsBacklog()
    {
        return string.Equals(Status, "Pending", StringComparison.Ordinal)
               || string.Equals(Status, "Publishing", StringComparison.Ordinal)
               || (string.Equals(Status, "DeadLetter", StringComparison.Ordinal) && ProcessedAt is null);
    }

    /// <summary>计算积压时长（分钟），未积压返回 0。</summary>
    public long GetBacklogAgeMinutes(DateTime? atUtc = null)
    {
        if (!IsBacklog())
        {
            return 0;
        }
        var now = atUtc ?? DateTime.UtcNow;
        var age = now - CreatedAt;
        return age.TotalMinutes < 0 ? 0 : (long)age.TotalMinutes;
    }

    private static void ValidateContext(string context)
    {
        if (string.IsNullOrWhiteSpace(context))
        {
            throw new SystemAdminDomainException("所属上下文不可为空", "OUTBOX_MESSAGE_CONTEXT_EMPTY");
        }
        if (context.Trim().Length > MaxContextLength)
        {
            throw new SystemAdminDomainException($"所属上下文长度不可超过 {MaxContextLength} 字符", "OUTBOX_MESSAGE_CONTEXT_LENGTH");
        }
    }

    private static void ValidateEventType(string eventType)
    {
        if (string.IsNullOrWhiteSpace(eventType))
        {
            throw new SystemAdminDomainException("事件类型不可为空", "OUTBOX_MESSAGE_EVENT_TYPE_EMPTY");
        }
        if (eventType.Trim().Length > MaxEventTypeLength)
        {
            throw new SystemAdminDomainException($"事件类型长度不可超过 {MaxEventTypeLength} 字符", "OUTBOX_MESSAGE_EVENT_TYPE_LENGTH");
        }
    }

    private static void ValidatePayload(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            throw new SystemAdminDomainException("消息载荷不可为空", "OUTBOX_MESSAGE_PAYLOAD_EMPTY");
        }
    }

    private static void ValidateStatus(string status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            throw new SystemAdminDomainException("状态不可为空", "OUTBOX_MESSAGE_STATUS_EMPTY");
        }
        if (status.Trim().Length > MaxStatusLength)
        {
            throw new SystemAdminDomainException($"状态字符串长度不可超过 {MaxStatusLength} 字符", "OUTBOX_MESSAGE_STATUS_LENGTH");
        }
    }
}
