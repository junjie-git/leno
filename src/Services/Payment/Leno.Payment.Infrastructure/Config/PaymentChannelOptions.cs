namespace Leno.Payment.Infrastructure.Config;

/// <summary>
/// 支付渠道配置选项，绑定 appsettings 中 <c>Payment:Channels</c> 节。
/// </summary>
public sealed class PaymentChannelOptions
{
    /// <summary>微信支付渠道配置。</summary>
    public ChannelOption WeChatPay { get; set; } = new();

    /// <summary>支付宝渠道配置。</summary>
    public ChannelOption Alipay { get; set; } = new();
}

/// <summary>
/// 单个支付渠道配置项。
/// </summary>
public sealed class ChannelOption
{
    /// <summary>应用标识（微信 AppId / 支付宝 AppId）。</summary>
    public string AppId { get; set; } = string.Empty;

    /// <summary>商户号（微信 MchId / 支付宝 PID）。</summary>
    public string MchId { get; set; } = string.Empty;

    /// <summary>API 密钥（微信 APIv2 密钥 / 支付宝 RSA 私钥）。</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>双向证书路径（微信退款需客户端证书）。</summary>
    public string? CertPath { get; set; }

    /// <summary>支付异步通知地址。</summary>
    public string NotifyUrl { get; set; } = string.Empty;

    /// <summary>退款异步通知地址。</summary>
    public string RefundNotifyUrl { get; set; } = string.Empty;
}
