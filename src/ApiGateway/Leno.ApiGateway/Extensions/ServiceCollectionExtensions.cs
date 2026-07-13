using Consul;
using Leno.ApiGateway.Options;
using Leno.ApiGateway.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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

    // Task 3 Step 5 将在此处追加 AddConsulDestinationResolver 方法
}
