using AppServices = Leno.Review.Application.Services;
using InfraServices = Leno.Review.Infrastructure.Services;
using Leno.Infrastructure.Abstractions;
using Leno.Infrastructure.AntiCorruption;
using Leno.Infrastructure.EventBus;
using Leno.Infrastructure.Persistence;
using Leno.Infrastructure.Storage;
using Leno.Review.Application;
using Leno.Review.Application.InternalQueryServices;
using Leno.Review.Application.Services;
using Leno.Review.Domain.Repositories;
using Leno.Review.Domain.Services;
using Leno.Review.Infrastructure.EventBus;
using Leno.Review.Infrastructure.ReadModels;
using Leno.Review.Infrastructure.Repositories;
using Leno.Review.Infrastructure.Services.Grpc;
using Leno.SharedContracts.Grpc.Order.V1;
using Leno.SharedKernel.Abstractions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leno.Review.Infrastructure.Dependencies;

/// <summary>
/// 评价域基础设施层 DI 注册入口（评价 BC 独立维护）。
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddReviewInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        string connectionStringName = "ReviewDb")
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddDbContext<ReviewDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString(connectionStringName);
            options.UseSqlServer(connectionString);
        });

        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork<ReviewDbContext>>();

        // 注册 Review BC 领域事件到集成事件翻译器
        services.AddSingleton<IIntegrationEventMapper, ReviewIntegrationEventMapper>();

        // 审计 3.11：文件签名校验器，防止伪装扩展名上传非图片文件
        services.AddSingleton<IFileSignatureDetector, FileSignatureDetector>();

        services.AddScoped<IReviewRepository, EfCoreReviewRepository>();

        // 防腐层实现：HttpClient 实现（保留作为降级备份）
        var orderApiUrl = configuration["ServiceUrls:OrderApi"] ?? "http://localhost:5154";

        services.AddHttpClient<InfraServices.HttpOrderStatusProvider>(c => c.BaseAddress = new Uri(orderApiUrl))
            .AddAntiCorruptionPolicies();

        // M4 双轨方案：gRPC 客户端 + 熔断器 + Dispatcher（仅当 UseGrpc=true 时生效）
        // 审计 4.4：gRPC 端点缺失时不抛异常，记录 LogWarning 并降级到 HttpClient 模式（仅注册 HttpClient 实现）。
        var antiCorruptionOptions = configuration.GetSection("AntiCorruption").Get<AntiCorruptionOptions>() ?? new AntiCorruptionOptions();
        if (antiCorruptionOptions.UseGrpc)
        {
            // Order 双轨（IOrderStatusProvider）
            var orderGrpcEndpoint = antiCorruptionOptions.GrpcEndpoints.GetValueOrDefault("Order");
            if (!string.IsNullOrWhiteSpace(orderGrpcEndpoint))
            {
                services.AddGrpcClient<OrderInternalService.OrderInternalServiceClient>(options =>
                {
                    options.Address = new Uri(orderGrpcEndpoint);
                });
                services.AddScoped<GrpcOrderStatusProvider>();

                services.AddKeyedSingleton<CircuitBreakerState>("order", (sp, _) =>
                {
                    var opts = sp.GetRequiredService<IOptionsMonitor<AntiCorruptionOptions>>().CurrentValue;
                    var cbOpts = opts.CircuitBreaker ?? new CircuitBreakerOptions();
                    return new CircuitBreakerState(
                        "order",
                        cbOpts.FailureThreshold,
                        cbOpts.SuccessThreshold,
                        TimeSpan.FromSeconds(cbOpts.OpenDurationSeconds));
                });

                services.AddScoped<AntiCorruptionDispatcher<IOrderStatusProvider>>(sp =>
                {
                    var httpImpl = sp.GetRequiredService<InfraServices.HttpOrderStatusProvider>();
                    var grpcImpl = sp.GetService<GrpcOrderStatusProvider>();
                    var options = sp.GetRequiredService<IOptionsMonitor<AntiCorruptionOptions>>();
                    var logger = sp.GetRequiredService<ILogger<AntiCorruptionDispatcher<IOrderStatusProvider>>>();
                    var cb = sp.GetRequiredKeyedService<CircuitBreakerState>("order");
                    return new AntiCorruptionDispatcher<IOrderStatusProvider>(
                        httpImpl, grpcImpl, options, logger, "order", cb);
                });
                services.AddScoped<OrderStatusDispatcherAdapter>();
                services.AddScoped<IOrderStatusProvider>(sp =>
                    sp.GetRequiredService<OrderStatusDispatcherAdapter>());
            }
            else
            {
                // 降级到 HttpClient 模式：注册启动时告警 HostedService，再注册 HttpClient 实现作为唯一实现。
                services.AddHostedService(sp => new GrpcDegradationWarningHostedService(
                    sp.GetRequiredService<ILogger<GrpcDegradationWarningHostedService>>(),
                    "Order",
                    "AntiCorruption:GrpcEndpoints:Order"));
                services.AddScoped<IOrderStatusProvider>(sp =>
                    sp.GetRequiredService<InfraServices.HttpOrderStatusProvider>());
            }
        }
        else
        {
            // UseGrpc=false：直接注册 HttpClient 实现（兼容期）
            services.AddScoped<IOrderStatusProvider>(sp =>
                sp.GetRequiredService<InfraServices.HttpOrderStatusProvider>());
        }

        // 资格校验器（依赖 IOrderStatusProvider，无论双轨与否都注册）
        services.AddScoped<IReviewEligibilityChecker, InfraServices.ReviewEligibilityChecker>();

        // 应用服务
        services.AddScoped<IReviewAppService, AppServices.ReviewAppService>();

        // M4 双轨方案：注册跨 BC 内部查询服务（供 ReviewGrpcService 复用）
        services.AddScoped<IReviewInternalQueryService, ReviewInternalQueryService>();

        // ES 索引初始化器（评价 BC 独立 reviews_v2 索引）
        services.AddHostedService<ReviewIndexInitializer>();

        return services;
    }

    public static IBusRegistrationConfigurator AddReviewConsumers(
        this IBusRegistrationConfigurator configurator)
    {
        ArgumentNullException.ThrowIfNull(configurator);

        // 评价读模型同步消费者：监听评价生命周期事件同步 ES reviews_v2 索引
        configurator.AddConsumer<ReviewReadModelSyncConsumer>();

        return configurator;
    }
}

/// <summary>
/// gRPC 端点缺失降级告警 HostedService（审计 4.4，评价 BC 独立维护）。
/// 在应用启动时记录 LogWarning，提示运维 AntiCorruption:GrpcEndpoints:{BcName} 配置缺失，
/// 已自动降级到 HttpClient 模式。仅打日志无其他副作用，运行一次即结束。
/// </summary>
internal sealed class GrpcDegradationWarningHostedService : IHostedService
{
    private readonly ILogger _logger;
    private readonly string _bcName;
    private readonly string _configKey;

    public GrpcDegradationWarningHostedService(ILogger logger, string bcName, string configKey)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        if (string.IsNullOrWhiteSpace(bcName))
        {
            throw new ArgumentException("BC 名称不可为空", nameof(bcName));
        }
        _bcName = bcName;
        if (string.IsNullOrWhiteSpace(configKey))
        {
            throw new ArgumentException("配置键不可为空", nameof(configKey));
        }
        _configKey = configKey;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogWarning(
            "AntiCorruption:UseGrpc=true 但 {ConfigKey} 配置缺失，{BcName} 防腐层已降级到 HttpClient 模式。请尽快补齐 gRPC 端点配置以恢复双轨能力。",
            _configKey,
            _bcName);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
