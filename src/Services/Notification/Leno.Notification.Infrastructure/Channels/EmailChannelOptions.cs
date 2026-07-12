namespace Leno.Notification.Infrastructure.Channels;

/// <summary>
/// 邮件渠道配置项。
/// </summary>
public sealed class EmailChannelOptions
{
    /// <summary>SMTP 主机。</summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>SMTP 端口。</summary>
    public int Port { get; set; } = 587;

    /// <summary>用户名。</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>密码。</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>发件人地址。</summary>
    public string From { get; set; } = string.Empty;

    /// <summary>是否启用 SSL。</summary>
    public bool UseSsl { get; set; } = true;
}