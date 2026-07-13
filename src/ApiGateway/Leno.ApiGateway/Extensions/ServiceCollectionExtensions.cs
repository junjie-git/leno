using Consul;
using Leno.ApiGateway.Options;
using Leno.ApiGateway.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Leno.ApiGateway.Extensions;

/// <summary>
/// 网关侧服务注册扩展。
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 注册 Consul 客户端与 <see cref="ConsulServiceDiscovery"/> 服务发现组件。
    /// 从 <c>Consul:Url</c> 和 <c>Consul:Token</c> 配置读取连接信息。
    /// </summary>
    public static IServiceCollection AddConsulServiceDiscovery(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<ConsulOptions>(configuration.GetSection("Consul"));

        services.AddSingleton<IConsulClient>(sp =>
        {
            var consulUrl = configuration["Consul:Url"] ?? "http://localhost:8500";
            var consulToken = configuration["Consul:Token"] ?? string.Empty;

            return new ConsulClient(c =>
            {
                c.Address = new Uri(consulUrl);
                if (!string.IsNullOrEmpty(consulToken))
                {
                    c.Token = consulToken;
                }
            });
        });

        services.AddSingleton<IConsulServiceDiscovery, ConsulServiceDiscovery>();

        return services;
    }

    /// <summary>
    /// 用 <see cref="ConsulDestinationResolver"/> 替换 YARP 默认的
    /// <see cref="Yarp.ReverseProxy.ServiceDiscovery.IDestinationResolver"/>，
    /// 使每个请求经过 Consul 动态解析健康实例。
    /// </summary>
    public static IServiceCollection AddConsulDestinationResolver(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.Replace(
            ServiceDescriptor.Singleton<
                Yarp.ReverseProxy.ServiceDiscovery.IDestinationResolver,
                ConsulDestinationResolver>());

        return services;
    }
}
