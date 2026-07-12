using Leno.SystemAdmin.Domain.Services;
using Leno.SystemAdmin.Domain.ValueObjects;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Leno.SystemAdmin.Infrastructure.Services;

/// <summary>
/// 健康聚合器实现，通过 IModuleHealthProbe 探测所有配置的模块端点，
/// 聚合各模块健康状态并计算整体健康评级。
/// 整体状态 = 所有模块中最差的状态（Unhealthy > Degraded > Healthy）。
/// </summary>
public sealed class HealthAggregator : IHealthAggregator
{
    private readonly IModuleHealthProbe _probe;
    private readonly IConfiguration _configuration;
    private readonly ILogger<HealthAggregator> _logger;

    private const string ModuleEndpointsConfigKey = "HealthCheck:ModuleEndpoints";

    public HealthAggregator(
        IModuleHealthProbe probe,
        IConfiguration configuration,
        ILogger<HealthAggregator> logger)
    {
        ArgumentNullException.ThrowIfNull(probe);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(logger);
        _probe = probe;
        _configuration = configuration;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<HealthAggregationResult> AggregateAsync(CancellationToken ct = default)
    {
        var endpoints = GetModuleEndpoints();
        var modules = new List<ModuleHealth>();

        if (endpoints.Count == 0)
        {
            _logger.LogWarning("未配置任何模块健康检查端点（配置键：{Key}）", ModuleEndpointsConfigKey);
            return new HealthAggregationResult
            {
                OverallStatus = ModuleHealthStatus.Healthy,
                Modules = modules,
                AggregatedAt = DateTime.UtcNow
            };
        }

        // 并行探测所有模块
        var probeTasks = endpoints.Select(ep => ProbeWithFallbackAsync(ep, ct));
        var results = await Task.WhenAll(probeTasks);
        modules.AddRange(results);

        var overallStatus = HealthAggregationResult.ComputeOverallStatus(modules);

        _logger.LogInformation(
            "健康聚合完成：整体状态={OverallStatus}，模块数={ModuleCount}",
            overallStatus, modules.Count);

        return new HealthAggregationResult
        {
            OverallStatus = overallStatus,
            Modules = modules,
            AggregatedAt = DateTime.UtcNow
        };
    }

    private async Task<ModuleHealth> ProbeWithFallbackAsync(string endpoint, CancellationToken ct)
    {
        try
        {
            return await _probe.ProbeAsync(endpoint, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "探测模块端点 {Endpoint} 时发生未预期异常", endpoint);
            return new ModuleHealth(
                endpoint,
                ModuleHealthStatus.Unhealthy,
                [],
                DateTime.UtcNow,
                errorMessage: $"探测异常：{ex.Message}");
        }
    }

    private List<string> GetModuleEndpoints()
    {
        var section = _configuration.GetSection(ModuleEndpointsConfigKey);
        if (!section.Exists())
        {
            return [];
        }

        var endpoints = new List<string>();
        foreach (var child in section.GetChildren())
        {
            var value = child.Value;
            if (!string.IsNullOrWhiteSpace(value))
            {
                endpoints.Add(value);
            }
        }

        return endpoints;
    }
}