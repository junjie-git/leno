using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Leno.Infrastructure.HealthChecks;

/// <summary>
/// HealthChecksUI 扩展，配置健康检查端点（/health/live、/health/ready）与
/// HealthChecksUI 仪表盘，覆盖 DB、Redis、Elasticsearch、RabbitMQ 等依赖。
/// </summary>
public static class HealthChecksUIExtensions
{
    private static readonly string[] LiveTags = [];
    private static readonly string[] ReadyTags = { "ready" };

    /// <summary>
    /// 添加完整的健康检查配置，覆盖 DB、Redis、Elasticsearch、RabbitMQ。
    /// 在 Program.cs 调用 <c>builder.Services.AddLenoHealthChecks(builder.Configuration)</c>。
    /// </summary>
    public static IHealthChecksBuilder AddLenoHealthChecks(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var builder = services.AddHealthChecks();

        // 自检
        builder.AddCheck("self", () => HealthCheckResult.Healthy(), tags: LiveTags);

        // Redis
        var redisConnection = configuration["Redis:Configuration"] ?? "localhost:6379";
        builder.AddRedis(redisConnection, name: "redis", tags: ReadyTags);

        // Elasticsearch
        var esUri = configuration.GetConnectionString("ReadDb")
                    ?? configuration["Elasticsearch:Uri"]
                    ?? "http://localhost:9200";
        builder.AddElasticsearch(esUri, name: "elasticsearch", tags: ReadyTags);

        // SQL Server (可选的 DB 健康检查)
        var dbConnectionString = configuration.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrWhiteSpace(dbConnectionString))
        {
            builder.AddSqlServer(
                dbConnectionString,
                name: "sqlserver",
                tags: ReadyTags);
        }

        // RabbitMQ
        var rabbitHost = configuration["RabbitMQ:Host"];
        if (!string.IsNullOrWhiteSpace(rabbitHost))
        {
            var rabbitPort = configuration["RabbitMQ:Port"] ?? "5672";
            var rabbitConnectionString = $"amqp://{configuration["RabbitMQ:Username"] ?? "guest"}:{configuration["RabbitMQ:Password"] ?? "guest"}@{rabbitHost}:{rabbitPort}";
            builder.AddRabbitMQ(
                rabbitConnectionString,
                name: "rabbitmq",
                tags: ReadyTags);
        }

