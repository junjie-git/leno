using Leno.Payment.Domain.Services;

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

    /// <summary>微信支付平台公钥（PEM 格式），V3 回调验签用。</summary>
    public string? PlatformPublicKey { get; set; }

    /// <summary>支付宝公钥（PEM 格式），回调验签用。</summary>
    public string? PublicKey { get; set; }

    /// <summary>双向证书路径（微信退款需客户端证书）。</summary>
    public string? CertPath { get; set; }

    /// <summary>支付异步通知地址。</summary>
    public string NotifyUrl { get; set; } = string.Empty;

    /// <summary>退款异步通知地址。</summary>
    public string RefundNotifyUrl { get; set; } = string.Empty;
}

/// <summary>
/// 支付渠道插件配置选项，绑定 appsettings 中 <c>Payment:Plugins</c> 节。
/// 阶段三 3.8：支持通过 <see cref="Assembly.LoadFrom"/> 动态加载外部插件程序集，
/// 新增渠道（如 UnionPay / ApplePay）无需修改 Payment BC 主代码，仅需提供独立 dll 并在配置中注册。
/// </summary>
public sealed class PaymentChannelPluginOptions
{
    /// <summary>
    /// 已启用渠道 Key 白名单。
    /// 非空时仅 <see cref="IPaymentChannelAdapter.ChannelKey"/> 命中白名单的适配器参与调度；
    /// 为空或未配置时全部已注册适配器默认启用（向后兼容）。
    /// </summary>
    public IReadOnlyList<string> EnabledChannels { get; set; } = Array.Empty<string>();

    /// <summary>
    /// 待加载的插件程序集绝对/相对路径列表。
    /// 启动时由 <c>PaymentChannelPluginLoader</c> 通过 <see cref="Assembly.LoadFrom"/> 加载，
    /// 扫描其中实现 <see cref="IPaymentChannelAdapter"/> 的非抽象类型并注册到 DI。
    /// </summary>
    public IReadOnlyList<string> PluginAssemblies { get; set; } = Array.Empty<string>();
}
