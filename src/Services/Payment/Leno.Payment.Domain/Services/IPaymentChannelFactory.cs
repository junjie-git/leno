using Leno.Payment.Domain.ValueObjects;

namespace Leno.Payment.Domain.Services;

/// <summary>
/// 支付渠道适配器工厂抽象（领域端口），按 <see cref="PaymentChannel"/> 或渠道 Key 解析对应的 <see cref="IPaymentChannelAdapter"/> 实现。
/// 消费者与作业依赖此抽象以便单元测试注入 Mock，避免对具体渠道适配器（sealed）的硬依赖。
/// </summary>
/// <remarks>
/// 阶段三 3.8 插件化：工厂不再硬编码渠道到适配器的映射（无 switch / if-else），
/// 改为通过 DI 注入 <c>IEnumerable&lt;IPaymentChannelAdapter&gt;</c> 并按 <see cref="IPaymentChannelAdapter.ChannelKey"/>
/// 构建 <see cref="StringComparer.OrdinalIgnoreCase"/> 字典查找。新增渠道仅需实现接口并注册 DI，工厂零修改。
/// 接口位于领域层（端口），实现 <c>PaymentChannelFactory</c> 位于基础设施层（适配器），符合 DDD 依赖倒置原则。
/// </remarks>
public interface IPaymentChannelFactory
{
    /// <summary>
    /// 按渠道枚举获取适配器（向后兼容入口）。
    /// 内部按枚举名匹配 <see cref="IPaymentChannelAdapter.ChannelKey"/>（大小写不敏感）。
    /// </summary>
    /// <param name="channel">支付渠道枚举。</param>
    /// <exception cref="Leno.Payment.Domain.Exceptions.PaymentDomainException">渠道未注册或已禁用。</exception>
    IPaymentChannelAdapter GetAdapter(PaymentChannel channel);

    /// <summary>
    /// 按渠道 Key 获取适配器（插件化推荐入口）。
    /// </summary>
    /// <param name="channelKey">渠道唯一标识，如 "WeChatPay" / "Alipay" / "UnionPay"，大小写不敏感。</param>
    /// <exception cref="ArgumentException"><paramref name="channelKey"/> 为空。</exception>
    /// <exception cref="Leno.Payment.Domain.Exceptions.PaymentDomainException">渠道未注册或已禁用。</exception>
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
