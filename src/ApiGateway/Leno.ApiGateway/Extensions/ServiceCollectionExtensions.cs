using Consul;
using Leno.ApiGateway.Middleware;
using Leno.ApiGateway.Options;
using Leno.ApiGateway.Services;
using Leno.ApiGateway.Transforms;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Prometheus;
using Serilog;
using Yarp.ReverseProxy.Transforms;

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

    /// <summary>
    /// 注册可观测性三件套：Serilog 结构化日志、OpenTelemetry 分布式追踪、prometheus-net 指标。
    /// 同时注册 <see cref="TracingTransform"/> 到 YARP Transform 管道，并暴露 <see cref="GatewayMetricsService"/> 单例。
    /// <para>
    /// 注意：此方法内部调用 <c>AddReverseProxy().LoadFromConfig().AddTransforms&lt;TracingTransform&gt;()</c>，
    /// 调用方不应再单独调用 <c>AddReverseProxy().LoadFromConfig()</c>。
    /// </para>
    /// </summary>
    public static IServiceCollection AddObservability(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<OpenTelemetryOptions>(configuration.GetSection("OpenTelemetry"));
        services.Configure<MetricsOptions>(configuration.GetSection("Metrics"));

        // ===== Serilog =====
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration.GetSection("Serilog"))
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Service", "leno-api-gateway")
            .CreateLogger();

        services.AddSerilog(Log.Logger, dispose: true);

        // ===== OpenTelemetry =====
        var otelEnabled = configuration.GetValue("OpenTelemetry:Enabled", true);
        if (otelEnabled)
        {
            var serviceName = configuration["OpenTelemetry:ServiceName"] ?? "leno-api-gateway";
            var exporter = configuration["OpenTelemetry:Exporter"] ?? "otlp";
            var endpoint = configuration["OpenTelemetry:Endpoint"] ?? "http://localhost:4317";

            var otelBuilder = services.AddOpenTelemetry()
                .ConfigureResource(r => r.AddService(serviceName))
                .WithTracing(tracing => tracing
                    .AddAspNetCoreInstrumentation(opts =>
                    {
                        opts.Filter = ctx => !ctx.Request.Path.StartsWithSegments("/metrics", StringComparison.OrdinalIgnoreCase)
                                          && !ctx.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase);
                    })
                    .AddHttpClientInstrumentation()
                    .AddSource("Yarp.ReverseProxy"));

            if (exporter.Equals("otlp", StringComparison.OrdinalIgnoreCase))
            {
                otelBuilder.UseOtlpExporter(OtlpExportProtocol.Grpc, new Uri(endpoint));
            }
        }

        // ===== Prometheus Metrics =====
        services.AddSingleton<GatewayMetricsService>();

        // ===== YARP + TracingTransform =====
        // 此处统一注册 YARP，调用方不应再单独调用 AddReverseProxy().LoadFromConfig()
        // TracingTransform 同时实现 ITransformProvider，通过 AddTransforms<T>() 注册到 YARP 管道
        services.AddReverseProxy()
            .LoadFromConfig(configuration.GetSection("ReverseProxy"))
            .AddTransforms<TracingTransform>();

        return services;
    }

    /// <summary>
    /// 注册可观测性中间件管道。调用顺序：
    /// 1. <see cref="AccessLoggingMiddleware"/> (访问日志)
    /// 2. 指标中间件 (活跃请求数 + 请求耗时计数)
    /// 3. /metrics 端点 (prometheus-net)
    /// </summary>
    public static IApplicationBuilder UseObservability(
        this IApplicationBuilder app,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(configuration);

        var metricsEnabled = configuration.GetValue("Metrics:Enabled", true);
        var metricsPath = configuration["Metrics:Path"] ?? "/metrics";

        // 访问日志中间件（最早记录请求元数据）
        app.UseMiddleware<AccessLoggingMiddleware>();

        // 指标中间件（包装活跃请求数与请求耗时计数）
        if (metricsEnabled)
        {
            app.Use(async (context, next) =>
            {
                var metrics = context.RequestServices.GetRequiredService<GatewayMetricsService>();
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                metrics.IncrementActiveRequests();

                try
                {
                    await next();
                }
                finally
                {
                    stopwatch.Stop();
                    // YARP 2.2.0: IReverseProxyFeature.Cluster is ClusterModel, read Config.ClusterId
                    var route = context.Features.Get<Yarp.ReverseProxy.Model.IReverseProxyFeature>()
                        ?.Cluster?.Config?.ClusterId;
                    metrics.RecordRequestDuration(route, context.Request.Method, stopwatch.Elapsed.TotalMilliseconds);
                    metrics.RecordRequest(route, context.Request.Method, context.Response.StatusCode);
                    metrics.DecrementActiveRequests();
                }
            });

            // /metrics 端点（在 YARP 之前注册，避免被代理）
            app.UseMetricServer(metricsPath);
        }

        return app;
    }
}
