namespace Leno.Notification.Domain.ValueObjects;

/// <summary>
/// 通知发送请求，由消费者或 API 传入，携带模板编码、用户、变量等。
/// </summary>
public sealed class NotificationRequest
{
    /// <summary>模板编码（对应 NotificationTemplate.Code）。</summary>
    public string TemplateCode { get; set; } = string.Empty;

    /// <summary>接收用户标识。</summary>
    public Guid UserId { get; set; }

    /// <summary>模板变量键值对。</summary>
    public Dictionary<string, string> Variables { get; set; } = [];

    /// <summary>幂等键，用于去重。</summary>
    public string IdempotencyKey { get; set; } = string.Empty;

    /// <summary>业务引用标识（如订单号）。</summary>
    public string BusinessRef { get; set; } = string.Empty;
}