using System.Reflection;
using Leno.Infrastructure.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog.Core;

namespace Leno.Infrastructure.Telemetry;

/// <summary>
/// OpenTelemetry 集成扩展，配置分布式追踪（Tracing），包括 ASP.NET Core、HttpClient、
/// EF Core、MassTransit 自动埋点，自定义 ActivitySource（订单/支付/库存），
/// 采样策略（生产 10%，开发 100%），以及 Serilog TraceId 富化器。
/// 在 Program.cs 调用 <c>builder.AddLenoOpenTelemetry()</c>。
/// </summary>
public static class OpenTelemetryExtensions
{
    /// <summary>
    /// OTLP Exporter 默认端点（适用于本地 Jaeger / Collector）。
    /// </summary>
    public const string DefaultOtlpEndpoint = "http://localhost:4317";

    /// <summary>
    /// 自定义 ActivitySource 名称常量，用于关键业务操作埋点。
    /// </summary>
    public static class ActivitySources
    {
        public const string Order = "Leno.Order";
        public const string Payment = "Leno.Payment";
        public const string Stock = "Leno.Stock";
    }

    /// <summary>
    /// 配置 OpenTelemetry Tracing 与 Metrics，包括自动埋点与自定义 ActivitySource/Meter。
    /// </summary>
    /// <param name="builder">宿主导入器。</param>
    /// <param name="configureTracing">可选回调，用于添加额外的 TracerProvider 配置。</param>
    /// <param name="configureMetrics">可选回调，用于添加额外的 MeterProvider 配置（M5.1 新增）。</param>
    public static IHostApplicationBuilder AddLenoOpenTelemetry(
        this IHostApplicationBuilder builder,
        Action<TracerProviderBuilder>? configureTracing = null,
        Action<MeterProviderBuilder>? configureMetrics = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var otlpEndpoint = builder.Configuration["OpenTelemetry:OtlpEndpoint"] ?? DefaultOtlpEndpoint;
        var serviceName = builder.Configuration["OpenTelemetry:ServiceName"]
                          ?? Assembly.GetEntryAssembly()?.GetName().Name
                          ?? "Leno";

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource =>
            {
                resource.AddService(
                    serviceName: serviceName,
                    serviceVersion: "1.0.0",
                    autoGenerateServiceInstanceId: true);
            })
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddEntityFrameworkCoreInstrumentation()
                    .AddSource("MassTransit")
                    .AddSource(ActivitySources.Order)
                    .AddSource(ActivitySources.Payment)
                    .AddSource(ActivitySources.Stock)
                    .SetSampler(CreateSampler(builder.Environment))
                    .AddOtlpExporter(options =>
                    {
                        options.Endpoint = new Uri(otlpEndpoint);
                    });

                configureTracing?.Invoke(tracing);
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddMeter("Leno.AntiCorruption")
                    .AddMeter("Leno.SystemAdmin.DeadLetter")
                    .AddMeter("Leno.Order.AntiCorruption")
                    .AddMeter("Leno.Outbox")  // M5.3 新增：Outbox 积压与发布计数指标
                    .AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint));

                configureMetrics?.Invoke(metrics);
            });

        // 注册 Serilog TraceId 富化器（统一实现，支持 OTel Activity 优先 + Serilog LogContext 回退）
        builder.Services.AddSingleton<ILogEventEnricher, TraceIdEnricher>();

        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        return builder;
    }

    /// <summary>
    /// 根据环境选择采样策略：开发环境 100% 采样，生产环境 10% 采样。
    /// </summary>
    private static Sampler CreateSampler(IHostEnvironment environment)
    {
        if (environment.IsDevelopment())
        {
            return new AlwaysOnSampler();
        }

        return new TraceIdRatioBasedSampler(0.1);
    }
}