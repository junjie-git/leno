using Leno.Payment.Domain.Services;
using Leno.Payment.Domain.ValueObjects;

namespace Leno.Payment.Infrastructure.Channels;

/// <summary>
/// 支付渠道适配器工厂，按 <see cref="PaymentChannel"/> 解析对应的 <see cref="IPaymentChannelAdapter"/> 实现。
/// 各渠道适配器由 DI 注入，避免在调用方硬编码渠道选择逻辑。
/// </summary>
public sealed class PaymentChannelFactory
{
    private readonly WeChatPayAdapter _weChatPayAdapter;
    private readonly AlipayAdapter _alipayAdapter;

    public PaymentChannelFactory(WeChatPayAdapter weChatPayAdapter, AlipayAdapter alipayAdapter)
    {
        _weChatPayAdapter = weChatPayAdapter ?? throw new ArgumentNullException(nameof(weChatPayAdapter));
        _alipayAdapter = alipayAdapter ?? throw new ArgumentNullException(nameof(alipayAdapter));
    }

    /// <summary>
    /// 获取指定渠道的适配器。
    /// </summary>
    /// <param name="channel">支付渠道。</param>
    public IPaymentChannelAdapter GetAdapter(PaymentChannel channel)
    {
        return channel switch
        {
            PaymentChannel.WeChatPay => _weChatPayAdapter,
            PaymentChannel.Alipay => _alipayAdapter,
            _ => throw new ArgumentOutOfRangeException(nameof(channel), channel, "不支持的支付渠道")
        };
    }
}
