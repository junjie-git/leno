using System.Diagnostics;
using Leno.SystemAdmin.Domain.Services;
using Leno.SystemAdmin.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Leno.SystemAdmin.Infrastructure.Services;

/// <summary>
/// HTTP 模块健康探测实现，通过 HTTP GET 请求各模块的 /health 端点进行健康检查。
/// 超时时间 3 秒，超时则标记为 Unhealthy。
/// </summary>
public sealed class HttpModuleHealthProbe : IModuleHealthProbe
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpModuleHealthProbe> _logger;
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(3);

    public HttpModuleHealthProbe(HttpClient httpClient, ILogger<HttpModuleHealthProbe> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(logger);
        _httpClient = httpClient;
        _logger = logger;
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
            timeoutCts.CancelAfter(ProbeTimeout);

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
            _logger.LogError("模块 {Module} 健康检查超时（>{Timeout}s），标记为 Unhealthy", moduleName, ProbeTimeout.TotalSeconds);

            return ModuleHealth.Unhealthy(
                moduleName,
                $"健康检查超时（>{ProbeTimeout.TotalSeconds}s）",
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