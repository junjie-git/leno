using Leno.Payment.Domain.Services;
using Leno.Payment.Domain.ValueObjects;
using Microsoft.Extensions.Options;

namespace Leno.Payment.Infrastructure.Config;

/// <summary>
/// 渠道配置提供者实现，从 <see cref="IOptions{PaymentChannelOptions}"/> 读取并返回对应渠道的 <see cref="ChannelConfig"/>。
/// </summary>
public sealed class ChannelConfigProvider : IChannelConfigProvider
{
    private readonly PaymentChannelOptions _options;

    public ChannelConfigProvider(IOptions<PaymentChannelOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
    }

    /// <inheritdoc />
    public Task<ChannelConfig> GetConfigAsync(PaymentChannel channel, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var option = channel switch
        {
            PaymentChannel.WeChatPay => _options.WeChatPay,
            PaymentChannel.Alipay => _options.Alipay,
            _ => throw new ArgumentOutOfRangeException(nameof(channel), channel, "不支持的支付渠道")
        };

        var config = new ChannelConfig
        {
            AppId = option.AppId,
            MchId = option.MchId,
            ApiKey = option.ApiKey,
            PlatformPublicKey = option.PlatformPublicKey,
            CertPath = option.CertPath,
            NotifyUrl = option.NotifyUrl,
            RefundNotifyUrl = option.RefundNotifyUrl
        };

        return Task.FromResult(config);
    }
}
