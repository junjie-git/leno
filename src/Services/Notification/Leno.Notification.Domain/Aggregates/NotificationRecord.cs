using Leno.Notification.Domain.Exceptions;
using Leno.Notification.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;

namespace Leno.Notification.Domain.Aggregates;

/// <summary>
/// 通知记录聚合根，封装一条通知的发送状态与重试信息。
/// 状态流转：Pending → Sent；Pending/Failed → Failed（RetryCount++）；Failed → Abandoned（超重试上限）。
/// 聚合标识 <see cref="Entity.Id"/> 即对外 <c>RecordId</c>。
/// </summary>
public sealed class NotificationRecord : AggregateRoot
{
    /// <summary>接收用户标识。</summary>
    public Guid UserId { get; private set; }

    /// <summary>触发事件类型名（如 OrderCreatedEvent）。</summary>
    public string EventType { get; private set; } = string.Empty;

    /// <summary>触发事件标识，用于幂等去重，可空。</summary>
    public Guid? EventId { get; private set; }

    /// <summary>通知渠道。</summary>
    public NotificationChannel Channel { get; private set; }

    /// <summary>通知标题。</summary>
    public string Title { get; private set; } = string.Empty;

    /// <summary>通知内容。</summary>
    public string Content { get; private set; } = string.Empty;

    /// <summary>发送状态。</summary>
    public NotificationStatus Status { get; private set; }

    /// <summary>重试次数。</summary>
    public int RetryCount { get; private set; }

    /// <summary>站内信是否已读。</summary>
    public bool IsRead { get; private set; }

    /// <summary>发送时间（UTC）。</summary>
    public DateTime? SentAt { get; private set; }

    /// <summary>失败原因。</summary>
    public string? FailReason { get; private set; }

    /// <summary>最大重试次数。</summary>
    public const int MaxRetryCount = 3;

    /// <summary>EF Core 无参构造。</summary>
    private NotificationRecord() { }

    private NotificationRecord(Guid id) : base(id) { }

    /// <summary>
    /// 工厂方法，从模板渲染结果创建待发送通知记录。
    /// </summary>
    public static NotificationRecord Create(
        Guid recordId,
        Guid userId,
        string eventType,
        Guid? eventId,
        NotificationChannel channel,
        string title,
        string content)
    {
        if (recordId == Guid.Empty)
        {
            throw new NotificationDomainException("RecordId 不可为空", "NOTIFICATION_RECORD_ID_EMPTY");
        }

        if (userId == Guid.Empty)
        {
            throw new NotificationDomainException("UserId 不可为空", "NOTIFICATION_USER_EMPTY");
        }

        if (string.IsNullOrWhiteSpace(eventType))
        {
            throw new NotificationDomainException("EventType 不可为空", "NOTIFICATION_EVENT_TYPE_EMPTY");
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new NotificationDomainException("标题不可为空", "NOTIFICATION_TITLE_EMPTY");
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new NotificationDomainException("内容不可为空", "NOTIFICATION_CONTENT_EMPTY");
        }

        if (!Enum.IsDefined(channel))
        {
            throw new NotificationDomainException($"通知渠道非法：{channel}", "NOTIFICATION_CHANNEL_INVALID");
        }

        return new NotificationRecord(recordId)
        {
            UserId = userId,
            EventType = eventType,
            EventId = eventId,
            Channel = channel,
            Title = title,
            Content = content,
            Status = NotificationStatus.Pending,
            RetryCount = 0,
            IsRead = false
        };
    }

    /// <summary>
    /// 标记发送成功。
    /// </summary>
    public void MarkSent()
    {
        if (Status == NotificationStatus.Sent || Status == NotificationStatus.Abandoned)
        {
            throw new NotificationDomainException(
                $"当前状态 {Status} 不可标记发送成功", "NOTIFICATION_SENT_STATUS_INVALID");
        }

        Status = NotificationStatus.Sent;
        SentAt = DateTime.UtcNow;
        FailReason = null;
    }

    /// <summary>
    /// 标记发送失败，增加重试次数。
    /// </summary>
    public void MarkFailed(string reason)
    {
        if (Status == NotificationStatus.Sent || Status == NotificationStatus.Abandoned)
        {
            throw new NotificationDomainException(
                $"当前状态 {Status} 不可标记失败", "NOTIFICATION_FAILED_STATUS_INVALID");
        }

        Status = NotificationStatus.Failed;
        FailReason = string.IsNullOrWhiteSpace(reason) ? "未知错误" : reason;
        RetryCount++;
    }

    /// <summary>
    /// 放弃发送（超过最大重试次数）。
    /// </summary>
    public void MarkAbandoned()
    {
        if (Status == NotificationStatus.Abandoned)
        {
            return;
        }

        Status = NotificationStatus.Abandoned;
    }

    /// <summary>
    /// 判断是否可重试（未超过最大重试次数且未放弃）。
    /// </summary>
    public bool CanRetry => Status == NotificationStatus.Failed && RetryCount < MaxRetryCount;

    /// <summary>
    /// 重置为待发送态以供重试。
    /// </summary>
    public void ResetForRetry()
    {
        if (!CanRetry)
        {
            throw new NotificationDomainException(
                $"当前状态 {Status} 或重试次数 {RetryCount} 不可重试", "NOTIFICATION_RETRY_INVALID");
        }

        Status = NotificationStatus.Pending;
    }

    /// <summary>
    /// 标记已读（仅站内信）。
    /// </summary>
    public void MarkAsRead()
    {
        if (Channel != NotificationChannel.InApp)
        {
            throw new NotificationDomainException("仅站内信可标记已读", "NOTIFICATION_READ_CHANNEL_INVALID");
        }

        IsRead = true;
    }
}
