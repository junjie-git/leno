using Leno.SystemAdmin.Domain.ValueObjects;

namespace Leno.SystemAdmin.Domain.Services;

/// <summary>
/// 健康聚合器领域服务接口，定义在领域层，由基础设施层实现。
/// 负责聚合所有模块的健康状态，计算整体健康评级。
/// 整体状态 = 所有模块中最差的状态。
/// </summary>
public interface IHealthAggregator
{
    /// <summary>
    /// 聚合所有模块的健康状态，返回各模块详情与整体状态。
    /// </summary>
    /// <param name="ct">取消令牌。</param>
    /// <returns>健康聚合结果，包含各模块健康详情与整体状态。</returns>
    Task<HealthAggregationResult> AggregateAsync(CancellationToken ct = default);
}

/// <summary>
/// 健康聚合结果，包含各模块健康详情与整体状态。
/// </summary>
public sealed record HealthAggregationResult
{
    /// <summary>整体状态（所有模块中最差的状态）。</summary>
    public ModuleHealthStatus OverallStatus { get; init; }

    /// <summary>各模块健康详情列表。</summary>
    public List<ModuleHealth> Modules { get; init; } = [];

    /// <summary>聚合时间。</summary>
    public DateTime AggregatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// 计算整体状态：取所有模块中最差的状态。
    /// </summary>
    public static ModuleHealthStatus ComputeOverallStatus(IReadOnlyList<ModuleHealth> modules)
    {
        if (modules.Count == 0)
        {
            return ModuleHealthStatus.Healthy;
        }

        if (modules.Any(m => m.Status == ModuleHealthStatus.Unhealthy))
        {
            return ModuleHealthStatus.Unhealthy;
        }

        if (modules.Any(m => m.Status == ModuleHealthStatus.Degraded))
        {
            return ModuleHealthStatus.Degraded;
        }

        return ModuleHealthStatus.Healthy;
    }
}