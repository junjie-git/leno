using System.Text;
using Leno.Infrastructure.AntiCorruption;
using Leno.Infrastructure.Auth;
using Leno.Infrastructure.Configuration;
using Leno.Infrastructure.HealthChecks;
using Leno.Infrastructure.Logging;
using Leno.Infrastructure.Middleware;
using Leno.Infrastructure.Outbox;
using MassTransit;
using Quartz;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Prometheus;
using Serilog;

namespace Leno.Infrastructure.Dependencies;

/// <summary>
/// WebApplication 一站式扩展，封装 11 个业务上下文 Program.cs 中高度同构的
/// 服务注册（<see cref="AddLenoApi{TDbContext}"/>）、Serilog 配置（<see cref="UseLenoSerilog"/>）
/// 与中间件管线（<see cref="UseLenoPipeline"/>），消除约 880 行重复样板。
/// </summary>
public static class WebApplicationExtensions
{
    /// <summary>
    /// 一站式注册 Leno BC 的全部服务：共享内核基础设施 + 内部 API Key 鉴权 +
    /// BC 专属基础设施回调 + Outbox 分片发布器 + 健康检查（含 DbContext 探活）+ MVC Controllers + OpenAPI +
    /// JwtBearer 鉴权（RS256/JWKS；GatewayHeader 透传模式已随 A6 删除）+ 授权。
    /// </summary>
    /// <typeparam name="TDbContext">BC 的 EF Core DbContext 类型，用于健康检查探活。</typeparam>
    /// <param name="services">服务集合。</param>
    /// <param name="configuration">应用配置，用于读取 Jwt/Auth/Redis/Elasticsearch 等节。</param>
    /// <param name="serviceName">服务名称，作为标识保留，未来可用于 OpenAPI 文档标题等。</param>
    /// <param name="configureConsumers">MassTransit 消费者注册回调，BC 在此注册集成事件消费者。</param>
    /// <param name="configureInfrastructure">BC 专属基础设施注册回调（如 AddOrderInfrastructure）。</param>
    /// <returns>服务集合，便于链式调用。</returns>
    /// <example>
    /// <code>
    /// builder.Services.AddLenoApi&lt;OrderDbContext&gt;(
    ///     builder.Configuration,
    ///     "leno-order-api",
    ///     cfg => cfg.AddOrderConsumers(),
    ///     services => services.AddOrderInfrastructure(builder.Configuration));
    /// </code>
    /// </example>
    public static IServiceCollection AddLenoApi<TDbContext>(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceName,
        Action<IBusRegistrationConfigurator>? configureConsumers = null,
        Action<IServiceCollection>? configureInfrastructure = null,
        Action<IServiceCollectionQuartzConfigurator>? configureScheduler = null)
        where TDbContext : Microsoft.EntityFrameworkCore.DbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

        // 0. 启动 fail-fast 配置校验（P0-C）：必须最先注册，保证先于 MassTransit/Consul 监听等
        //    托管服务启动；非 Development 环境下关键配置（本 BC ConnectionStrings:{Bc}Db、
        //    Redis:Configuration、RabbitMQ:Host）缺失或仍为 localhost 默认值时拒绝启动。
        services.AddLenoStartupConfigurationValidation(configuration, serviceName);

        // 1. 共享内核基础设施：JWT 生成器、当前用户上下文、事件总线、Redis、ES、健康检查
        services.AddLenoInfrastructure(configuration, configureConsumers, configureScheduler);

        // 1.1 防腐层 HttpClient Polly 策略（重试/熔断/超时，由各 BC AddHttpClient 链式追加）
        services.AddLenoAntiCorruptionPolly(configuration);

        // 1.1.1 防腐层 gRPC Polly 重试策略（P1-B.1：临时性故障自动重试 2 次）
        //       由 GrpcAntiCorruptionClientBase.ExecuteAsync 内嵌使用
        services.AddLenoGrpcAntiCorruptionPolly(configuration);

        // 1.2 防腐层 gRPC 灰度开关（M4.3 + M4 双轨方案）：默认 false 走 HTTP，true 走 gRPC
        // 各 BC 在 configureInfrastructure 委托中按 UseGrpc 注册具体 gRPC 客户端/服务
        services.Configure<AntiCorruptionOptions>(configuration.GetSection("AntiCorruption"));
        var antiCorruptionOptions = configuration.GetSection("AntiCorruption").Get<AntiCorruptionOptions>() ?? new AntiCorruptionOptions();

