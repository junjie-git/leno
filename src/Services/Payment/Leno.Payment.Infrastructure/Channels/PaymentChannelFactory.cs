using Leno.Payment.Domain.Exceptions;
using Leno.Payment.Domain.Services;
using Leno.Payment.Domain.ValueObjects;

namespace Leno.Payment.Infrastructure.Channels;

/// <summary>
/// 支付渠道适配器工厂抽象，按 <see cref="PaymentChannel"/> 或渠道 Key 解析对应的 <see cref="IPaymentChannelAdapter"/> 实现。
/// 消费者与作业依赖此抽象以便单元测试注入 Mock，避免对具体渠道适配器（sealed）的硬依赖。
/// </summary>
/// <remarks>
/// 阶段三 3.8 插件化：工厂不再硬编码渠道到适配器的映射（无 switch / if-else），
/// 改为通过 DI 注入 <c>IEnumerable&lt;IPaymentChannelAdapter&gt;</c> 并按 <see cref="IPaymentChannelAdapter.ChannelKey"/>
/// 构建 <see cref="StringComparer.OrdinalIgnoreCase"/> 字典查找。新增渠道仅需实现接口并注册 DI，工厂零修改。
/// </remarks>
public interface IPaymentChannelFactory
{
    /// <summary>
    /// 按渠道枚举获取适配器（向后兼容入口）。
    /// 内部按枚举名匹配 <see cref="IPaymentChannelAdapter.ChannelKey"/>（大小写不敏感）。
    /// </summary>
    /// <param name="channel">支付渠道枚举。</param>
    /// <exception cref="PaymentDomainException">渠道未注册或已禁用。</exception>
    IPaymentChannelAdapter GetAdapter(PaymentChannel channel);

    /// <summary>
    /// 按渠道 Key 获取适配器（插件化推荐入口）。
    /// </summary>
    /// <param name="channelKey">渠道唯一标识，如 "WeChatPay" / "Alipay" / "UnionPay"，大小写不敏感。</param>
    /// <exception cref="ArgumentException"><paramref name="channelKey"/> 为空。</exception>
    /// <exception cref="PaymentDomainException">渠道未注册或已禁用。</exception>
    IPaymentChannelAdapter GetAdapter(string channelKey);

    /// <summary>
    /// 列出所有已启用渠道的 Key（按 <see cref="IPaymentChannelAdapter.ChannelKey"/>）。
    /// </summary>
    IReadOnlyList<string> ListEnabledChannels();

    /// <summary>
    /// 列出所有已启用渠道的元数据，按 <see cref="PaymentChannelMetadata.Priority"/> 升序排列。
    /// </summary>
    IReadOnlyList<PaymentChannelMetadata> ListEnabledMetadata();
}

/// <summary>
/// 支付渠道适配器工厂实现，通过 DI 注入的 <c>IEnumerable&lt;IPaymentChannelAdapter&gt;</c> 构建按 Key 查找的字典。
/// 替换原 switch/if-else 硬编码分支，新增渠道仅需注册 DI 即可被工厂识别。
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
