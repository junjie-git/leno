using AppServices = Leno.ReviewAfterSales.Application.Services;
using InfraServices = Leno.ReviewAfterSales.Infrastructure.Services;
using Leno.Infrastructure.Abstractions;
using Leno.Infrastructure.AntiCorruption;
using Leno.Infrastructure.EventBus;
using Leno.Infrastructure.Persistence;
using Leno.Infrastructure.Storage;
using Leno.ReviewAfterSales.Application;
using Leno.ReviewAfterSales.Application.InternalQueryServices;
using Leno.ReviewAfterSales.Application.Services;
using Leno.ReviewAfterSales.Domain.Repositories;
using Leno.ReviewAfterSales.Domain.Services;
using Leno.ReviewAfterSales.Infrastructure.Consumers;
using Leno.ReviewAfterSales.Infrastructure.EventBus;
using Leno.ReviewAfterSales.Infrastructure.ReadModels;
using Leno.ReviewAfterSales.Infrastructure.Repositories;
using Leno.ReviewAfterSales.Infrastructure.Services.Grpc;
using Leno.SharedContracts.Grpc.Order.V1;
using Leno.SharedContracts.Grpc.Payment.V1;
using Leno.SharedContracts.Grpc.Product.V1;
using Leno.SharedKernel.Abstractions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leno.ReviewAfterSales.Infrastructure.Dependencies;

