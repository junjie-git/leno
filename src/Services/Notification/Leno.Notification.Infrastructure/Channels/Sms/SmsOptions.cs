namespace Leno.Notification.Infrastructure.Channels.Sms;

/// <summary>
/// 短信渠道配置项。
/// </summary>
public sealed class SmsOptions
{
    /// <summary>服务商（Aliyun/Tencent）。</summary>
    public string Provider { get; set; } = "Aliyun";

    /// <summary>AccessKey。</summary>
    public string AccessKey { get; set; } = string.Empty;

    /// <summary>Secret。</summary>
    public string Secret { get; set; } = string.Empty;

    /// <summary>签名名称。</summary>
    public string SignName { get; set; } = string.Empty;

    /// <summary>默认模板代码。</summary>
    public string TemplateCode { get; set; } = string.Empty;

    /// <summary>API 端点。</summary>
    public string Endpoint { get; set; } = "https://dysmsapi.aliyuncs.com";
}
