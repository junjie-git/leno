namespace Leno.Notification.Domain.ValueObjects;

/// <summary>
/// 渠道发送请求记录，封装单次渠道发送的完整参数。
/// </summary>
public sealed record ChannelSendRequest(
    NotificationChannel Channel,
    Recipient Recipient,
    string Subject,
    string Body,
    string IdempotencyKey);