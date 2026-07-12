using Leno.Payment.Application.DTOs;

namespace Leno.Payment.Application;

/// <summary>
/// 支付渠道配置应用服务，编排渠道配置管理用例。
/// </summary>
public interface IPaymentChannelConfigAppService
{
    /// <summary>
    /// 获取所有渠道配置项列表。
    /// </summary>
    Task<List<PaymentChannelConfigDto>> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// 按标识获取配置项详情。
    /// </summary>
    Task<PaymentChannelConfigDto?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// 更新配置项值。
    /// </summary>
    Task<PaymentChannelConfigDto> UpdateAsync(Guid id, UpdatePaymentChannelConfigDto dto, CancellationToken ct = default);

    /// <summary>
    /// 启用配置项。
    /// </summary>
    Task EnableAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// 禁用配置项。
    /// </summary>
    Task DisableAsync(Guid id, CancellationToken ct = default);
}