        // T19：注册可重载的 Consul 配置提供者，使 IOptionsMonitor<AntiCorruptionOptions> 感知 KV 热更新。
        // provider 作为单例同时注册到 DI（ConsulConfigWatcher 注入）与配置链（IOptionsMonitor 绑定源）。
        // 加在链尾，确保 Consul KV 值覆盖 appsettings.json 等静态源。
        var consulConfigProvider = new ConsulReloadableConfigurationProvider();
        services.AddSingleton(consulConfigProvider);
        if (configuration is IConfigurationBuilder configBuilder)
        {
            configBuilder.Add(new ConsulReloadableConfigurationSource(consulConfigProvider));
        }

        // 初始化 AntiCorruptionMetrics 的 ObservableGauge（幂等，重复调用安全）
        AntiCorruptionMetrics.Initialize();

        if (antiCorruptionOptions.UseGrpc)
        {
            // gRPC 模式：注册公共 gRPC 服务端基础设施 + InternalKey 鉴权拦截器
            services.AddSingleton<GrpcInternalKeyInterceptor>();
            services.AddGrpc(opts =>
            {
                opts.EnableDetailedErrors = false;
                opts.Interceptors.Add<GrpcInternalKeyInterceptor>();
            });
        }

        // 2. 内部服务间 API Key 鉴权（保护 internal/ 前缀路由）
        services.AddInternalApiKeyAuth(configuration);

        // 3. BC 专属基础设施回调（DbContext、工作单元、仓储、应用服务等）
        configureInfrastructure?.Invoke(services);

        // 3.1 Outbox 分片发布器（4.4）：统一以 TDbContext 为发件箱载体注册后台发布器。
        //     在组合根集中注册，保证每个 BC 的 outbox_messages 都有宿主进程搬运 ——
        //     否则域事件虽已在 SaveChangesWithOutboxAsync 中同事务落库，却永远停留在 Pending，
        //     跨 BC 异步链路会静默失效。
        //     默认 ShardCount=1 / ShardId=0（单实例）；多实例部署通过 Outbox:Sharding 配置节
        //     或环境变量 OUTBOX__SHARDING__SHARD_ID / OUTBOX__SHARDING__SHARD_COUNT 覆盖。
        services.AddShardedOutboxPublisher<TDbContext>(configuration);

        // 4. 健康检查：self + Redis + ES + SqlServer + RabbitMQ + DbContext 探活
        services.AddLenoHealthChecks<TDbContext>(configuration);

        // 5. MVC Controllers
        services.AddControllers();

        // 6. OpenAPI
        services.AddOpenApi();

        // 7. 鉴权配置（RS256-only，零信任；双轨下线 A6，2026-09-23，D-4/D-6）：
        //    - 删除 Auth:Mode 与 GatewayHeader 透传模式：每个服务自行验签 JWT，不信任上游注入的身份头
        //    - 删除 HS256 共享密钥验签（Jwt:SecretKey）：统一从 Identity 的 OIDC 发现文档拉取 JWKS 公钥
        var jwtOpts = configuration.GetSection("Jwt").Get<JwtOptions>()
            ?? throw new InvalidOperationException("Jwt 配置节缺失");
        if (string.IsNullOrWhiteSpace(jwtOpts.DiscoveryUrl))
        {
            throw new InvalidOperationException(
                "Jwt:DiscoveryUrl 配置缺失。RS256 验签需指向 Identity 的 OIDC 发现文档" +
                "（如 http://leno-identity-api:8080/.well-known/openid-configuration）。");
        }