        return builder;
    }

    /// <summary>
    /// 注册 Leno 全部健康检查（self + Redis + ES + SqlServer + RabbitMQ + DbContext）。
    /// 各 BC 调用 AddLenoApi&lt;TDbContext&gt; 时自动使用此重载。
    /// </summary>
    /// <remarks>
    /// <see cref="WebApplicationExtensions.AddLenoApi{TDbContext}"/> 在调用本方法前已先调用
    /// <c>AddLenoInfrastructure</c>，后者注册了 self/Redis/Elasticsearch/RabbitMQ 基础检查。
    /// 此处仅追加 DbContext 探活，避免重复注册导致
    /// <see cref="DefaultHealthCheckService"/> 校验抛
    /// <c>ArgumentException("Duplicate health checks")</c>。
    /// 独立调用场景（未经过 AddLenoApi/AddLenoInfrastructure）请先调用非泛型
    /// <see cref="AddLenoHealthChecks(IServiceCollection, IConfiguration)"/> 注册基础检查。
    /// </remarks>
    public static IHealthChecksBuilder AddLenoHealthChecks<TDbContext>(
        this IServiceCollection services,
        IConfiguration configuration)
        where TDbContext : Microsoft.EntityFrameworkCore.DbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // AddLenoApi 先调用 AddLenoInfrastructure（已注册 self/Redis/ES/RabbitMQ 基础检查），
        // 此处仅追加 DbContext 探活，避免重复注册。
        var builder = services.AddHealthChecks();
        builder.AddDbContextCheck<TDbContext>(tags: ReadyTags);

        return builder;
    }

    /// <summary>
    /// 映射健康检查端点：/health/live（存活探针）、/health/ready（就绪探针）。
    /// 在 app 构建后调用 <c>app.MapLenoHealthChecks()</c>。
    /// </summary>
    public static IEndpointRouteBuilder MapLenoHealthChecks(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        // 存活探针：仅检查自身，不包含依赖
        endpoints.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = check => !check.Tags.Contains("ready"),
            ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
        });

        // 就绪探针：检查所有依赖（Redis、ES、DB、RabbitMQ）
        endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
            ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
        });

        // 综合健康检查端点（HealthChecksUI 使用）
        endpoints.MapHealthChecks("/health", new HealthCheckOptions
        {
            ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
        });

        return endpoints;
    }

    /// <summary>
    /// 添加 HealthChecksUI 仪表盘服务。
    /// 在 API Gateway 或独立仪表盘服务中调用。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <param name="configuration">配置。</param>
    public static IServiceCollection AddLenoHealthChecksUI(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddHealthChecksUI(setup =>
        {
            setup.SetHeaderText("Leno 健康检查仪表盘");

            // 从配置中读取各服务健康端点
            var endpoints = configuration
                .GetSection("HealthChecksUI:HealthChecks")
                .Get<List<HealthCheckServiceEndpoint>>()
                ?? GetDefaultEndpoints();

            foreach (var endpoint in endpoints)
            {
                setup.AddHealthCheckEndpoint(endpoint.Name, endpoint.Uri);
            }

            // 评估时间间隔
            setup.SetEvaluationTimeInSeconds(
                int.TryParse(configuration["HealthChecksUI:EvaluationTimeInSeconds"], out var evalTime)
                    ? evalTime
                    : 10);

            // 历史记录保留天数
            setup.SetMinimumSecondsBetweenFailureNotifications(
                int.TryParse(configuration["HealthChecksUI:MinimumSecondsBetweenFailureNotifications"], out var minSec)
                    ? minSec
                    : 60);
        })
        .AddInMemoryStorage();

        return services;
    }

    /// <summary>
    /// 映射 HealthChecksUI 仪表盘到 /health-dashboard。
    /// </summary>
    public static IEndpointRouteBuilder MapLenoHealthChecksUI(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapHealthChecksUI(options =>
        {
            options.UIPath = "/health-dashboard";
            options.ApiPath = "/health-dashboard/api";
            options.UseRelativeApiPath = false;
            options.UseRelativeResourcesPath = false;
        });

        return endpoints;
    }

    /// <summary>
    /// 获取默认的服务健康端点列表。
    /// </summary>
    private static List<HealthCheckServiceEndpoint> GetDefaultEndpoints()
    {
        return
        [
            new HealthCheckServiceEndpoint
            {
                Name = "API Gateway",
                Uri = "http://localhost:5000/health"
            },
            new HealthCheckServiceEndpoint
            {
                Name = "Order Service",
                Uri = "http://localhost:5100/health"
            },
            new HealthCheckServiceEndpoint
            {
                Name = "Product Service",
                Uri = "http://localhost:5200/health"
            },
            new HealthCheckServiceEndpoint
            {
                Name = "UserAuth Service",
                Uri = "http://localhost:5300/health"
            },
            new HealthCheckServiceEndpoint
            {
                Name = "Cart Service",
                Uri = "http://localhost:5400/health"
            },
            new HealthCheckServiceEndpoint
            {
                Name = "Payment Service",
                Uri = "http://localhost:5500/health"
            },
            new HealthCheckServiceEndpoint
            {
                Name = "Notification Service",
                Uri = "http://localhost:5600/health"
            }
        ];
    }

    /// <summary>
    /// HealthChecksUI 服务端点配置。
    /// </summary>
    public sealed class HealthCheckServiceEndpoint
    {
        public string Name { get; set; } = string.Empty;
        public string Uri { get; set; } = string.Empty;
    }
}