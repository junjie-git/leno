using FluentValidation;
using Leno.Order.Application;
using Leno.Order.Application.Services;
using Leno.Order.Domain.Repositories;
using Leno.Order.Domain.Services;
using Leno.Order.Infrastructure.Consumers;
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

        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddScoped<IOrderRepository, EfCoreOrderRepository>();
        services.AddScoped<ILogisticsCompanyRepository, EfCoreLogisticsCompanyRepository>();
        services.AddScoped<IFreightTemplateRepository, EfCoreFreightTemplateRepository>();
        services.AddScoped<IInventoryRepository, RedisInventoryRepository>();

        // 领域服务
        services.AddScoped<IStockReservationDomainService, StockReservationDomainService>();
        services.AddScoped<IOrderPricingDomainService, OrderPricingDomainService>();
        services.AddScoped<IOrderNumberGenerator, OrderNumberGenerator>();
        services.AddScoped<IFreightCalculator, FreightCalculator>();

        // 防腐层实现：通过 HttpClient 调用商品/促销/积分域内部 API
        var productApiUrl = configuration["ServiceUrls:ProductApi"] ?? "http://localhost:5150";
        var promotionApiUrl = configuration["ServiceUrls:PromotionApi"] ?? "http://localhost:5152";
        var pointsApiUrl = configuration["ServiceUrls:PointsMembershipApi"] ?? "http://localhost:5153";

        services.AddHttpClient<IProductAntiCorruptionService, ProductAntiCorruptionService>(c => c.BaseAddress = new Uri(productApiUrl));
        services.AddHttpClient<IPromotionAntiCorruptionService, PromotionAntiCorruptionService>(c => c.BaseAddress = new Uri(promotionApiUrl));
        services.AddHttpClient<IPointsAntiCorruptionService, PointsAntiCorruptionService>(c => c.BaseAddress = new Uri(pointsApiUrl));

        // 应用服务
        services.AddScoped<IOrderAppService, OrderAppService>();
        services.AddScoped<ILogisticsCompanyAppService, LogisticsCompanyAppService>();
        services.AddScoped<IFreightTemplateAppService, FreightTemplateAppService>();
        services.AddScoped<ILogisticsTrackingService, LogisticsTrackingService>();
        services.AddScoped<IOrderInternalQueryService, OrderInternalQueryService>();

        // FluentValidation 校验器
        services.AddValidatorsFromAssembly(typeof(IOrderAppService).Assembly);

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
        configurator.AddConsumer<RefundCompletedEventConsumer>();
        configurator.AddConsumer<StockAdjustedEventConsumer>();
        configurator.AddConsumer<OrderReadModelSyncConsumer>();

        return configurator;
    }
}