/// <summary>
/// 评价与售后域基础设施层 DI 注册入口。
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddReviewAfterSalesInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        string connectionStringName = "ReviewAfterSalesDb")
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddDbContext<ReviewAfterSalesDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString(connectionStringName);
            options.UseSqlServer(connectionString);
        });

        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork<ReviewAfterSalesDbContext>>();

        // 注册 ReviewAfterSales BC 领域事件到集成事件翻译器
        services.AddSingleton<IIntegrationEventMapper, ReviewAfterSalesIntegrationEventMapper>();

        // 审计 3.11：文件签名校验器，防止伪装扩展名上传非图片文件
        services.AddSingleton<IFileSignatureDetector, FileSignatureDetector>();

        services.AddScoped<IReviewRepository, EfCoreReviewRepository>();
        services.AddScoped<IAfterSalesRepository, EfCoreAfterSalesRepository>();

        // 防腐层实现：HttpClient 实现（保留作为降级备份）
        var paymentApiUrl = configuration["ServiceUrls:PaymentApi"] ?? "http://localhost:5155";
        var orderApiUrl = configuration["ServiceUrls:OrderApi"] ?? "http://localhost:5154";

        services.AddHttpClient<InfraServices.PaymentInfoQueryService>(c => c.BaseAddress = new Uri(paymentApiUrl))
            .AddAntiCorruptionPolicies();
        services.AddHttpClient<InfraServices.HttpOrderStatusProvider>(c => c.BaseAddress = new Uri(orderApiUrl))
            .AddAntiCorruptionPolicies();

        // M4 双轨方案：gRPC 客户端 + 熔断器 + Dispatcher（仅当 UseGrpc=true 时生效）
        // 审计 4.4：gRPC 端点缺失时不抛异常，记录 LogWarning 并降级到 HttpClient 模式（仅注册 HttpClient 实现）。
        // 分别按 Payment/Order 端点是否存在独立降级，避免单端点缺失导致整个 BC 启动失败。
        var antiCorruptionOptions = configuration.GetSection("AntiCorruption").Get<AntiCorruptionOptions>() ?? new AntiCorruptionOptions();
        if (antiCorruptionOptions.UseGrpc)
        {
            // Payment 双轨（IPaymentInfoQueryService）
            var paymentGrpcEndpoint = antiCorruptionOptions.GrpcEndpoints.GetValueOrDefault("Payment");
            if (!string.IsNullOrWhiteSpace(paymentGrpcEndpoint))
            {
                services.AddGrpcClient<PaymentInternalService.PaymentInternalServiceClient>(options =>
                {
                    options.Address = new Uri(paymentGrpcEndpoint);
                });
                services.AddScoped<GrpcPaymentInfoQueryService>();

                services.AddKeyedSingleton<CircuitBreakerState>("payment", (sp, _) =>
                {
                    var opts = sp.GetRequiredService<IOptionsMonitor<AntiCorruptionOptions>>().CurrentValue;
                    var cbOpts = opts.CircuitBreaker ?? new CircuitBreakerOptions();
                    return new CircuitBreakerState(
                        "payment",
                        cbOpts.FailureThreshold,
                        cbOpts.SuccessThreshold,
                        TimeSpan.FromSeconds(cbOpts.OpenDurationSeconds));
                });

                services.AddScoped<AntiCorruptionDispatcher<IPaymentInfoQueryService>>(sp =>
                {
                    var httpImpl = sp.GetRequiredService<InfraServices.PaymentInfoQueryService>();
                    var grpcImpl = sp.GetService<GrpcPaymentInfoQueryService>();
                    var options = sp.GetRequiredService<IOptionsMonitor<AntiCorruptionOptions>>();
                    var logger = sp.GetRequiredService<ILogger<AntiCorruptionDispatcher<IPaymentInfoQueryService>>>();
                    var cb = sp.GetRequiredKeyedService<CircuitBreakerState>("payment");
                    return new AntiCorruptionDispatcher<IPaymentInfoQueryService>(
                        httpImpl, grpcImpl, options, logger, "payment", cb);
                });
                services.AddScoped<PaymentInfoQueryDispatcherAdapter>();
                services.AddScoped<IPaymentInfoQueryService>(sp =>
                    sp.GetRequiredService<PaymentInfoQueryDispatcherAdapter>());
            }
            else
            {
                // 降级到 HttpClient 模式：注册启动时告警 HostedService，再注册 HttpClient 实现作为唯一实现。
                services.AddHostedService(sp => new GrpcDegradationWarningHostedService(
                    sp.GetRequiredService<ILogger<GrpcDegradationWarningHostedService>>(),
                    "Payment",
                    "AntiCorruption:GrpcEndpoints:Payment"));
                services.AddScoped<IPaymentInfoQueryService>(sp =>
                    sp.GetRequiredService<InfraServices.PaymentInfoQueryService>());
            }

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

            // Product 单轨（IProductInfoQueryService）
            // 卖家侧评价列表按商品名称过滤场景使用，无 HttpClient 降级实现，
            // gRPC 端点缺失时降级到 NullProductInfoQueryService（fail-open，返回空字典，按 productName 过滤返回空列表）。
            var productGrpcEndpoint = antiCorruptionOptions.GrpcEndpoints.GetValueOrDefault("Product");
            if (!string.IsNullOrWhiteSpace(productGrpcEndpoint))
            {
                services.AddGrpcClient<ProductInternalService.ProductInternalServiceClient>(options =>
                {
                    options.Address = new Uri(productGrpcEndpoint);
                });
                services.AddScoped<GrpcProductInfoQueryService>();
                services.AddScoped<IProductInfoQueryService>(sp =>
                    sp.GetRequiredService<GrpcProductInfoQueryService>());
            }
            else
            {
                // 降级到 NullProductInfoQueryService：注册启动时告警 HostedService，再注册 Null 实现作为唯一实现。
                services.AddHostedService(sp => new GrpcDegradationWarningHostedService(
                    sp.GetRequiredService<ILogger<GrpcDegradationWarningHostedService>>(),
                    "Product",
                    "AntiCorruption:GrpcEndpoints:Product"));
                services.AddScoped<IProductInfoQueryService, InfraServices.NullProductInfoQueryService>();
            }
        }
        else
        {
            // UseGrpc=false：直接注册 HttpClient 实现（兼容期）
            services.AddScoped<IPaymentInfoQueryService>(sp =>
                sp.GetRequiredService<InfraServices.PaymentInfoQueryService>());
            services.AddScoped<IOrderStatusProvider>(sp =>
                sp.GetRequiredService<InfraServices.HttpOrderStatusProvider>());
            // Product 无 HttpClient 实现，UseGrpc=false 时降级到 NullProductInfoQueryService（fail-open）
            services.AddScoped<IProductInfoQueryService, InfraServices.NullProductInfoQueryService>();
        }

        // 资格校验器（依赖 IOrderStatusProvider，无论双轨与否都注册）
        services.AddScoped<IAfterSalesEligibilityChecker, InfraServices.AfterSalesEligibilityChecker>();
        services.AddScoped<IReviewEligibilityChecker, InfraServices.ReviewEligibilityChecker>();

        // 应用服务
        services.AddScoped<IReviewAppService, AppServices.ReviewAppService>();
        services.AddScoped<IAfterSalesAppService, AppServices.AfterSalesAppService>();

        // M4 双轨方案：注册跨 BC 内部查询服务（供 ReviewGrpcService 复用）
        services.AddScoped<IReviewInternalQueryService, ReviewInternalQueryService>();

        return services;
    }

    public static IBusRegistrationConfigurator AddReviewAfterSalesConsumers(
        this IBusRegistrationConfigurator configurator)
    {
        ArgumentNullException.ThrowIfNull(configurator);

        // 审计 4.1：原订单完成事件消费者仅打日志无副作用，评价资格校验在提交时通过订单域防腐层执行，
        // 死消费者徒增事件总线负担，已删除。
        configurator.AddConsumer<RefundSucceededEventConsumer>();
        configurator.AddConsumer<RefundFailedEventConsumer>();
        configurator.AddConsumer<ReviewReadModelSyncConsumer>();

        return configurator;
    }
}

/// <summary>
/// gRPC 端点缺失降级告警 HostedService（审计 4.4）。
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
