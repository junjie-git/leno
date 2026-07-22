using FluentValidation;
using Leno.Infrastructure.AntiCorruption;
using Leno.Infrastructure.Cqrs;
using Leno.Infrastructure.EventBus;
using Leno.Infrastructure.Persistence;
using Leno.Order.Application;
using Leno.Order.Application.Services;
using Leno.Order.Domain.Repositories;
using Leno.Order.Domain.Services;
using Leno.Order.Infrastructure.Consumers;
using Leno.Order.Infrastructure.EventBus;
using Leno.Order.Infrastructure.ReadModels;
using Leno.Order.Infrastructure.Repositories;
using Leno.Order.Infrastructure.Services;
using Leno.Order.Infrastructure.Services.Grpc;
using Leno.SharedContracts.Grpc.Points.V1;
using Leno.SharedContracts.Grpc.Product.V1;
using Leno.SharedContracts.Grpc.Promotion.V1;
using Leno.SharedKernel.Abstractions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leno.Order.Infrastructure.Dependencies;

/// <summary>
/// 订单域基础设施层 DI 注册入口。
/// 注册 DbContext、工作单元、仓储、领域服务、防腐层、应用服务、FluentValidation 校验器与 MassTransit 消费者。
/// 调用方在表现层 Program.cs 调用 <c>services.AddOrderInfrastructure(configuration)</c>。
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <param name="connectionStringName">连接字符串名称，默认 <c>OrderDb</c>。</param>
    public static IServiceCollection AddOrderInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        string connectionStringName = "OrderDb")
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddDbContext<OrderDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString(connectionStringName);
            options.UseSqlServer(connectionString);
        });

        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork<OrderDbContext>>();

        // 领域事件 → 集成事件翻译器（Outbox 同事务发布时由 UnitOfWork 调用）
        services.AddSingleton<IIntegrationEventMapper, OrderIntegrationEventMapper>();

        services.AddScoped<IOrderRepository, EfCoreOrderRepository>();
        services.AddScoped<ILogisticsCompanyRepository, EfCoreLogisticsCompanyRepository>();
        services.AddScoped<IFreightTemplateRepository, EfCoreFreightTemplateRepository>();
        services.AddScoped<IInventoryRepository, RedisInventoryRepository>();
        services.AddScoped<IStockReservationRepository, EfCoreStockReservationRepository>();
        services.AddScoped<IStockReservationCompensationRepository, EfCoreStockReservationCompensationRepository>();

        // 领域服务
        services.AddScoped<IStockReservationDomainService, StockReservationDomainService>();
        services.AddScoped<IOrderPricingDomainService, OrderPricingDomainService>();
        services.AddScoped<IOrderPricingPreviewService, OrderPricingPreviewService>();
        services.AddScoped<IPointsAllocationService, PointsAllocationService>();
        services.AddScoped<IOrderNumberGenerator, OrderNumberGenerator>();
        services.AddScoped<IFreightCalculator, FreightCalculator>();

        // 防腐层实现：通过 HttpClient 调用商品/促销/积分域内部 API
        var productApiUrl = configuration["ServiceUrls:ProductApi"] ?? "http://localhost:5150";
        var promotionApiUrl = configuration["ServiceUrls:PromotionApi"] ?? "http://localhost:5152";
        var pointsApiUrl = configuration["ServiceUrls:PointsMembershipApi"] ?? "http://localhost:5153";

        // HttpClient 防腐层实现（保留作为降级备份）
        services.AddHttpClient<ProductAntiCorruptionService>(c => c.BaseAddress = new Uri(productApiUrl))
            .AddAntiCorruptionPolicies();
        services.AddHttpClient<PromotionAntiCorruptionService>(c => c.BaseAddress = new Uri(promotionApiUrl))
            .AddAntiCorruptionPolicies();
        services.AddHttpClient<PointsAntiCorruptionService>(c => c.BaseAddress = new Uri(pointsApiUrl))
            .AddAntiCorruptionPolicies();

        // M4 双轨方案：gRPC 客户端 + 熔断器 + Dispatcher（仅当 UseGrpc=true 时生效）
        var antiCorruptionOptions = configuration.GetSection("AntiCorruption").Get<AntiCorruptionOptions>() ?? new AntiCorruptionOptions();
        if (antiCorruptionOptions.UseGrpc)
        {
            var productGrpcEndpoint = antiCorruptionOptions.GrpcEndpoints.GetValueOrDefault("Product")
                ?? throw new InvalidOperationException("AntiCorruption:GrpcEndpoints:Product 配置缺失");

            services.AddGrpcClient<ProductInternalService.ProductInternalServiceClient>(options =>
            {
                options.Address = new Uri(productGrpcEndpoint);
            });
            services.AddScoped<GrpcProductAntiCorruptionClient>();

            services.AddKeyedSingleton<CircuitBreakerState>("product", (sp, _) =>
            {
                var opts = sp.GetRequiredService<IOptionsMonitor<AntiCorruptionOptions>>().CurrentValue;
                var cbOpts = opts.CircuitBreaker ?? new CircuitBreakerOptions();
                return new CircuitBreakerState(
                    "product",
                    cbOpts.FailureThreshold,
                    cbOpts.SuccessThreshold,
                    TimeSpan.FromSeconds(cbOpts.OpenDurationSeconds));
            });

            services.AddScoped<AntiCorruptionDispatcher<IProductAntiCorruptionService>>(sp =>
            {
                var httpImpl = sp.GetRequiredService<ProductAntiCorruptionService>();
                var grpcImpl = sp.GetService<GrpcProductAntiCorruptionClient>();
                var options = sp.GetRequiredService<IOptionsMonitor<AntiCorruptionOptions>>();
                var logger = sp.GetRequiredService<ILogger<AntiCorruptionDispatcher<IProductAntiCorruptionService>>>();
                var cb = sp.GetRequiredKeyedService<CircuitBreakerState>("product");
                return new AntiCorruptionDispatcher<IProductAntiCorruptionService>(
                    httpImpl, grpcImpl, options, logger, "product", cb);
            });
            services.AddScoped<ProductAntiCorruptionDispatcherAdapter>();
            services.AddScoped<IProductAntiCorruptionService>(sp =>
                sp.GetRequiredService<ProductAntiCorruptionDispatcherAdapter>());

            // Promotion 双轨
            var promotionGrpcEndpoint = antiCorruptionOptions.GrpcEndpoints.GetValueOrDefault("Promotion")
                ?? throw new InvalidOperationException("AntiCorruption:GrpcEndpoints:Promotion 配置缺失");

            services.AddGrpcClient<PromotionInternalService.PromotionInternalServiceClient>(options =>
            {
                options.Address = new Uri(promotionGrpcEndpoint);
            });
            services.AddScoped<GrpcPromotionAntiCorruptionClient>();

            services.AddKeyedSingleton<CircuitBreakerState>("promotion", (sp, _) =>
            {
                var opts = sp.GetRequiredService<IOptionsMonitor<AntiCorruptionOptions>>().CurrentValue;
                var cbOpts = opts.CircuitBreaker ?? new CircuitBreakerOptions();
                return new CircuitBreakerState(
                    "promotion",
                    cbOpts.FailureThreshold,
                    cbOpts.SuccessThreshold,
                    TimeSpan.FromSeconds(cbOpts.OpenDurationSeconds));
            });

            services.AddScoped<AntiCorruptionDispatcher<IPromotionAntiCorruptionService>>(sp =>
            {
                var httpImpl = sp.GetRequiredService<PromotionAntiCorruptionService>();
                var grpcImpl = sp.GetService<GrpcPromotionAntiCorruptionClient>();
                var options = sp.GetRequiredService<IOptionsMonitor<AntiCorruptionOptions>>();
                var logger = sp.GetRequiredService<ILogger<AntiCorruptionDispatcher<IPromotionAntiCorruptionService>>>();
                var cb = sp.GetRequiredKeyedService<CircuitBreakerState>("promotion");
                return new AntiCorruptionDispatcher<IPromotionAntiCorruptionService>(
                    httpImpl, grpcImpl, options, logger, "promotion", cb);
            });
            services.AddScoped<PromotionAntiCorruptionDispatcherAdapter>();
            services.AddScoped<IPromotionAntiCorruptionService>(sp =>
                sp.GetRequiredService<PromotionAntiCorruptionDispatcherAdapter>());

            // Points 双轨
            var pointsGrpcEndpoint = antiCorruptionOptions.GrpcEndpoints.GetValueOrDefault("PointsMembership")
                ?? throw new InvalidOperationException("AntiCorruption:GrpcEndpoints:PointsMembership 配置缺失");

            services.AddGrpcClient<PointsInternalService.PointsInternalServiceClient>(options =>
            {
                options.Address = new Uri(pointsGrpcEndpoint);
            });
            services.AddScoped<GrpcPointsAntiCorruptionClient>();

            services.AddKeyedSingleton<CircuitBreakerState>("points", (sp, _) =>
            {
                var opts = sp.GetRequiredService<IOptionsMonitor<AntiCorruptionOptions>>().CurrentValue;
                var cbOpts = opts.CircuitBreaker ?? new CircuitBreakerOptions();
                return new CircuitBreakerState(
                    "points",
                    cbOpts.FailureThreshold,
                    cbOpts.SuccessThreshold,
                    TimeSpan.FromSeconds(cbOpts.OpenDurationSeconds));
            });

            services.AddScoped<AntiCorruptionDispatcher<IPointsAntiCorruptionService>>(sp =>
            {
                var httpImpl = sp.GetRequiredService<PointsAntiCorruptionService>();
                var grpcImpl = sp.GetService<GrpcPointsAntiCorruptionClient>();
                var options = sp.GetRequiredService<IOptionsMonitor<AntiCorruptionOptions>>();
                var logger = sp.GetRequiredService<ILogger<AntiCorruptionDispatcher<IPointsAntiCorruptionService>>>();
                var cb = sp.GetRequiredKeyedService<CircuitBreakerState>("points");
                return new AntiCorruptionDispatcher<IPointsAntiCorruptionService>(
                    httpImpl, grpcImpl, options, logger, "points", cb);
            });
            services.AddScoped<PointsAntiCorruptionDispatcherAdapter>();
            services.AddScoped<IPointsAntiCorruptionService>(sp =>
                sp.GetRequiredService<PointsAntiCorruptionDispatcherAdapter>());
        }
        else
        {
            // UseGrpc=false：直接注册 HttpClient 实现（兼容期）
            services.AddScoped<IProductAntiCorruptionService>(sp =>
                sp.GetRequiredService<ProductAntiCorruptionService>());
            services.AddScoped<IPromotionAntiCorruptionService>(sp =>
                sp.GetRequiredService<PromotionAntiCorruptionService>());
            services.AddScoped<IPointsAntiCorruptionService>(sp =>
                sp.GetRequiredService<PointsAntiCorruptionService>());
        }

        // T17: 防腐层降级告警 —— 通过 OpenTelemetry SDK 按名称订阅 Meter，
        // 暴露 Prometheus 指标 anticorruption_failure_total{service,operation}。
        // 各 BC 表现层 Program.cs 调用 AddLenoOpenTelemetry 时通过 configureTracing 回调
        // 追加 .AddMeter(AntiCorruptionMetrics.MeterName) 即可采集；本注册仅文档化订阅方式。
        // Meter 实例本身为静态单例（见 AntiCorruptionMetrics），无需 DI 注册。

        // 物流轨迹查询：通过 HttpClient 调用第三方物流 API
        services.Configure<LogisticsApiOptions>(configuration.GetSection(LogisticsApiOptions.SectionName));
        services.AddHttpClient<Domain.Services.ILogisticsTrackingService, LogisticsTrackingService>()
            .AddAntiCorruptionPolicies();

        // 应用服务
        services.AddScoped<IOrderAppService, OrderAppService>();
        services.AddScoped<ILogisticsCompanyAppService, LogisticsCompanyAppService>();
        services.AddScoped<IFreightTemplateAppService, FreightTemplateAppService>();
        services.AddScoped<IOrderInternalQueryService, OrderInternalQueryService>();
        services.AddScoped<SeckillOrderCreationService>();

        // 多卖家拆单 Saga 编排器（P1-T24：生产环境并行度上限 = 5，缩短多卖家下单延迟）
        services.AddScoped<IOrderSagaOrchestrator>(sp => new OrderSagaOrchestrator(
            sp.GetRequiredService<IOrderRepository>(),
            sp.GetRequiredService<IUnitOfWork>(),
            sp.GetRequiredService<IOrderNumberGenerator>(),
            sp.GetRequiredService<IStockReservationDomainService>(),
            sp.GetRequiredService<IOrderPricingDomainService>(),
            sp.GetRequiredService<IFreightCalculator>(),
            sp.GetRequiredService<IPromotionAntiCorruptionService>(),
            sp.GetRequiredService<IPointsAntiCorruptionService>(),
            sp.GetRequiredService<IBus>(),
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<OrderSagaOrchestrator>>(),
            maxDegreeOfParallelism: OrderSagaOrchestrator.ProductionMaxDegreeOfParallelism));

        // FluentValidation 校验器
        services.AddValidatorsFromAssembly(typeof(IOrderAppService).Assembly);

        // CQRS 读侧：扫描 Application 程序集注册所有 IQueryHandler<TQuery, TResult>
        services.AddQueryHandlers(typeof(IOrderAppService).Assembly);

        // 库存对账后台服务
        services.AddHostedService<StockReconciliationService>();

        // T18: 库存预占回滚补偿后台服务，定期重试 Pending 补偿记录释放库存
        services.Configure<StockReservationCompensationOptions>(
            configuration.GetSection("StockReservationCompensation"));
        services.AddHostedService<StockReservationCompensationBackgroundService>();

        return services;
    }

    /// <summary>
    /// 注册订单域的 MassTransit 消费者（集成事件消费者 + ES 读模型同步消费者）。
    /// 在表现层调用 <c>AddLenoInfrastructure(configuration, cfg => cfg.AddOrderConsumers())</c>。
    /// </summary>
    public static IBusRegistrationConfigurator AddOrderConsumers(
        this IBusRegistrationConfigurator configurator)
    {
        ArgumentNullException.ThrowIfNull(configurator);

        configurator.AddConsumer<PaymentSucceededEventConsumer>();
        // T4：库存确认与积分确认拆分为独立消费者，与订单状态变更隔离，
        // 通过独立队列 + 独立幂等键（stock-confirm-/points-confirm-{PaymentId}）实现任一失败不影响其他
        configurator.AddConsumer<StockConfirmConsumer>();
        configurator.AddConsumer<PointsConfirmConsumer>();
        configurator.AddConsumer<PaymentFailedEventConsumer>();
        configurator.AddConsumer<OrderTimeoutDelayMessageConsumer>();
        configurator.AddConsumer<AfterSalesWindowConsumer>();
        configurator.AddConsumer<RefundCompletedEventConsumer>();
        configurator.AddConsumer<StockAdjustedEventConsumer>();
        configurator.AddConsumer<OrderReadModelSyncConsumer>();
        configurator.AddConsumer<SeckillOrderCreatedEventConsumer>();

        return configurator;
    }
}
