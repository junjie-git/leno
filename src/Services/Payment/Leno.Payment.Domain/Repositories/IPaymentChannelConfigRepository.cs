using Leno.Payment.Domain.Aggregates;
using Leno.Payment.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;

namespace Leno.Payment.Domain.Repositories;

/// <summary>
/// 支付渠道配置仓储接口，管理 <see cref="PaymentChannelConfig"/> 聚合。
/// </summary>
public interface IPaymentChannelConfigRepository : IRepository<PaymentChannelConfig>
{
    /// <summary>
    /// 获取所有配置项。
    /// </summary>
    Task<List<PaymentChannelConfig>> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// 按支付渠道获取配置项列表。
    /// </summary>
    /// <param name="channel">支付渠道。</param>
    Task<List<PaymentChannelConfig>> GetByChannelAsync(PaymentChannel channel, CancellationToken ct = default);
}