namespace Leno.Notification.Infrastructure.Channels.Email;

/// <summary>
/// 邮件渠道配置项。
/// </summary>
public sealed class EmailOptions
{
    /// <summary>SMTP 主机。</summary>
    public string SmtpHost { get; set; } = string.Empty;

    /// <summary>SMTP 端口。</summary>
    public int Port { get; set; } = 587;

    /// <summary>用户名。</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>密码。</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>发件人地址。</summary>
    public string FromAddress { get; set; } = string.Empty;

    /// <summary>是否启用 SSL。</summary>
    public bool EnableSsl { get; set; } = true;
}
