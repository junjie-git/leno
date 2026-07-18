using System.Text;
using Leno.Infrastructure.Auth;
using Leno.Infrastructure.HealthChecks;
using Leno.Infrastructure.Logging;
using Leno.Infrastructure.Middleware;
using MassTransit;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
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
    /// BC 专属基础设施回调 + 健康检查（含 DbContext 探活）+ MVC Controllers + OpenAPI +
    /// JwtBearer/GatewayHeader 双模式鉴权 + 授权。
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
        Action<IServiceCollection>? configureInfrastructure = null)
        where TDbContext : Microsoft.EntityFrameworkCore.DbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

        // 1. 共享内核基础设施：JWT 生成器、当前用户上下文、事件总线、Redis、ES、健康检查
        services.AddLenoInfrastructure(configuration, configureConsumers);

        // 2. 内部服务间 API Key 鉴权（保护 internal/ 前缀路由）
        services.AddInternalApiKeyAuth(configuration);

        // 3. BC 专属基础设施回调（DbContext、工作单元、仓储、应用服务等）
        configureInfrastructure?.Invoke(services);

        // 4. 健康检查：self + Redis + ES + SqlServer + RabbitMQ + DbContext 探活
        services.AddLenoHealthChecks<TDbContext>(configuration);

        // 5. MVC Controllers
        services.AddControllers();

        // 6. OpenAPI
        services.AddOpenApi();

        // 7. 鉴权配置：支持 JwtBearer 与 GatewayHeader 两种模式，按 Auth:Mode 灰度切换
        var authMode = configuration["Auth:Mode"] ?? "JwtBearer";
        services.AddAuthentication(authMode == "GatewayHeader"
            ? "GatewayHeader"
            : JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                var jwtOpts = configuration.GetSection("Jwt").Get<JwtOptions>()
                    ?? throw new InvalidOperationException("Jwt 配置节缺失");
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtOpts.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtOpts.Audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOpts.SecretKey)),
                    ClockSkew = TimeSpan.FromMinutes(1)
                };
            })
            .AddScheme<GatewayAuthOptions, GatewayAuthHandler>("GatewayHeader", _ => { });

        // 8. 授权
        services.AddAuthorization();

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
    /// 认证 + 授权 + 启动时校验内部 API Key + 健康检查端点 + 控制器路由映射。
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

        // 3. 内部 API Key 鉴权中间件（校验 internal/ 前缀路由）
        app.UseMiddleware<InternalApiKeyMiddleware>();

        // 4. 认证
        app.UseAuthentication();

        // 5. 授权
        app.UseAuthorization();

        // 6. 启动时校验内部 API Key 配置（非开发环境缺失则抛异常阻止启动）
        app.EnsureInternalApiKeyConfigured();

        // 7. 健康检查端点（/health/live、/health/ready、/health）
        app.MapLenoHealthChecks();

        // 8. 控制器路由映射
        app.MapControllers();

        return app;
    }
}
