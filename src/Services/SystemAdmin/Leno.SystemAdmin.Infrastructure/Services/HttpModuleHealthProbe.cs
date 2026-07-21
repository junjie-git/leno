using System.Diagnostics;
using Leno.SystemAdmin.Domain.Services;
using Leno.SystemAdmin.Domain.ValueObjects;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Leno.SystemAdmin.Infrastructure.Services;

/// <summary>
/// HTTP 模块健康探测实现，通过 HTTP GET 请求各模块的 /health 端点进行健康检查。
/// 超时时间通过配置 <c>HealthProbe:TimeoutSeconds</c> 指定（默认 5 秒），超时则标记为 Unhealthy。
/// </summary>
public sealed class HttpModuleHealthProbe : IModuleHealthProbe
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpModuleHealthProbe> _logger;
    private readonly TimeSpan _probeTimeout;

    /// <summary>
    /// 获取当前配置的健康探测超时时长。
    /// </summary>
    internal TimeSpan ProbeTimeout => _probeTimeout;

    public HttpModuleHealthProbe(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<HttpModuleHealthProbe> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(logger);
        _httpClient = httpClient;
        _logger = logger;

        // 读取配置化超时，默认 5 秒；配置无效时回退到 5 秒并告警
        var timeoutSeconds = configuration["HealthProbe:TimeoutSeconds"];
        if (string.IsNullOrWhiteSpace(timeoutSeconds) || !int.TryParse(timeoutSeconds, out var parsed) || parsed <= 0)
        {
            _probeTimeout = TimeSpan.FromSeconds(5);
        }
        else
        {
            _probeTimeout = TimeSpan.FromSeconds(parsed);
        }
    }

    /// <inheritdoc />
    public async Task<ModuleHealth> ProbeAsync(string moduleEndpoint, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(moduleEndpoint);

        var moduleName = ExtractModuleName(moduleEndpoint);
        var startTime = Stopwatch.GetTimestamp();

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(_probeTimeout);

            var response = await _httpClient.GetAsync(moduleEndpoint, timeoutCts.Token);
            var elapsedMs = Stopwatch.GetElapsedTime(startTime).TotalMilliseconds;

            if (response.IsSuccessStatusCode)
            {
                _logger.LogDebug("模块 {Module} 健康检查通过，耗时 {ElapsedMs}ms", moduleName, (long)elapsedMs);
                return ModuleHealth.Healthy(moduleName, responseTimeMs: (long)elapsedMs);
            }

            var errorMessage = $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}";
            _logger.LogWarning("模块 {Module} 健康检查返回非成功状态码：{StatusCode}", moduleName, (int)response.StatusCode);

            return ModuleHealth.Degraded(moduleName, errorMessage, responseTimeMs: (long)elapsedMs);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // 超时
            var elapsedMs = Stopwatch.GetElapsedTime(startTime).TotalMilliseconds;
            _logger.LogError("模块 {Module} 健康检查超时（>{Timeout}s），标记为 Unhealthy", moduleName, _probeTimeout.TotalSeconds);

            return ModuleHealth.Unhealthy(
                moduleName,
                $"健康检查超时（>{_probeTimeout.TotalSeconds}s）",
                responseTimeMs: -1);
        }
        catch (Exception ex)
        {
            var elapsedMs = Stopwatch.GetElapsedTime(startTime).TotalMilliseconds;
            _logger.LogError(ex, "模块 {Module} 健康检查异常：{ErrorMessage}", moduleName, ex.Message);

            return ModuleHealth.Unhealthy(
                moduleName,
                ex.Message,
                responseTimeMs: (long)elapsedMs);
        }
    }

    private static string ExtractModuleName(string endpoint)
    {
        try
        {
            var uri = new Uri(endpoint);
            return uri.Host;
        }
        catch
        {
            return endpoint;
        }
    }
}