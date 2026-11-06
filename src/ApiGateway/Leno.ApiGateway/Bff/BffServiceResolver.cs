using Leno.ApiGateway.Options;
using Leno.ApiGateway.Services;
using Microsoft.Extensions.Options;

namespace Leno.ApiGateway.Bff;

/// <summary>
/// <see cref="IBffServiceResolver"/> 默认实现：显式基址优先，其次 Consul 健康实例发现。
/// <para>
/// 配置来源为 <c>Bff:Services</c>（绑定到 <see cref="BffOptions.Services"/>），
/// 每次解析读取 <see cref="IOptionsMonitor{T}.CurrentValue"/>，因此 Consul KV / 环境变量热更新后立即生效。
/// </para>
/// </summary>
public sealed class BffServiceResolver : IBffServiceResolver
{
    private readonly IOptionsMonitor<BffOptions> _optionsMonitor;
    private readonly IConsulServiceDiscovery _serviceDiscovery;
    private readonly ILogger<BffServiceResolver> _logger;

    /// <summary>构造解析器。</summary>
    /// <param name="optionsMonitor">BFF 配置监视器（支持热更新）。</param>
    /// <param name="serviceDiscovery">Consul 健康实例查询。</param>
    /// <param name="logger">日志。</param>
    public BffServiceResolver(
        IOptionsMonitor<BffOptions> optionsMonitor,
        IConsulServiceDiscovery serviceDiscovery,
        ILogger<BffServiceResolver> logger)
    {
        _optionsMonitor = optionsMonitor ?? throw new ArgumentNullException(nameof(optionsMonitor));
        _serviceDiscovery = serviceDiscovery ?? throw new ArgumentNullException(nameof(serviceDiscovery));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<string> ResolveBaseAddressAsync(string serviceName, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(serviceName))
        {
            throw new ArgumentException("服务名不能为空。", nameof(serviceName));
        }

        var endpoint = FindEndpoint(_optionsMonitor.CurrentValue, serviceName);
        if (endpoint is null)
        {
            _logger.LogError(
                "BFF 寻址失败：服务 {ServiceName} 未在 Bff:Services 中登记", serviceName);
            throw new BffServiceUnavailableException(
                serviceName,
                string.Empty,
                $"服务 '{serviceName}' 未在 Bff:Services 中登记，请按 deploy/services.manifest.json 补齐配置。");
        }

        var consulServiceName = endpoint.ConsulServiceName ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(endpoint.BaseUrl))
        {
            return NormalizeExplicitBaseUrl(serviceName, consulServiceName, endpoint.BaseUrl);
        }

        if (string.IsNullOrWhiteSpace(consulServiceName))
        {
            _logger.LogError(
                "BFF 寻址失败：服务 {ServiceName} 既未配置 BaseUrl 也未配置 ConsulServiceName", serviceName);
            throw new BffServiceUnavailableException(
                serviceName,
                string.Empty,
                $"服务 '{serviceName}' 既未配置 BaseUrl 也未配置 ConsulServiceName，无法寻址。");
        }

        // 查询异常与无健康实例在 ConsulServiceDiscovery 中统一返回空列表，此处按不可用处理
        var instances = await _serviceDiscovery
            .GetHealthyInstancesAsync(consulServiceName, ct)
            .ConfigureAwait(false);

        if (instances.Count == 0)
        {
            _logger.LogError(
                "BFF 寻址失败：Consul 服务 {ConsulServiceName}（清单服务 {ServiceName}）无健康实例",
                consulServiceName, serviceName);
            throw new BffServiceUnavailableException(
                serviceName,
                consulServiceName,
                $"Consul 服务 '{consulServiceName}' 无健康实例，下游不可用。");
        }

        var instance = instances[0];
        var baseAddress = $"http://{instance.Address}:{instance.Port}";
        _logger.LogDebug(
            "BFF 寻址命中：服务 {ServiceName} → {BaseAddress}（Consul {ConsulServiceName}，实例 {InstanceId}）",
            serviceName, baseAddress, consulServiceName, instance.Id);
        return baseAddress;
    }

    /// <summary>
    /// 按配置键查找服务条目（大小写不敏感，避免绑定器键名大小写差异导致误判）。
    /// </summary>
    private static BffServiceEndpoint? FindEndpoint(BffOptions options, string serviceName)
    {
        foreach (var pair in options.Services)
        {
            if (string.Equals(pair.Key, serviceName, StringComparison.OrdinalIgnoreCase))
            {
                return pair.Value;
            }
        }

        return null;
    }

    /// <summary>
    /// 校验显式基址为 http/https 绝对地址，并去掉尾部斜杠，保证与路径拼接不产生双斜杠。
    /// </summary>
    private static string NormalizeExplicitBaseUrl(
        string serviceName,
        string consulServiceName,
        string rawBaseUrl)
    {
        var trimmed = rawBaseUrl.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new BffServiceUnavailableException(
                serviceName,
                consulServiceName,
                $"服务 '{serviceName}' 的 Bff:Services:BaseUrl 不是合法的 http/https 绝对地址：'{rawBaseUrl}'。");
        }

        return trimmed.TrimEnd('/');
    }
}
