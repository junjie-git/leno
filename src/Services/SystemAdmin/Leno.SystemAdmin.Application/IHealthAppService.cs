using Leno.SystemAdmin.Application.DTOs;

namespace Leno.SystemAdmin.Application;

/// <summary>
/// 系统健康监控应用服务接口。
/// </summary>
public interface IHealthAppService
{
    /// <summary>获取聚合健康状态（整体状态 + 各模块详情）。</summary>
    Task<HealthAggregationResultDto> GetAggregatedHealthAsync(CancellationToken ct = default);

    /// <summary>获取各模块健康详情列表。</summary>
    Task<List<ModuleHealthDto>> GetModuleHealthDetailsAsync(CancellationToken ct = default);
}