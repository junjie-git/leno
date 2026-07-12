using Leno.Notification.Domain.ValueObjects;

namespace Leno.Notification.Application.DTOs;

/// <summary>
/// 通知记录 DTO（站内信）。
/// </summary>
public sealed class NotificationRecordDto
{
    public Guid RecordId { get; set; }
    public Guid UserId { get; set; }
    public string TemplateCode { get; set; } = string.Empty;
    public NotificationChannel Channel { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public NotificationStatus Status { get; set; }
    public bool IsRead { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// 站内信分页查询结果。
/// </summary>
public sealed class NotificationListResultDto
{
    public List<NotificationRecordDto> Items { get; set; } = [];
    public int Total { get; set; }
    public int UnreadCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

/// <summary>
/// 通知模板 DTO。
/// </summary>
public sealed class NotificationTemplateDto
{
    public Guid TemplateId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public NotificationChannel Channel { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? SmsTemplateCode { get; set; }
    public string? Description { get; set; }
    public List<TemplateVariable> Variables { get; set; } = [];
    public TemplateStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// 创建/更新通知模板请求 DTO。
/// </summary>
public sealed class SaveNotificationTemplateDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public NotificationChannel Channel { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? SmsTemplateCode { get; set; }
    public string? Description { get; set; }
    public List<TemplateVariable> Variables { get; set; } = [];
}

/// <summary>
/// 模板预览请求 DTO。
/// </summary>
public sealed class PreviewTemplateDto
{
    public Dictionary<string, string> Variables { get; set; } = [];
}

/// <summary>
/// 模板预览结果 DTO。
/// </summary>
public sealed class TemplatePreviewResultDto
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

/// <summary>
/// 通知模板分页查询结果。
/// </summary>
public sealed class NotificationTemplateListResultDto
{
    public List<NotificationTemplateDto> Items { get; set; } = [];
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

/// <summary>
/// 用户通知偏好 DTO。
/// </summary>
public sealed class NotificationPreferenceDto
{
    public Guid PreferenceId { get; set; }
    public Guid UserId { get; set; }
    public Dictionary<string, List<NotificationChannel>> EventChannels { get; set; } = [];
    public PreferenceStatus Status { get; set; }
}

/// <summary>
/// 设置渠道偏好请求 DTO。
/// </summary>
public sealed class SetChannelPreferenceDto
{
    public string EventType { get; set; } = string.Empty;
    public List<NotificationChannel> Channels { get; set; } = [];
}

/// <summary>
/// 批量标记已读请求 DTO。
/// </summary>
public sealed class MarkAsReadDto
{
    public List<Guid> RecordIds { get; set; } = [];
}

/// <summary>
/// 通知发送请求 DTO（内部服务间调用）。
/// </summary>
public sealed class SendNotificationRequest
{
    /// <summary>模板编码（对应 NotificationTemplate.Code）。</summary>
    public string TemplateCode { get; set; } = string.Empty;

    /// <summary>接收用户标识。</summary>
    public Guid UserId { get; set; }

    /// <summary>模板变量键值对。</summary>
    public Dictionary<string, string> Variables { get; set; } = [];

    /// <summary>幂等键，用于去重。</summary>
    public string? IdempotencyKey { get; set; }

    /// <summary>业务引用标识（如订单号）。</summary>
    public string? BusinessRef { get; set; }
}

/// <summary>
/// 通知发送响应 DTO。
/// </summary>
public sealed class SendNotificationResponse
{
    /// <summary>是否发送成功。</summary>
    public bool Succeeded { get; set; }

    /// <summary>通知记录标识。</summary>
    public Guid? RecordId { get; set; }

    /// <summary>错误码。</summary>
    public string? ErrorCode { get; set; }

    /// <summary>错误信息。</summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// 死信通知记录 DTO。
/// </summary>
public sealed class DeadLetterRecordDto
{
    public Guid RecordId { get; set; }
    public Guid UserId { get; set; }
    public string TemplateCode { get; set; } = string.Empty;
    public NotificationChannel Channel { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public NotificationStatus Status { get; set; }
    public int RetryCount { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ErrorCode { get; set; }
    public DateTime? FailedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// 死信列表分页查询结果。
/// </summary>
public sealed class DeadLetterListResultDto
{
    public List<DeadLetterRecordDto> Items { get; set; } = [];
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

/// <summary>
/// 批量操作死信请求 DTO。
/// </summary>
public sealed class BatchDeadLetterRequestDto
{
    public List<Guid> RecordIds { get; set; } = [];
    /// <summary>丢弃原因（丢弃操作时必填）。</summary>
    public string? DiscardReason { get; set; }
}

/// <summary>
/// 批量操作结果 DTO。
/// </summary>
public sealed class BatchOperationResultDto
{
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public List<string> Errors { get; set; } = [];
}

/// <summary>
/// 渠道配置 DTO（显示时敏感字段脱敏为 ******）。
/// </summary>
public sealed class NotificationConfigDto
{
    public NotificationChannel Channel { get; set; }
    public bool Enabled { get; set; }
    public string? SmtpHost { get; set; }
    public int? SmtpPort { get; set; }
    public string? SmtpUsername { get; set; }
    /// <summary>脱敏后的密码，总是显示为 ****** 或空。</summary>
    public string? SmtpPassword { get; set; }
    public string? FromAddress { get; set; }
    public bool? UseSsl { get; set; }
    public string? SmsProvider { get; set; }
    public string? AccessKeyId { get; set; }
    /// <summary>脱敏后的密钥，总是显示为 ****** 或空。</summary>
    public string? AccessKeySecret { get; set; }
    public string? SmsSignName { get; set; }
}

/// <summary>
/// 保存渠道配置请求 DTO。
/// </summary>
public sealed class SaveNotificationConfigDto
{
    public bool? Enabled { get; set; }
    public string? SmtpHost { get; set; }
    public int? SmtpPort { get; set; }
    public string? SmtpUsername { get; set; }
    public string? SmtpPassword { get; set; }
    public string? FromAddress { get; set; }
    public bool? UseSsl { get; set; }
    public string? SmsProvider { get; set; }
    public string? AccessKeyId { get; set; }
    public string? AccessKeySecret { get; set; }
    public string? SmsSignName { get; set; }
}

/// <summary>
/// 测试发送请求 DTO。
/// </summary>
public sealed class TestSendRequestDto
{
    public NotificationChannel Channel { get; set; }
    /// <summary>测试接收邮箱（Email 渠道必填）。</summary>
    public string? Email { get; set; }
    /// <summary>测试接收手机号（Sms 渠道必填）。</summary>
    public string? PhoneNumber { get; set; }
}

/// <summary>
/// 测试发送结果 DTO。
/// </summary>
public sealed class TestSendResultDto
{
    public bool Succeeded { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ErrorCode { get; set; }
}

/// <summary>
/// 频率限制配置 DTO。
/// </summary>
public sealed class RateLimitConfigDto
{
    public NotificationChannel Channel { get; set; }
    /// <summary>每小时限制条数。</summary>
    public int HourlyLimit { get; set; }
    /// <summary>每日限制条数（仅 SMS 渠道）。</summary>
    public int? DailyLimit { get; set; }
    /// <summary>是否启用。</summary>
    public bool Enabled { get; set; }
}

/// <summary>
/// 保存频率限制配置请求 DTO。
/// </summary>
public sealed class SaveRateLimitConfigDto
{
    public int? HourlyLimit { get; set; }
    public int? DailyLimit { get; set; }
    public bool? Enabled { get; set; }
}

/// <summary>
/// 渠道回执请求 DTO（邮件回执）。
/// </summary>
public sealed class EmailReceiptDto
{
    /// <summary>渠道消息标识。</summary>
    public string ChannelMessageId { get; set; } = string.Empty;
    /// <summary>是否送达成功。</summary>
    public bool Succeeded { get; set; }
    /// <summary>回执原始数据。</summary>
    public string? RawPayload { get; set; }
    /// <summary>签名，用于防伪造。</summary>
    public string Signature { get; set; } = string.Empty;
    /// <summary>时间戳。</summary>
    public long Timestamp { get; set; }
}

/// <summary>
/// 渠道回执请求 DTO（短信回执）。
/// </summary>
public sealed class SmsReceiptDto
{
    /// <summary>渠道消息标识。</summary>
    public string ChannelMessageId { get; set; } = string.Empty;
    /// <summary>是否送达成功。</summary>
    public bool Succeeded { get; set; }
    /// <summary>回执原始数据。</summary>
    public string? RawPayload { get; set; }
    /// <summary>签名，用于防伪造。</summary>
    public string Signature { get; set; } = string.Empty;
    /// <summary>时间戳。</summary>
    public long Timestamp { get; set; }
}

/// <summary>
/// 通知记录详情 DTO（管理员端，含脱敏字段）。
/// </summary>
public sealed class NotificationRecordDetailDto
{
    public Guid RecordId { get; set; }
    public Guid UserId { get; set; }
    public string TemplateCode { get; set; } = string.Empty;
    public NotificationChannel Channel { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public NotificationStatus Status { get; set; }
    public int RetryCount { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ErrorCode { get; set; }
    /// <summary>脱敏后的邮箱或手机号。</summary>
    public string? MaskedContact { get; set; }
    public string? BusinessRef { get; set; }
    public string? ChannelMessageId { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? FailedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// 通知记录列表项 DTO（管理员端，含脱敏字段）。
/// </summary>
public sealed class NotificationRecordListItemDto
{
    public Guid RecordId { get; set; }
    public Guid UserId { get; set; }
    public string TemplateCode { get; set; } = string.Empty;
    public NotificationChannel Channel { get; set; }
    public string Title { get; set; } = string.Empty;
    public NotificationStatus Status { get; set; }
    /// <summary>脱敏的联系方式。</summary>
    public string? MaskedContact { get; set; }
    public string? BusinessRef { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// 通知记录分页查询结果。
/// </summary>
public sealed class NotificationRecordListResultDto
{
    public List<NotificationRecordListItemDto> Items { get; set; } = [];
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

/// <summary>
/// 送达率统计 DTO。
/// </summary>
public sealed class DeliveryStatisticsDto
{
    public NotificationChannel Channel { get; set; }
    public string TemplateCode { get; set; } = string.Empty;
    public int TotalCount { get; set; }
    public int SucceededCount { get; set; }
    public int FailedCount { get; set; }
    public int DeadLetteredCount { get; set; }
    public double DeliveryRate { get; set; }
}

/// <summary>
/// 送达率统计列表 DTO。
/// </summary>
public sealed class DeliveryStatisticsListDto
{
    public List<DeliveryStatisticsDto> Items { get; set; } = [];
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
}