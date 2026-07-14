using Consul;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Leno.Infrastructure.ServiceDiscovery;

/// <summary>
/// Consul 服务注册配置选项。
/// </summary>
public sealed class ConsulRegistrationOptions
{
    /// <summary>Consul 中注册的服务名（如 <c>leno-product-api</c>）。</summary>
    public string ServiceName { get; set; } = default!;

    /// <summary>服务实例唯一 ID（如 <c>leno-product-api-instance-1</c>）。</summary>
    public string ServiceId { get; set; } = default!;

    /// <summary>服务实例可达地址（IP 或主机名）。</summary>
    public string Address { get; set; } = default!;

    /// <summary>服务实例端口。</summary>
    public int Port { get; set; }

    /// <summary>健康检查路径（Consul 将定期 HTTP 探测此路径）。</summary>
    public string HealthCheckPath { get; set; } = "/health/live";

    /// <summary>服务标签列表（可用于灰度路由等）。</summary>
    public string[] Tags { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Consul 服务注册托管服务：应用启动时注册实例，关闭时注销。
/// </summary>
public sealed class ConsulServiceRegistrationHostedService : IHostedService, IAsyncDisposable
{
    private readonly IConsulClient _consulClient;
    private readonly ConsulRegistrationOptions _options;
    private readonly ILogger<ConsulServiceRegistrationHostedService> _logger;

    public ConsulServiceRegistrationHostedService(
        IConsulClient consulClient,
        ConsulRegistrationOptions options,
        ILogger<ConsulServiceRegistrationHostedService> logger)
    {
        _consulClient = consulClient ?? throw new ArgumentNullException(nameof(consulClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var registration = new AgentServiceRegistration
        {
            ID = _options.ServiceId,
            Name = _options.ServiceName,
            Address = _options.Address,
            Port = _options.Port,
            Tags = _options.Tags,
            Check = new AgentServiceCheck
            {
                HTTP = $"http://{_options.Address}:{_options.Port}{_options.HealthCheckPath}",
                Interval = TimeSpan.FromSeconds(10),
                DeregisterCriticalServiceAfter = TimeSpan.FromMinutes(1)
            }
        };

        try
        {
            await _consulClient.Agent.ServiceRegister(registration, cancellationToken);

            _logger.LogInformation(
                "Registered service {ServiceName} (ID: {ServiceId}) with Consul at {Address}:{Port}",
                _options.ServiceName, _options.ServiceId, _options.Address, _options.Port);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Consul 不可达时不阻止应用启动，服务将在下次健康检查时被重新发现
            _logger.LogWarning(ex,
                "Failed to register service {ServiceName} (ID: {ServiceId}) with Consul at {Address}:{Port}. " +
                "Application will continue startup; service may be unreachable until Consul recovers.",
                _options.ServiceName, _options.ServiceId, _options.Address, _options.Port);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _consulClient.Agent.ServiceDeregister(_options.ServiceId, cancellationToken);
            _logger.LogInformation(
                "Deregistered service {ServiceName} (ID: {ServiceId}) from Consul",
                _options.ServiceName, _options.ServiceId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex,
                "Failed to deregister service {ServiceId} from Consul", _options.ServiceId);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_consulClient is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }
        else
        {
            _consulClient.Dispose();
        }
    }
}

/// <summary>
/// 微服务 Consul 注册扩展方法。
/// 在微服务 Program.cs 中调用 <c>builder.AddConsulServiceRegistration("leno-product-api", opts => {...})</c>
/// 即可在启动时注册到 Consul，关闭时自动注销。
/// </summary>
public static class ConsulServiceRegistrationExtensions
{
    /// <summary>
    /// 注册 Consul 客户端与服务注册托管服务。
    /// </summary>
    /// <param name="builder">应用构建器。</param>
    /// <param name="serviceName">Consul 中注册的服务名（如 <c>leno-product-api</c>）。</param>
    /// <param name="configure">可选回调，用于覆盖默认注册参数（Address、Port、Tags 等）。</param>
    public static IHostApplicationBuilder AddConsulServiceRegistration(
        this IHostApplicationBuilder builder,
        string serviceName,
        Action<ConsulRegistrationOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(serviceName);

        var consulUrl = builder.Configuration["Consul:Url"] ?? "http://localhost:8500";
        var consulToken = builder.Configuration["Consul:Token"] ?? string.Empty;

        builder.Services.AddSingleton<IConsulClient>(sp =>
        {
            return new ConsulClient(c =>
            {
                c.Address = new Uri(consulUrl);
                if (!string.IsNullOrEmpty(consulToken))
                {
                    c.Token = consulToken;
                }
            });
        });

        var options = new ConsulRegistrationOptions
        {
            ServiceName = serviceName,
            ServiceId = $"{serviceName}-{Environment.MachineName}-{builder.Configuration["Consul:ServicePort"] ?? "8080"}",
            Address = builder.Configuration["Consul:ServiceAddress"] ?? Environment.MachineName,
            Port = int.TryParse(builder.Configuration["Consul:ServicePort"], out var port) ? port : 8080
        };

        configure?.Invoke(options);

        builder.Services.AddSingleton(options);
        builder.Services.AddHostedService<ConsulServiceRegistrationHostedService>();

        return builder;
    }
}
