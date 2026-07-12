namespace Leno.Payment.Infrastructure.Channels.WeChatPay;

/// <summary>
/// 微信支付 V3 API 配置选项，绑定 appsettings 中 <c>Payment:WeChatPayV3</c> 节。
/// 敏感字段（ApiV3Key、PrivateKeyPath）应从环境变量或密钥管理服务读取。
/// </summary>
public sealed class WeChatPayOptions
{
    /// <summary>微信应用 AppId。</summary>
    public string AppId { get; set; } = string.Empty;

    /// <summary>微信商户号 MchId。</summary>
    public string MchId { get; set; } = string.Empty;

    /// <summary>API v3 密钥，用于回调通知签名验证。</summary>
    public string ApiV3Key { get; set; } = string.Empty;

    /// <summary>商户 API 私钥文件路径（PEM 格式），用于请求签名。</summary>
    public string PrivateKeyPath { get; set; } = string.Empty;

    /// <summary>商户 API 私钥内容（PEM 格式），优先级高于 PrivateKeyPath。</summary>
    public string? PrivateKey { get; set; }

    /// <summary>商户证书序列号，用于 Authorization 头。</summary>
    public string SerialNo { get; set; } = string.Empty;

    /// <summary>支付异步通知地址。</summary>
    public string NotifyUrl { get; set; } = string.Empty;

    /// <summary>退款异步通知地址。</summary>
    public string RefundNotifyUrl { get; set; } = string.Empty;
}