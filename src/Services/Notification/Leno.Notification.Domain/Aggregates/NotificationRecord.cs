using Leno.Notification.Domain.Exceptions;
using Leno.Notification.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;

namespace Leno.Notification.Domain.Aggregates;

/// <summary>
/// 通知记录聚合根，封装一条通知的发送状态与重试信息。
/// 6 状态机：Pending → Sending → Succeeded / Failed → Retried → DeadLettered。
/// 聚合标识 <see cref="Entity.Id"/> 即对外 <c>RecordId</c>。
/// </summary>
public sealed class NotificationRecord : AggregateRoot
{
    /// <summary>接收用户标识。</summary>
    public Guid UserId { get; private set; }

    /// <summary>模板编码（如 OrderCreated）。</summary>
    public string TemplateCode { get; private set; } = string.Empty;

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

    /// <summary>最大重试次数。</summary>
    public int MaxRetry { get; private set; }

    /// <summary>下次重试时间（UTC）。</summary>
    public DateTime? NextRetryAt { get; private set; }

    /// <summary>站内信是否已读。</summary>
    public bool IsRead { get; private set; }

    /// <summary>发送时间（UTC）。</summary>
    public DateTime? SentAt { get; private set; }

    /// <summary>失败时间（UTC）。</summary>
    public DateTime? FailedAt { get; private set; }

    /// <summary>错误信息。</summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>错误码。</summary>
    public string? ErrorCode { get; private set; }

    /// <summary>渲染后的内容快照（JSON），用于重试时无需重新渲染。</summary>
    public string? ContentSnapshot { get; private set; }

    /// <summary>渠道消息标识（如短信回执 ID）。</summary>
    public string? ChannelMessageId { get; private set; }

    /// <summary>渠道回执原始数据（JSON）。</summary>
    public string? ChannelReceipt { get; private set; }

    /// <summary>业务引用标识（如订单号）。</summary>
    public string? BusinessRef { get; private set; }

    /// <summary>幂等键。</summary>
    public string? IdempotencyKey { get; private set; }

    /// <summary>默认最大重试次数。</summary>
    public const int DefaultMaxRetry = 3;

    /// <summary>EF Core 无参构造。</summary>
    private NotificationRecord() { }

    private NotificationRecord(Guid id) : base(id) { }

