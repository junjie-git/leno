using Leno.Payment.Domain.Services;
using Leno.Payment.Domain.ValueObjects;

namespace Leno.Payment.Infrastructure.Channels;

/// <summary>
/// 支付渠道适配器工厂抽象，按 <see cref="PaymentChannel"/> 解析对应的 <see cref="IPaymentChannelAdapter"/> 实现。
/// 消费者与作业依赖此抽象以便单元测试注入 Mock，避免对具体渠道适配器（sealed）的硬依赖。
/// </summary>
public interface IPaymentChannelFactory
{
    /// <summary>
    /// 获取指定渠道的适配器。
    /// </summary>
    /// <param name="channel">支付渠道。</param>
    IPaymentChannelAdapter GetAdapter(PaymentChannel channel);
}

/// <summary>
/// 支付渠道适配器工厂，按 <see cref="PaymentChannel"/> 解析对应的 <see cref="IPaymentChannelAdapter"/> 实现。
/// 各渠道适配器由 DI 注入，避免在调用方硬编码渠道选择逻辑。
/// </summary>
public sealed class PaymentChannelFactory : IPaymentChannelFactory
{
    private readonly WeChatPayAdapter _weChatPayAdapter;
    private readonly AlipayAdapter _alipayAdapter;

    public PaymentChannelFactory(WeChatPayAdapter weChatPayAdapter, AlipayAdapter alipayAdapter)
    {
        _weChatPayAdapter = weChatPayAdapter ?? throw new ArgumentNullException(nameof(weChatPayAdapter));
        _alipayAdapter = alipayAdapter ?? throw new ArgumentNullException(nameof(alipayAdapter));
    }

    /// <inheritdoc />
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
