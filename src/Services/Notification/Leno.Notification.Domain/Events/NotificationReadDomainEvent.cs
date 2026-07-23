using Leno.SharedKernel.Abstractions;

namespace Leno.Notification.Domain.Events;

/// <summary>
/// 通知记录被标记已读领域事件。
/// 由 NotificationRecord.MarkAsRead 在状态从未读流转到已读时收集，
/// mapper 可翻译为集成事件供读模型同步、未读数缓存失效、行为分析等消费方使用。
/// 幂等性：已读记录重复调用 MarkAsRead 不会发布此事件。
/// </summary>
public sealed class NotificationReadDomainEvent : DomainEventBase
{
    /// <summary>通知记录标识。</summary>
    public Guid RecordId { get; init; }

    /// <summary>接收用户标识。</summary>
    public Guid UserId { get; init; }

    /// <summary>已读时间（UTC）。</summary>
    public DateTime ReadAt { get; init; }

    public NotificationReadDomainEvent(Guid recordId, Guid userId, DateTime readAt)
        : base(recordId)
    {
        RecordId = recordId;
        UserId = userId;
        ReadAt = readAt;
    }
}
