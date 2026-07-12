namespace Leno.Notification.Infrastructure.Channels;

/// <summary>
/// 短信渠道配置项。
/// </summary>
public sealed class SmsChannelOptions
{
    /// <summary>服务商（Aliyun/Tencent）。</summary>
    public string Provider { get; set; } = "Aliyun";

    /// <summary>AccessKeyId。</summary>
    public string AccessKeyId { get; set; } = string.Empty;

    /// <summary>AccessKeySecret。</summary>
    public string AccessKeySecret { get; set; } = string.Empty;

    /// <summary>签名名称。</summary>
    public string SignName { get; set; } = string.Empty;
}