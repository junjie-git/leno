namespace Leno.Payment.Domain.Services;

/// <summary>
/// 支付渠道注册表抽象，提供渠道元数据查询能力。
/// 由基础设施层从 DI 注入的 <c>IEnumerable&lt;IPaymentChannelAdapter&gt;</c> 构建实例。
/// 调度层、管理后台、能力驱动逻辑（如退款能力判定）依赖此抽象。
/// </summary>
public interface IPaymentChannelRegistry
{
    /// <summary>
    /// 列出全部已注册渠道（含禁用）的元数据，按优先级升序排列。
    /// </summary>
    IReadOnlyList<PaymentChannelMetadata> GetAllChannels();

    /// <summary>
    /// 列出全部已启用渠道的元数据，按优先级升序排列。
    /// </summary>
    IReadOnlyList<PaymentChannelMetadata> GetEnabledChannels();

    /// <summary>
    /// 按渠道 Key 获取元数据。
    /// </summary>
    /// <param name="channelKey">渠道唯一标识，大小写不敏感。</param>
    /// <returns>元数据；未注册或禁用时返回 null。</returns>
    PaymentChannelMetadata? GetChannel(string channelKey);

    /// <summary>
    /// 判断渠道是否已注册（含禁用）。
    /// </summary>
    bool IsRegistered(string channelKey);

    /// <summary>
    /// 判断渠道是否已注册且启用。
    /// </summary>
    bool IsEnabled(string channelKey);

    /// <summary>
    /// 按能力过滤渠道：返回具备指定能力的全部已启用渠道。
    /// </summary>
    /// <param name="predicate">能力谓词，返回 true 表示所需能力匹配。</param>
    IReadOnlyList<PaymentChannelMetadata> GetChannelsByCapability(
        Func<PaymentChannelCapabilities, bool> predicate);
}
