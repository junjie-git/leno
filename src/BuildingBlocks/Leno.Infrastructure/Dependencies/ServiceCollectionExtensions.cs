using Elastic.Clients.Elasticsearch;
using Leno.Infrastructure.Auth;
using Leno.Infrastructure.EventBus;
using Leno.Infrastructure.HealthChecks;
using Leno.Infrastructure.ReadModel;
using Leno.Infrastructure.Storage;
using Leno.SharedKernel.Abstractions;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace Leno.Infrastructure.Dependencies;

/// <summary>
/// 基础设施 DI 注册入口，统一注册文件存储、JWT、当前用户上下文、事件总线、
/// Redis、Elasticsearch 读模型仓储与健康检查。
/// 业务上下文 Presentation 层在 Program.cs 调用 <c>services.AddLenoInfrastructure(configuration)</c>。
/// </summary>
public static class ServiceCollectionExtensions
{
    private static readonly string[] ReadyTags = { "ready" };

    /// <summary>
    /// 注册 Leno 基础设施全部服务。
    /// </summary>
    /// <param name="configureConsumers">MassTransit 消费者注册回调，业务上下文在此注册集成事件消费者。</param>
    public static IServiceCollection AddLenoInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<IBusRegistrationConfigurator>? configureConsumers = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        AddOptions(services, configuration);
        AddFileStorage(services, configuration);
        AddAuth(services);
        AddRedis(services, configuration);
        AddElasticsearch(services, configuration);
        AddEventBus(services, configuration, configureConsumers);
        AddHealthChecks(services);

        return services;
    }

    private static void AddOptions(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<FileStorageOptions>(configuration.GetSection("FileStorage"));
        services.Configure<LocalStorageOptions>(configuration.GetSection("FileStorage:Local"));
        services.Configure<ObjectStorageOptions>(configuration.GetSection("FileStorage:ObjectStorage"));
        services.Configure<JwtOptions>(configuration.GetSection("Jwt"));
    }

    private static void AddFileStorage(IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration["FileStorage:Provider"] ?? "Local";
        if (string.Equals(provider, "Local", StringComparison.OrdinalIgnoreCase))
        {
            services.AddScoped<IFileStorageService, LocalFileStorageService>();
        }
        // 对象存储（MinIO/OSS）适配器预留，后续按需注册
    }

    private static void AddAuth(IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddSingleton<JwtTokenGenerator>();
        services.AddScoped<ICurrentUserContext, CurrentUserContext>();
    }

    private static void AddRedis(IServiceCollection services, IConfiguration configuration)
    {
        var redisConfig = configuration["Redis:Configuration"] ?? "localhost:6379";
        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConfig));
    }

    private static void AddElasticsearch(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<ElasticsearchClient>(_ =>
        {
            var uri = configuration.GetConnectionString("ReadDb")
                      ?? configuration["Elasticsearch:Uri"]
                      ?? "http://localhost:9200";
            var settings = new ElasticsearchClientSettings(new Uri(uri));
            return new ElasticsearchClient(settings);
        });

        services.AddScoped(typeof(IEsReadModelRepository<>), typeof(EsReadModelRepository<>));
    }

    private static void AddEventBus(
        IServiceCollection services,
        IConfiguration configuration,
        Action<IBusRegistrationConfigurator>? configureConsumers)
    {
        services.AddScoped<IEventBus, RabbitMqEventBus>();

        services.AddMassTransit(cfg =>
        {
            configureConsumers?.Invoke(cfg);

            cfg.UsingRabbitMq((context, rabbitCfg) =>
            {
                var host = configuration["RabbitMQ:Host"] ?? "localhost";
                var port = int.TryParse(configuration["RabbitMQ:Port"], out var p) ? p : 5672;
                var username = configuration["RabbitMQ:Username"] ?? "guest";
                var password = configuration["RabbitMQ:Password"] ?? "guest";
                var virtualHost = configuration["RabbitMQ:VirtualHost"] ?? "/";

                var vhostPath = virtualHost.StartsWith('/') ? virtualHost : "/" + virtualHost;
                var hostAddress = new Uri($"rabbitmq://{host}:{port}{vhostPath}");

                rabbitCfg.Host(hostAddress, h =>
                {
                    h.Username(username);
                    h.Password(password);
                });

                // 指数退避重试，重试耗尽进入死信队列
                rabbitCfg.UseMessageRetry(r => r.Incremental(5, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30)));

                rabbitCfg.ConfigureEndpoints(context);
            });
        });
    }

    private static void AddHealthChecks(IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck<RedisHealthCheck>("redis", tags: ReadyTags)
            .AddCheck<ElasticsearchHealthCheck>("elasticsearch", tags: ReadyTags);
    }
}