        // 生产环境门禁（P2 改进）：由 LenoStartupConfigurationValidator 宿主服务统一执行
        // （其注入 IHostEnvironment 并天然跳过 Development/Testing，环境判定比配置键更可靠）。

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.MetadataAddress = jwtOpts.DiscoveryUrl;
                options.RequireHttpsMetadata = jwtOpts.RequireHttpsMetadata;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtOpts.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtOpts.Audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    // 签名公钥由 MetadataAddress 指向的发现文档 → jwks_uri 自动拉取并缓存（RS256）
                    ClockSkew = TimeSpan.FromSeconds(30)
                };

                // JWKS 缓存与刷新调优（P1 改进，配合密钥轮换 runbook）：
                // - 自动刷新 1h（默认 12h）：密钥轮换后公钥滞后窗口的上限
                // - 拉取失败 30s 重试（默认 5min）
                options.AutomaticRefreshInterval = TimeSpan.FromHours(1);
                options.RefreshInterval = TimeSpan.FromSeconds(30);

                options.Events = new JwtBearerEvents
                {
                    OnAuthenticationFailed = context =>
                    {
                        // kid 未命中（轮换后本地缓存仍是旧公钥）：请求即时刷新，下一次请求用新公钥
                        if (context.Exception is SecurityTokenSignatureKeyNotFoundException)
                        {
                            context.Options.ConfigurationManager?.RequestRefresh();
                        }
                        return Task.CompletedTask;
                    }
                };
            });

        // 8. 授权
        services.AddAuthorization();

        // 9. Consul KV 配置热更新后台服务（M4 双轨方案：监听 leno/anticorruption/use-grpc/{bc} KV）
        // 仅当 AntiCorruption:EnableConsulConfigWatcher=true（默认 true）时注册
        if (configuration.GetValue<bool>("AntiCorruption:EnableConsulConfigWatcher", true))
        {
            services.AddHostedService<ConsulConfigWatcher>();
        }

        return services;
    }

    /// <summary>
    /// 配置 Serilog 结构化日志（JSON 输出 + Application/Environment/TraceId 富化），
    /// 从 <c>Application:Name</c> 读取应用名，缺失时回退到 <paramref name="serviceName"/>。
    /// </summary>
    /// <param name="hostBuilder">主机构建器。</param>
    /// <param name="configuration">应用配置（校验非空；实际读取在 Serilog 配置委托内通过 context.Configuration）。</param>
    /// <param name="serviceName">服务名称，作为 Application 富化兜底默认值。</param>
    /// <returns>主机构建器，便于链式调用。</returns>
    public static IHostBuilder UseLenoSerilog(this IHostBuilder hostBuilder, IConfiguration configuration, string serviceName)
    {
        ArgumentNullException.ThrowIfNull(hostBuilder);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

        hostBuilder.UseSerilog((context, _, serilogConfig) =>
        {
            var appName = context.Configuration["Application:Name"] ?? serviceName;
            SerilogConfig.ConfigureDefaults(
                serilogConfig, appName, context.HostingEnvironment.EnvironmentName)
                .ReadFrom.Configuration(context.Configuration.GetSection("Serilog"));
        });

        return hostBuilder;
    }

    /// <summary>
    /// 一站式配置 Leno BC 的中间件管线：开发环境 OpenAPI + 全局异常 + 内部 API Key 中间件 +
    /// 认证 + 授权 + 启动时校验内部 API Key + Prometheus /metrics 端点 + 健康检查端点 + 控制器路由映射。
    /// </summary>
    /// <remarks>
    /// 不包含 <c>MigrateWithLockAsync&lt;TDbContext&gt;</c>（各 BC 因 TDbContext 类型不同需自行调用）；
    /// 不包含 <c>AuditLogMiddleware</c>（UserAuth BC 专属，在该 BC 调用 UseLenoPipeline 后自行 UseMiddleware）。
    /// </remarks>
    /// <param name="app">WebApplication 实例。</param>
    /// <returns>WebApplication 实例，便于链式调用。</returns>
    public static WebApplication UseLenoPipeline(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        // 1. 开发环境映射 OpenAPI
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        // 2. 全局异常处理（领域异常 → HTTP 状态码映射）
        app.UseMiddleware<GlobalExceptionMiddleware>();

        // 2.1 内部路由旧前缀探测器已于 2026-09-23 移除（双轨下线 C2 收口）：
        //     10 处旧路由已按"立即删除"口径清理，无观察期残留。

        // 3. 内部 API Key 鉴权中间件（校验 internal/ 前缀路由）
        app.UseMiddleware<InternalApiKeyMiddleware>();

        // 4. 认证
        app.UseAuthentication();

        // 5. 授权
        app.UseAuthorization();

        // 6. 启动时校验内部 API Key 配置（非开发环境缺失则抛异常阻止启动）
        app.EnsureInternalApiKeyConfigured();

        // 7. 暴露 Prometheus /metrics 端点（M5.1：供 Prometheus 抓取）
        app.UseMetricServer("/metrics");

        // 8. 健康检查端点（/health/live、/health/ready、/health）
        app.MapLenoHealthChecks();

        // 9. 控制器路由映射
        app.MapControllers();

        return app;
    }
}
