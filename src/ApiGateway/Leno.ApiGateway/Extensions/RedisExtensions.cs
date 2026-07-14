using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Leno.ApiGateway.Extensions;

/// <summary>
/// 网关 Redis 注册扩展。
/// 单独注册 <see cref="IConnectionMultiplexer"/> 用于限流计数与（后续阶段）黑名单同步，
/// 不依赖 Leno.Infrastructure 的 <c>AddLenoInfrastructure</c>（网关不需要 MassTransit/Elasticsearch）。
/// </summary>
public static class RedisExtensions
{
    /// <summary>
    /// 注册 <see cref="IConnectionMultiplexer"/> 与 <see cref="IDatabase"/>，
    /// 从 <c>Redis:Configuration</c> 配置读取连接字符串。
    /// </summary>
    public static IServiceCollection AddGatewayRedis(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var connectionString = configuration["Redis:Configuration"]
                ?? configuration.GetConnectionString("Redis")
                ?? "localhost:6379";

            var configurationOptions = ConfigurationOptions.Parse(connectionString);
            configurationOptions.AbortOnConnectFail = false; // 容错：Redis 不可用时网关仍可降级
            return ConnectionMultiplexer.Connect(configurationOptions);
        });

        services.AddScoped<IDatabase>(sp =>
        {
            var multiplexer = sp.GetRequiredService<IConnectionMultiplexer>();
            return multiplexer.GetDatabase();
        });

        return services;
    }
}
