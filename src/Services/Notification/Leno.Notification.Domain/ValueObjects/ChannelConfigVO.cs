namespace Leno.Notification.Domain.ValueObjects;

/// <summary>
/// 渠道配置值对象，表示一个通知渠道的完整配置。
/// 敏感字段（如密码、密钥）在传输和存储时加密，显示时脱敏。
/// </summary>
public sealed class ChannelConfigVO
{
    /// <summary>渠道类型。</summary>
    public NotificationChannel Channel { get; set; }

    /// <summary>是否启用。</summary>
    public bool Enabled { get; set; }

    // ---- 邮件渠道参数 ----
    /// <summary>SMTP 主机。</summary>
    public string? SmtpHost { get; set; }

    /// <summary>SMTP 端口。</summary>
    public int? SmtpPort { get; set; }

    /// <summary>SMTP 用户名。</summary>
    public string? SmtpUsername { get; set; }

    /// <summary>SMTP 密码（加密存储，显示脱敏）。</summary>
    public string? SmtpPassword { get; set; }

    /// <summary>发件人地址。</summary>
    public string? FromAddress { get; set; }

    /// <summary>是否启用 SSL。</summary>
    public bool? UseSsl { get; set; }

    // ---- 短信渠道参数 ----
    /// <summary>短信服务商（Aliyun/Tencent）。</summary>
    public string? SmsProvider { get; set; }

    /// <summary>AccessKeyId。</summary>
    public string? AccessKeyId { get; set; }

    /// <summary>AccessKeySecret（加密存储，显示脱敏）。</summary>
    public string? AccessKeySecret { get; set; }

    /// <summary>短信签名名称。</summary>
    public string? SmsSignName { get; set; }
}