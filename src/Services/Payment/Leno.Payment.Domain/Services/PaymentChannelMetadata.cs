namespace Leno.Payment.Domain.Services;

/// <summary>
/// 支付渠道元数据，描述渠道的标识、显示名称、能力与启用状态。
/// 由 <see cref="IPaymentChannelAdapter"/> 实现自描述，注册表聚合后供调度层查询。
/// </summary>
public sealed class PaymentChannelMetadata
{
    /// <summary>渠道唯一标识（如 "WeChatPay" / "Alipay"），大小写不敏感。</summary>
    public string ChannelKey { get; init; } = string.Empty;

    /// <summary>渠道显示名称（如 "微信支付"），用于管理后台展示。</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>渠道能力声明。</summary>
    public PaymentChannelCapabilities Capabilities { get; init; } = PaymentChannelCapabilities.Default;

    /// <summary>是否启用。禁用的渠道不参与调度。</summary>
    public bool IsEnabled { get; init; }

    /// <summary>优先级（数字越小越优先），用于多渠道兜底排序。</summary>
    public int Priority { get; init; }
}
