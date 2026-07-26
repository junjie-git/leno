using Leno.Payment.Domain.Exceptions;
using Leno.Payment.Domain.Services;
using Leno.Payment.Domain.ValueObjects;

namespace Leno.Payment.Infrastructure.Channels;

/// <summary>
/// 支付渠道适配器工厂实现，通过 DI 注入的 <c>IEnumerable&lt;IPaymentChannelAdapter&gt;</c> 构建按 Key 查找的字典。
/// 替换原 switch/if-else 硬编码分支，新增渠道仅需注册 DI 即可被工厂识别。
/// 接口 <see cref="IPaymentChannelFactory"/> 位于领域层（端口），本类位于基础设施层（适配器），符合 DDD 依赖倒置。
/// </summary>
public sealed class PaymentChannelFactory : IPaymentChannelFactory
{
    private readonly IReadOnlyDictionary<string, IPaymentChannelAdapter> _byKey;
    private readonly IReadOnlyList<PaymentChannelMetadata> _metadata;

    /// <summary>
    /// 构造工厂，注入所有渠道适配器并按 <see cref="IPaymentChannelAdapter.ChannelKey"/> 构建查找字典。
    /// 仅 <see cref="IPaymentChannelAdapter.IsEnabled"/> 为 true 的适配器参与查找。
    /// </summary>
    /// <param name="adapters">DI 注入的全部适配器实例。</param>
    /// <exception cref="ArgumentNullException"><paramref name="adapters"/> 为 null。</exception>
    /// <exception cref="InvalidOperationException">存在重复的 <see cref="IPaymentChannelAdapter.ChannelKey"/>。</exception>
    public PaymentChannelFactory(IEnumerable<IPaymentChannelAdapter> adapters)
    {
        ArgumentNullException.ThrowIfNull(adapters);

        var enabled = adapters.Where(a => a.IsEnabled).ToList();
        _byKey = enabled.ToDictionary(
            a => a.ChannelKey,
            a => a,
            StringComparer.OrdinalIgnoreCase);

        _metadata = enabled
            .Select(a => new PaymentChannelMetadata
            {
                ChannelKey = a.ChannelKey,
                DisplayName = a.DisplayName,
                Capabilities = a.Capabilities,
                IsEnabled = a.IsEnabled,
                Priority = 0
            })
            .OrderBy(m => m.Priority)
            .ThenBy(m => m.ChannelKey, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <inheritdoc />
    public IPaymentChannelAdapter GetAdapter(PaymentChannel channel)
    {
        var key = channel.ToString();
        if (!_byKey.TryGetValue(key, out var adapter))
        {
            throw new PaymentDomainException(
                $"渠道 '{channel}'（Key='{key}'）未注册或已禁用",
                "PAYMENT_CHANNEL_NOT_FOUND");
        }
        return adapter;
    }

    /// <inheritdoc />
    public IPaymentChannelAdapter GetAdapter(string channelKey)
    {
        if (string.IsNullOrWhiteSpace(channelKey))
        {
            throw new ArgumentException("渠道标识不可为空", nameof(channelKey));
        }

        if (!_byKey.TryGetValue(channelKey, out var adapter))
        {
            throw new PaymentDomainException(
                $"渠道 '{channelKey}' 未注册或已禁用",
                "PAYMENT_CHANNEL_NOT_FOUND");
        }
        return adapter;
    }

    /// <inheritdoc />
    public IReadOnlyList<string> ListEnabledChannels()
        => _byKey.Keys.ToList();

    /// <inheritdoc />
    public IReadOnlyList<PaymentChannelMetadata> ListEnabledMetadata()
        => _metadata;
}