    /// <summary>
    /// 工厂方法，从模板渲染结果创建待发送通知记录。
    /// </summary>
    public static NotificationRecord Create(
        Guid recordId,
        Guid userId,
        string templateCode,
        Guid? eventId,
        NotificationChannel channel,
        string title,
        string content,
        string? businessRef = null,
        string? idempotencyKey = null,
        int maxRetry = DefaultMaxRetry)
    {
        if (recordId == Guid.Empty)
        {
            throw new NotificationDomainException("RecordId 不可为空", "NOTIFICATION_RECORD_ID_EMPTY");
        }

        if (userId == Guid.Empty)
        {
            throw new NotificationDomainException("UserId 不可为空", "NOTIFICATION_USER_EMPTY");
        }

        if (string.IsNullOrWhiteSpace(templateCode))
        {
            throw new NotificationDomainException("TemplateCode 不可为空", "NOTIFICATION_TEMPLATE_CODE_EMPTY");
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

        if (maxRetry < 0)
        {
            throw new NotificationDomainException("最大重试次数不可为负", "NOTIFICATION_MAX_RETRY_INVALID");
        }

        return new NotificationRecord(recordId)
        {
            UserId = userId,
            TemplateCode = templateCode,
            EventId = eventId,
            Channel = channel,
            Title = title,
            Content = content,
            Status = NotificationStatus.Pending,
            RetryCount = 0,
            MaxRetry = maxRetry,
            IsRead = false,
            BusinessRef = businessRef,
            IdempotencyKey = idempotencyKey
        };
    }

    /// <summary>
    /// 标记发送中。Pending → Sending，或 Retried → Sending（重试场景）。
    /// </summary>
    public void MarkSending()
    {
        if (Status != NotificationStatus.Pending && Status != NotificationStatus.Retried)
        {
            throw new NotificationDomainException(
                $"当前状态 {Status} 不可标记发送中，仅 Pending 或 Retried 状态可转入 Sending", "NOTIFICATION_SENDING_STATUS_INVALID");
        }

        Status = NotificationStatus.Sending;
    }

    /// <summary>
    /// 标记发送成功。Sending → Succeeded（终态）。
    /// </summary>
    public void MarkSucceeded(string? channelMessageId = null)
    {
        if (Status != NotificationStatus.Sending)
        {
            throw new NotificationDomainException(
                $"当前状态 {Status} 不可标记成功，仅 Sending 状态可转入 Succeeded", "NOTIFICATION_SUCCEEDED_STATUS_INVALID");
        }

        Status = NotificationStatus.Succeeded;
        SentAt = DateTime.UtcNow;
        ChannelMessageId = channelMessageId;
        ErrorMessage = null;
        ErrorCode = null;
    }

    /// <summary>
    /// 标记发送失败。Sending → Failed。
    /// </summary>
    public void MarkFailed(string errorMessage, string? errorCode = null)
    {
        if (Status != NotificationStatus.Sending)
        {
            throw new NotificationDomainException(
                $"当前状态 {Status} 不可标记失败，仅 Sending 状态可转入 Failed", "NOTIFICATION_FAILED_STATUS_INVALID");
        }

        Status = NotificationStatus.Failed;
        ErrorMessage = string.IsNullOrWhiteSpace(errorMessage) ? "未知错误" : errorMessage;
        ErrorCode = errorCode;
        FailedAt = DateTime.UtcNow;
        RetryCount++;
    }

    /// <summary>
    /// 安排重试。Failed → Retried。
    /// </summary>
    public void ScheduleRetry(DateTime? nextRetryAt = null)
    {
        if (Status != NotificationStatus.Failed)
        {
            throw new NotificationDomainException(
                $"当前状态 {Status} 不可安排重试，仅 Failed 状态可转入 Retried", "NOTIFICATION_RETRY_STATUS_INVALID");
        }

        Status = NotificationStatus.Retried;
        NextRetryAt = nextRetryAt ?? DateTime.UtcNow.AddMinutes(1);
    }

    /// <summary>
    /// 移入死信队列。Retried → DeadLettered（终态）。
    /// 此外允许 Sending → DeadLettered，用于 DeadLetterAppService.BatchResendAsync
    /// 在 MarkResend 后渠道发送异常时回退状态，避免记录卡死在 Sending（无 Job 拾取）。
    /// </summary>
    public void MoveToDeadLetter(string reason)
    {
        if (Status == NotificationStatus.DeadLettered)
        {
            return;
        }

        if (Status != NotificationStatus.Retried && Status != NotificationStatus.Sending)
        {
            throw new NotificationDomainException(
                $"当前状态 {Status} 不可移入死信，仅 Retried 或 Sending 状态可转入 DeadLettered", "NOTIFICATION_DEAD_LETTER_STATUS_INVALID");
        }

        Status = NotificationStatus.DeadLettered;
        ErrorMessage = string.IsNullOrWhiteSpace(reason) ? "超过最大重试次数" : reason;
    }

    /// <summary>
    /// 判断是否可重试（未超过最大重试次数且未达终态）。
    /// </summary>
    public bool CanRetry => Status == NotificationStatus.Failed && RetryCount < MaxRetry;

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

    /// <summary>
    /// 从死信状态手工重发。DeadLettered → Sending，重置重试计数与错误信息。
    /// </summary>
    /// <remarks>
    /// 注意：此方法将状态置为 Sending，但没有任何 Job 拾取 Sending 状态记录，
    /// 会导致记录永久卡死。建议使用 <see cref="RequeueForSend"/> 将状态置为 Pending
    /// 让 NotificationDispatchJob 接管实际发送。
    /// </remarks>
    public void MarkResend()
    {
        if (Status != NotificationStatus.DeadLettered)
        {
            throw new NotificationDomainException(
                $"当前状态 {Status} 不可手工重发，仅 DeadLettered 状态可重发", "NOTIFICATION_RESEND_STATUS_INVALID");
        }

        Status = NotificationStatus.Sending;
        RetryCount = 0;
        ErrorMessage = null;
        ErrorCode = null;
        FailedAt = null;
        NextRetryAt = null;
    }

    /// <summary>
    /// 重新排队发送。DeadLettered → Pending，让 NotificationDispatchJob 重新拾取实际发送。
    /// 相比 <see cref="MarkResend"/>（置为 Sending 无 Job 拾取），此方法置为 Pending
    /// 可被 DispatchJob 正常接管，避免记录卡死在 Sending 状态。
    /// </summary>
    public void RequeueForSend()
    {
        if (Status != NotificationStatus.DeadLettered)
        {
            throw new NotificationDomainException(
                $"当前状态 {Status} 不可重新排队，仅 DeadLettered 状态可转入 Pending", "NOTIFICATION_REQUEUE_STATUS_INVALID");
        }

        Status = NotificationStatus.Pending;
        RetryCount = 0;
        ErrorMessage = null;
        ErrorCode = null;
        FailedAt = null;
        NextRetryAt = null;
    }

    /// <summary>
    /// 标记死信已丢弃，记录丢弃原因（仅可从 DeadLettered 状态操作）。
    /// </summary>
    public void MarkDiscarded(string reason)
    {
        if (Status != NotificationStatus.DeadLettered)
        {
            throw new NotificationDomainException(
                $"当前状态 {Status} 不可标记丢弃，仅 DeadLettered 状态可丢弃", "NOTIFICATION_DISCARD_STATUS_INVALID");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new NotificationDomainException("丢弃原因不可为空", "NOTIFICATION_DISCARD_REASON_EMPTY");
        }

        ErrorMessage = $"已丢弃：{reason}";
    }

    /// <summary>
    /// 应用渠道回执，更新通知记录状态。
    /// 仅当回执的 ChannelMessageId 匹配且记录非终态时更新。
    /// 已 Succeeded 的记录幂等处理（不重复更新）。
    /// </summary>
    /// <param name="channelMessageId">渠道消息标识，用于匹配记录。</param>
    /// <param name="succeeded">渠道是否确认送达成功。</param>
    /// <param name="receiptPayload">渠道回执原始数据（JSON），敏感字段已脱敏。</param>
    /// <returns>true 表示回执已应用，false 表示幂等跳过（已 Succeeded 或不匹配）。</returns>
    public bool ApplyReceipt(string channelMessageId, bool succeeded, string? receiptPayload)
    {
        if (string.IsNullOrWhiteSpace(channelMessageId))
        {
            throw new NotificationDomainException("渠道消息标识不可为空", "NOTIFICATION_RECEIPT_MESSAGE_ID_EMPTY");
        }

        // 仅当 ChannelMessageId 匹配时处理
        if (!string.Equals(ChannelMessageId, channelMessageId, StringComparison.Ordinal))
        {
            return false;
        }

        // 已 Succeeded 的记录幂等处理
        if (Status == NotificationStatus.Succeeded)
        {
            return false;
        }

        if (succeeded)
        {
            Status = NotificationStatus.Succeeded;
            SentAt = DateTime.UtcNow;
            ErrorMessage = null;
            ErrorCode = null;
        }
        else
        {
            // P1-34：渠道回执确认失败时，将记录状态置为 Failed（可被 NotificationRetryJob 拾取重试），
            // 而非仅记录错误信息并保留当前状态导致记录滞留。
            // 仅 Sending 状态可安全转入 Failed；其他状态（如 Failed/Retried）保留现状仅更新错误信息，
            // 避免破坏状态机不变量抛出异常。
            if (Status == NotificationStatus.Sending)
            {
                MarkFailed("渠道回执确认失败", "CHANNEL_RECEIPT_FAILED");
            }
            else
            {
                ErrorMessage = "渠道回执确认失败";
                ErrorCode = "CHANNEL_RECEIPT_FAILED";
            }
        }

        ChannelReceipt = MaskSensitiveData(receiptPayload);
        return true;
    }

    /// <summary>
    /// 脱敏回执中的敏感数据（手机号、邮箱）。
    /// </summary>
    private static string? MaskSensitiveData(string? receiptPayload)
    {
        if (string.IsNullOrWhiteSpace(receiptPayload))
        {
            return receiptPayload;
        }

        // 脱敏手机号：138****1234
        var masked = System.Text.RegularExpressions.Regex.Replace(
            receiptPayload,
            @"(\+?86)?1[3-9]\d{9}",
            match =>
            {
                var phone = match.Value;
                if (phone.Length >= 11)
                {
                    return phone[..3] + "****" + phone[^4..];
                }
                return "***";
            });

        // 脱敏邮箱：将 @ 前部分脱敏
        masked = System.Text.RegularExpressions.Regex.Replace(
            masked,
            @"([a-zA-Z0-9._%+-]+)@",
            m =>
            {
                var local = m.Groups[1].Value;
                if (local.Length <= 3)
                {
                    return "***@";
                }
                return local[..3] + "***@";
            });

        return masked;
    }
}