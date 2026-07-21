using Elastic.Clients.Elasticsearch;
using Leno.Infrastructure.Auth;
using Leno.Infrastructure.EventBus;
using Leno.Infrastructure.HealthChecks;
using Leno.Infrastructure.Middleware;
using Leno.Infrastructure.ReadModel;
using Leno.Infrastructure.Storage;
using Leno.SharedKernel.Abstractions;
using Leno.Infrastructure.Abstractions;
using MassTransit;
using Medallion.Threading;
using Medallion.Threading.Redis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;
using System.Globalization;

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

        RegisterSpecialErrorCodes();
        AddOptions(services, configuration);
        AddFileStorage(services, configuration);
        AddAuth(services);
        AddRedis(services, configuration);
        // 默认注册空翻译器，各 BC 在 AddXxxInfrastructure 中覆盖为具体实现
        services.AddSingleton<IIntegrationEventMapper, NullIntegrationEventMapper>();
        AddElasticsearch(services, configuration);
        AddEventBus(services, configuration, configureConsumers);
        AddHealthChecks(services, configuration);

        return services;
    }

    /// <summary>
    /// 注册内部服务间鉴权（X-Internal-Key 头部校验），保护 internal/ 前缀路由。
    /// </summary>
    public static IServiceCollection AddInternalApiKeyAuth(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<InternalApiKeyOptions>(configuration.GetSection(InternalApiKeyOptions.SectionName));
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
        if (string.Equals(provider, "MinIO", StringComparison.OrdinalIgnoreCase))
        {
            services.AddScoped<IFileStorageService, ObjectStorageService>();
        }
        else
        {
            // 默认使用本地文件存储（开发环境）
            services.AddScoped<IFileStorageService, LocalFileStorageService>();
        }
    }

    private static void AddAuth(IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddSingleton<JwtTokenGenerator>();
        services.AddScoped<ICurrentUserContext, CurrentUserContext>();
        services.AddScoped<ResourceOwnershipChecker>();
    }

    private static void AddRedis(IServiceCollection services, IConfiguration configuration)
    {
        var redisConfig = configuration["Redis:Configuration"] ?? "localhost:6379";

        // T21：使用 Lazy<Task<IConnectionMultiplexer>> 延迟异步连接，避免应用启动时同步阻塞主线程。
        // LazyThreadSafetyMode.ExecutionAndPublication 保证多线程下仅初始化一次。
        // ConnectionMultiplexer.ConnectAsync 在首次解析 IConnectionMultiplexer 时触发（DI 工厂内 GetAwaiter().GetResult()），
        // 相比原 ConnectionMultiplexer.Connect 同步阻塞注册阶段，连接推迟到首次实际使用。
        var lazyMultiplexer = new Lazy<Task<IConnectionMultiplexer>>(
            () => ConnectionMultiplexer.ConnectAsync(redisConfig),
            LazyThreadSafetyMode.ExecutionAndPublication);

        services.AddSingleton<IConnectionMultiplexer>(_ => lazyMultiplexer.Value.GetAwaiter().GetResult());
        // 集成事件消费幂等去重存储，基于 Redis SET NX + 24h TTL
        services.AddSingleton<IIdempotencyStore, RedisIdempotencyStore>();
        // 数据库迁移分布式锁提供者（基于 Redis SET NX EX，DistributedLock.Redis 实现）
        // 从 DI 解析 IConnectionMultiplexer（触发 Lazy 初始化），而非捕获局部变量
        services.AddSingleton<IDistributedLockProvider>(sp =>
        {
            var multiplexer = sp.GetRequiredService<IConnectionMultiplexer>();
            return new RedisDistributedSynchronizationProvider(multiplexer.GetDatabase());
        });
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

                // 重试策略：从 MassTransit:Retry 配置节读取，未配置时使用默认值 5 次/10s 初始/30s 递增（向后兼容）
                // 重试耗尽后由 MassTransit 转入死信队列
                // 配置 Interval 时同时用作初始间隔和递增量（符合设计 spec 的 5s/10s/15s 模式）
                var retrySection = configuration.GetSection("MassTransit:Retry");
                var retryCount = retrySection.GetValue<int?>("Count") ?? 5;
                var isIncremental = retrySection.GetValue<bool?>("Incremental") ?? true;

                TimeSpan initialInterval;
                TimeSpan intervalIncrement;
                var intervalStr = retrySection["Interval"];
                if (TimeSpan.TryParse(intervalStr, CultureInfo.InvariantCulture, out var parsedInterval))
                {
                    // 配置了 Interval：同时作为初始间隔和递增量（符合 spec 的 5s/10s/15s 模式）
                    initialInterval = parsedInterval;
                    intervalIncrement = parsedInterval;
                }
                else
                {
                    // 未配置 Interval：使用既有默认值 10s 初始 + 30s 递增（向后兼容其他服务）
                    initialInterval = TimeSpan.FromSeconds(10);
                    intervalIncrement = TimeSpan.FromSeconds(30);
                }

                rabbitCfg.UseMessageRetry(r =>
                {
                    if (isIncremental)
                    {
                        r.Incremental(retryCount, initialInterval, intervalIncrement);
                    }
                    else
                    {
                        r.Intervals(initialInterval);
                    }
                });

                rabbitCfg.ConfigureEndpoints(context);
            });
        });
    }

    private static void AddHealthChecks(IServiceCollection services, IConfiguration configuration)
    {
        var builder = services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), tags: Array.Empty<string>())
            .AddCheck<RedisHealthCheck>("redis", tags: ReadyTags)
            .AddCheck<ElasticsearchHealthCheck>("elasticsearch", tags: ReadyTags);

        // T20：基础路径也注册 RabbitMQ 健康检查，确保仅调用 AddLenoInfrastructure（不走 AddLenoFullHealthChecks）
        // 的场景下 RabbitMQ 依赖也被纳入就绪探针。
        // 与 HealthChecksUIExtensions.AddLenoHealthChecks 保持一致：仅当 RabbitMQ:Host 配置时注册。
        var rabbitHost = configuration["RabbitMQ:Host"];
        if (!string.IsNullOrWhiteSpace(rabbitHost))
        {
            var rabbitPort = configuration["RabbitMQ:Port"] ?? "5672";
            var rabbitConnectionString = $"amqp://{configuration["RabbitMQ:Username"] ?? "guest"}:{configuration["RabbitMQ:Password"] ?? "guest"}@{rabbitHost}:{rabbitPort}";
            builder.AddRabbitMQ(rabbitConnectionString, name: "rabbitmq", tags: ReadyTags);
        }
    }

    /// <summary>
    /// 使用 NuGet 健康检查包添加完整的依赖健康检查（DB、Redis、ES、RabbitMQ）。
    /// 在 Program.cs 中 <c>services.AddLenoHealthChecks(configuration)</c> 后调用此方法映射端点。
    /// </summary>
    public static IServiceCollection AddLenoFullHealthChecks(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        HealthChecksUIExtensions.AddLenoHealthChecks(services, configuration);
        return services;
    }

    /// <summary>
    /// 注册不遵循后缀约定的特殊 ErrorCode 到 HTTP 状态码映射。
    /// 这些 ErrorCode 的实际 HTTP 语义与后缀约定不符（如 USER_DISABLED→403 而非 400）。
    /// </summary>
    private static void RegisterSpecialErrorCodes()
    {
        ErrorCodeMapping.RegisterAll(
            // 409 Conflict（状态冲突，但 ErrorCode 后缀不匹配 _ALREADY_/_EXISTS_/_CONFLICT）
            ("USER_DISABLE_SELF", 409),
            ("USER_NOT_SUSPENDED", 409),
            ("USER_REVOKE_ADMIN_SELF", 409),
            ("USER_LAST_ROLE", 409),
            ("EXTERNAL_LOGIN_LAST", 409),
            ("CART_VARIETY_LIMIT", 409),
            ("SELLER_APPROVED", 409),
            ("SHOP_CLOSED", 409),
            ("ADDRESS_ALREADY_DELETED", 409),
            ("ADDRESS_NOT_ACTIVE", 409),
            ("USER_USERNAME_CONFLICT", 409),
            // 400 Bad Request（CART_ANONYMOUS_ID_REQUIRED 匹配 _REQUIRED→401，但业务语义是参数缺失→400）
            ("CART_ANONYMOUS_ID_REQUIRED", 400),
            // 403 Forbidden（USER_DISABLED 是禁用而非校验失败）
            ("USER_DISABLED", 403),
            // 500 Internal Server Error（USER_2FA_SECRET_MISSING 已匹配 _MISSING，但显式注册以防后缀变更）
            ("USER_2FA_SECRET_MISSING", 500),
            // 401 Unauthorized（_INVALID 默认 400，需显式 401）
            ("USER_OLD_PASSWORD_INVALID", 401),
            ("USER_2FA_CODE_INVALID", 401),
            ("USER_2FA_TEMP_TOKEN_INVALID", 401),
            ("USER_RESET_TOKEN_INVALID", 401));
    }
}
