using AppServices = Leno.ReviewAfterSales.Application.Services;
using InfraServices = Leno.ReviewAfterSales.Infrastructure.Services;
using Leno.Infrastructure.AntiCorruption;
using Leno.Infrastructure.EventBus;
using Leno.Infrastructure.Persistence;
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
using Leno.SharedKernel.Abstractions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
        var antiCorruptionOptions = configuration.GetSection("AntiCorruption").Get<AntiCorruptionOptions>() ?? new AntiCorruptionOptions();
        if (antiCorruptionOptions.UseGrpc)
        {
            // Payment 双轨（IPaymentInfoQueryService）
            var paymentGrpcEndpoint = antiCorruptionOptions.GrpcEndpoints.GetValueOrDefault("Payment")
                ?? throw new InvalidOperationException("AntiCorruption:GrpcEndpoints:Payment 配置缺失");

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

            // Order 双轨（IOrderStatusProvider）
            var orderGrpcEndpoint = antiCorruptionOptions.GrpcEndpoints.GetValueOrDefault("Order")
                ?? throw new InvalidOperationException("AntiCorruption:GrpcEndpoints:Order 配置缺失");

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
            // UseGrpc=false：直接注册 HttpClient 实现（兼容期）
            services.AddScoped<IPaymentInfoQueryService>(sp =>
                sp.GetRequiredService<InfraServices.PaymentInfoQueryService>());
            services.AddScoped<IOrderStatusProvider>(sp =>
                sp.GetRequiredService<InfraServices.HttpOrderStatusProvider>());
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

        configurator.AddConsumer<OrderCompletedEventConsumer>();
        configurator.AddConsumer<RefundSucceededEventConsumer>();
        configurator.AddConsumer<RefundFailedEventConsumer>();
        configurator.AddConsumer<ReviewReadModelSyncConsumer>();

        return configurator;
    }
}
