using Leno.SystemAdmin.Application.DTOs;
using Leno.SystemAdmin.Domain.Services;
using Microsoft.Extensions.Logging;

namespace Leno.SystemAdmin.Application.Services;

/// <summary>
/// 系统健康监控应用服务实现。
/// 委托 IHealthAggregator 执行聚合，映射为 DTO 返回。
/// </summary>
public sealed class HealthAppService : IHealthAppService
{
    private readonly IHealthAggregator _aggregator;
    private readonly ILogger<HealthAppService> _logger;

    public HealthAppService(IHealthAggregator aggregator, ILogger<HealthAppService> logger)
    {
        ArgumentNullException.ThrowIfNull(aggregator);
        ArgumentNullException.ThrowIfNull(logger);
        _aggregator = aggregator;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<HealthAggregationResultDto> GetAggregatedHealthAsync(CancellationToken ct = default)
    {
        var result = await _aggregator.AggregateAsync(ct);

        _logger.LogInformation("获取聚合健康状态：OverallStatus={OverallStatus}", result.OverallStatus);

        return new HealthAggregationResultDto
        {
            OverallStatus = result.OverallStatus.ToString(),
            Modules = result.Modules.Select(ToDto).ToList(),
            AggregatedAt = result.AggregatedAt
        };
    }

    /// <inheritdoc />
    public async Task<List<ModuleHealthDto>> GetModuleHealthDetailsAsync(CancellationToken ct = default)
    {
        var result = await _aggregator.AggregateAsync(ct);

        _logger.LogInformation("获取模块健康详情：模块数={ModuleCount}", result.Modules.Count);

        return result.Modules.Select(ToDto).ToList();
    }

    private static ModuleHealthDto ToDto(Domain.ValueObjects.ModuleHealth health)
        => new()
        {
            Module = health.Module,
            Status = health.Status.ToString(),
            Dependencies = health.Dependencies,
            CheckedAt = health.CheckedAt,
            ResponseTimeMs = health.ResponseTimeMs,
            ErrorMessage = health.ErrorMessage
        };
}