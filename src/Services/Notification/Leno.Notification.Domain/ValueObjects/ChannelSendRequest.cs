namespace Leno.Notification.Domain.ValueObjects;

/// <summary>
/// 渠道发送请求记录，封装单次渠道发送的完整参数。
/// </summary>
/// <param name="SmsTemplateCode">短信渠道模板编码（如阿里云 SMS_12345678、腾讯云纯数字），仅 Sms 渠道使用，由 NotificationTemplate.SmsTemplateCode 透传。</param>
public sealed record ChannelSendRequest(
    NotificationChannel Channel,
    Recipient Recipient,
    string Subject,
    string Body,
    string IdempotencyKey,
    string? SmsTemplateCode = null);