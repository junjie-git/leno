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
/// 注册 DbContext、工作单元、仓储、领域服务与 MassTransit 消费者。
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
