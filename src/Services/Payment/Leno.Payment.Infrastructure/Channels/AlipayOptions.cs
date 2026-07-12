namespace Leno.Payment.Infrastructure.Channels;

/// <summary>
/// 支付宝渠道配置选项，绑定 appsettings 中 <c>Payment:Alipay</c> 节。
/// 敏感值（如私钥）应从环境变量或密钥管理服务读取，严禁硬编码。
/// </summary>
public sealed class AlipayOptions
{
    /// <summary>支付宝开放平台应用 ID。</summary>
    public string AppId { get; set; } = string.Empty;

    /// <summary>支付宝网关地址，默认 https://openapi.alipay.com/gateway.do。</summary>
    public string GatewayUrl { get; set; } = "https://openapi.alipay.com/gateway.do";

    /// <summary>RSA 私钥（PEM 格式），用于请求签名。</summary>
    public string PrivateKey { get; set; } = string.Empty;

    /// <summary>支付宝 RSA 公钥（PEM 格式），用于验签与通知验证。</summary>
    public string AlipayPublicKey { get; set; } = string.Empty;

    /// <summary>支付异步通知地址。</summary>
    public string NotifyUrl { get; set; } = string.Empty;

    /// <summary>签名算法，默认 RSA2（RSA-SHA256）。</summary>
    public string SignType { get; set; } = "RSA2";
}