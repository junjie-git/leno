using Leno.Payment.Domain.Services;

namespace Leno.Payment.Infrastructure.Channels;

/// <summary>
/// 支付渠道注册表实现，从 DI 注入的 <c>IEnumerable&lt;IPaymentChannelAdapter&gt;</c> 构建渠道元数据索引。
/// 提供按 Key / 启用状态 / 能力的查询能力，供调度层与管理后台使用。
/// </summary>
public sealed class PaymentChannelRegistry : IPaymentChannelRegistry
{
    private readonly IReadOnlyList<PaymentChannelMetadata> _all;
    private readonly IReadOnlyList<PaymentChannelMetadata> _enabled;
    private readonly IReadOnlyDictionary<string, PaymentChannelMetadata> _byKey;

    /// <summary>
    /// 构造注册表，注入全部渠道适配器并构建元数据索引。
    /// </summary>
    /// <param name="adapters">DI 注入的全部适配器实例（含禁用）。</param>
    /// <exception cref="ArgumentNullException"><paramref name="adapters"/> 为 null。</exception>
    public PaymentChannelRegistry(IEnumerable<IPaymentChannelAdapter> adapters)
    {
        ArgumentNullException.ThrowIfNull(adapters);

        var list = adapters.ToList();
        _all = list
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

        _enabled = _all.Where(m => m.IsEnabled).ToList();
        _byKey = _all.ToDictionary(
            m => m.ChannelKey,
            m => m,
            StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public IReadOnlyList<PaymentChannelMetadata> GetAllChannels() => _all;

    /// <inheritdoc />
    public IReadOnlyList<PaymentChannelMetadata> GetEnabledChannels() => _enabled;

    /// <inheritdoc />
    public PaymentChannelMetadata? GetChannel(string channelKey)
    {
        if (string.IsNullOrWhiteSpace(channelKey))
        {
            return null;
        }
        return _byKey.TryGetValue(channelKey, out var meta) ? meta : null;
    }

    /// <inheritdoc />
    public bool IsRegistered(string channelKey)
        => !string.IsNullOrWhiteSpace(channelKey) && _byKey.ContainsKey(channelKey);

    /// <inheritdoc />
    public bool IsEnabled(string channelKey)
    {
        if (string.IsNullOrWhiteSpace(channelKey))
        {
            return false;
        }
        return _byKey.TryGetValue(channelKey, out var meta) && meta.IsEnabled;
    }

    /// <inheritdoc />
    public IReadOnlyList<PaymentChannelMetadata> GetChannelsByCapability(
        Func<PaymentChannelCapabilities, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return _enabled.Where(m => predicate(m.Capabilities)).ToList();
    }
}
