using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.ServiceDiscovery;

namespace Leno.ApiGateway.Services;

/// <summary>
/// 基于 Consul 的 YARP 动态 Destination 解析器。
/// 当 Destination 的 Metadata 中包含 "ConsulServiceName" 时，从 Consul 查询健康实例并动态构建 Destination 列表。
/// 否则返回原始 destinations（静态配置回退）。
/// </summary>
public sealed class ConsulDestinationResolver : IDestinationResolver
{
    private readonly IConsulServiceDiscovery _discovery;
    private readonly ILogger<ConsulDestinationResolver> _logger;

    public ConsulDestinationResolver(
        IConsulServiceDiscovery discovery,
        ILogger<ConsulDestinationResolver> logger)
    {
        _discovery = discovery ?? throw new ArgumentNullException(nameof(discovery));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async ValueTask<ResolvedDestinationCollection> ResolveDestinationsAsync(
        IReadOnlyDictionary<string, DestinationConfig> destinations,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(destinations);

        // 检查第一个 destination 的 Metadata 是否包含 ConsulServiceName
        var firstDest = destinations.Values.FirstOrDefault();
        if (firstDest?.Metadata is not null
            && firstDest.Metadata.TryGetValue("ConsulServiceName", out var serviceName)
            && !string.IsNullOrWhiteSpace(serviceName))
        {
            return await ResolveFromConsulAsync(serviceName, cancellationToken);
        }

        // 静态配置回退：直接返回原始 destinations
        return new ResolvedDestinationCollection(destinations, EmptyChangeToken.Instance);
    }

    private async ValueTask<ResolvedDestinationCollection> ResolveFromConsulAsync(
        string serviceName,
        CancellationToken cancellationToken)
    {
        var instances = await _discovery.GetHealthyInstancesAsync(serviceName, cancellationToken);

        if (instances.Count == 0)
        {
            _logger.LogWarning(
                "No healthy instances found for Consul service {ServiceName}", serviceName);
        }

        var resolved = new Dictionary<string, DestinationConfig>(StringComparer.OrdinalIgnoreCase);

        foreach (var instance in instances)
        {
            var destinationId = $"{serviceName}-{instance.Id}";
            resolved[destinationId] = new DestinationConfig
            {
                Address = $"http://{instance.Address}:{instance.Port}/",
                Metadata = new Dictionary<string, string> { ["ConsulServiceName"] = serviceName }
            };
        }

        return new ResolvedDestinationCollection(resolved, EmptyChangeToken.Instance);
    }
}

/// <summary>
/// 空实现 IChangeToken，表示配置不会自动过期。
/// Consul 实例变更由 YARP Active HealthCheck + 请求时动态解析覆盖。
/// </summary>
internal sealed class EmptyChangeToken : IChangeToken
{
    public static readonly EmptyChangeToken Instance = new();

    public bool HasChanged => false;
    public bool ActiveChangeCallbacks => false;

    public IDisposable RegisterChangeCallback(Action<object?> callback, object? state)
        => EmptyDisposable.Instance;

    private sealed class EmptyDisposable : IDisposable
    {
        public static readonly EmptyDisposable Instance = new();
        public void Dispose() { }
    }
}
