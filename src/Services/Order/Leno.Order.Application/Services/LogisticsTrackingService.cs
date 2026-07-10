using Leno.Order.Application.DTOs;

namespace Leno.Order.Application.Services;

/// <summary>
/// 物流轨迹查询服务接口，封装第三方物流轨迹查询。
/// </summary>
public interface ILogisticsTrackingService
{
    /// <summary>
    /// 查询物流单号对应的物流轨迹。
    /// </summary>
    /// <param name="logisticsNo">物流单号。</param>
    Task<LogisticsTrackingDto> GetTrackingAsync(string logisticsNo, CancellationToken ct = default);
}
