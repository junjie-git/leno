using FluentValidation;
using Leno.Infrastructure.AntiCorruption;
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
using Leno.SharedKernel.Abstractions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
        services.AddScoped<IStockReservationCompensationRepository, EfCoreStockReservationCompensationRepository>();

        // 领域服务
        services.AddScoped<IStockReservationDomainService, StockReservationDomainService>();
        services.AddScoped<IOrderPricingDomainService, OrderPricingDomainService>();
        services.AddScoped<IOrderNumberGenerator, OrderNumberGenerator>();
        services.AddScoped<IFreightCalculator, FreightCalculator>();

        // 防腐层实现：通过 HttpClient 调用商品/促销/积分域内部 API
        var productApiUrl = configuration["ServiceUrls:ProductApi"] ?? "http://localhost:5150";
        var promotionApiUrl = configuration["ServiceUrls:PromotionApi"] ?? "http://localhost:5152";
        var pointsApiUrl = configuration["ServiceUrls:PointsMembershipApi"] ?? "http://localhost:5153";

        services.AddHttpClient<IProductAntiCorruptionService, ProductAntiCorruptionService>(c => c.BaseAddress = new Uri(productApiUrl))
            .AddAntiCorruptionPolicies();
        services.AddHttpClient<IPromotionAntiCorruptionService, PromotionAntiCorruptionService>(c => c.BaseAddress = new Uri(promotionApiUrl))
            .AddAntiCorruptionPolicies();
        services.AddHttpClient<IPointsAntiCorruptionService, PointsAntiCorruptionService>(c => c.BaseAddress = new Uri(pointsApiUrl))
            .AddAntiCorruptionPolicies();

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

        // 多卖家拆单 Saga 编排器
        services.AddScoped<IOrderSagaOrchestrator, OrderSagaOrchestrator>();

        // FluentValidation 校验器
        services.AddValidatorsFromAssembly(typeof(IOrderAppService).Assembly);

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
