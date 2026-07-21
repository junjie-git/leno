using Leno.Payment.Domain.ValueObjects;

namespace Leno.Payment.Domain.Services;

public interface IChannelConfigProvider
{
    Task<ChannelConfig> GetConfigAsync(PaymentChannel channel, CancellationToken ct = default);
}

public sealed class ChannelConfig
{
    public string AppId { get; set; } = string.Empty;
    public string MchId { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>微信支付平台公钥（PEM 格式），用于 V3 回调验签。支付宝渠道此字段为 null。</summary>
    public string? PlatformPublicKey { get; set; }

    public string? CertPath { get; set; }
    public string NotifyUrl { get; set; } = string.Empty;
    public string RefundNotifyUrl { get; set; } = string.Empty;
}
