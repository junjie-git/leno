using Consul;
using Leno.ApiGateway.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leno.ApiGateway.Services;

/// <summary>
/// 服务实例信息，从 Consul Health API 解析。
/// </summary>
public sealed record ServiceInstance(string Id, string Address, int Port, IReadOnlyList<string> Tags);

/// <summary>
/// Consul 服务发现抽象，便于 <see cref="ConsulDestinationResolver"/> 解耦与单元测试 mock。
/// </summary>
public interface IConsulServiceDiscovery
{
    /// <summary>
    /// 查询指定 Consul 服务的健康实例列表。
    /// </summary>
    /// <param name="serviceName">Consul 中注册的服务名。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>健康实例列表；查询异常时返回空列表。</returns>
    Task<IReadOnlyList<ServiceInstance>> GetHealthyInstancesAsync(
        string serviceName, CancellationToken cancellationToken);
}

/// <summary>
/// 封装 Consul Health API 查询，返回指定服务的健康实例列表。
/// 供 <see cref="ConsulDestinationResolver"/> 在请求时调用，实现动态路由解析。
/// </summary>
public sealed class ConsulServiceDiscovery : IConsulServiceDiscovery
{
    private readonly IConsulClient _consulClient;
    private readonly ConsulOptions _options;
    private readonly ILogger<ConsulServiceDiscovery> _logger;

    public ConsulServiceDiscovery(
        IConsulClient consulClient,
        IOptions<ConsulOptions> options,
        ILogger<ConsulServiceDiscovery> logger)
    {
        _consulClient = consulClient ?? throw new ArgumentNullException(nameof(consulClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 查询指定 Consul 服务的健康实例列表。
    /// 仅返回 passing 状态的实例（由 <see cref="ConsulOptions.PassingOnly"/> 控制）。
    /// </summary>
    /// <param name="serviceName">Consul 中注册的服务名。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>健康实例列表；查询异常时返回空列表。</returns>
    public async Task<IReadOnlyList<ServiceInstance>> GetHealthyInstancesAsync(
        string serviceName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(serviceName))
        {
            throw new ArgumentException("Service name cannot be null or whitespace.", nameof(serviceName));
        }

        try
        {
            var result = await _consulClient.Health.Service(
                serviceName, null, _options.PassingOnly, cancellationToken);

            var instances = result.Response
                .Where(entry => entry.Service is not null && !string.IsNullOrEmpty(entry.Service.Address))
                .Select(entry => new ServiceInstance(
                    entry.Service.ID ?? Guid.NewGuid().ToString(),
                    entry.Service.Address,
                    entry.Service.Port,
                    (IReadOnlyList<string>)(entry.Service.Tags ?? Array.Empty<string>())))
                .ToList();

            _logger.LogDebug(
                "Consul returned {Count} healthy instances for service {ServiceName}",
                instances.Count, serviceName);

            return instances;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to query Consul for service {ServiceName}", serviceName);
            return Array.Empty<ServiceInstance>();
        }
    }
}
