using FluentValidation;
using Leno.Infrastructure.EventBus;
using Leno.Infrastructure.Persistence;
using Leno.Promotion.Application;
using Leno.Promotion.Application.Services;
using Leno.Promotion.Domain.Repositories;
using Leno.Promotion.Domain.Services;
using Leno.Promotion.Infrastructure.EventBus;
using Leno.Promotion.Infrastructure.Repositories;
using Leno.Promotion.Infrastructure.Services;
using Leno.SharedKernel.Abstractions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Leno.Promotion.Infrastructure.Dependencies;

/// <summary>
/// 促销域基础设施层 DI 注册入口。
/// 注册 DbContext、工作单元、仓储、Redis 秒杀库存、防腐层实现、应用服务与校验器。
/// 调用方在表现层 Program.cs 调用 <c>services.AddPromotionInfrastructure(configuration)</c>。
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <param name="connectionStringName">连接字符串名称，默认 <c>PromotionDb</c>。</param>
    public static IServiceCollection AddPromotionInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        string connectionStringName = "PromotionDb")
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddDbContext<PromotionDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString(connectionStringName);
            options.UseSqlServer(connectionString);
        });

        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork<PromotionDbContext>>();

        // 注册 Promotion BC 领域事件到集成事件翻译器
        services.AddSingleton<IIntegrationEventMapper, PromotionIntegrationEventMapper>();

        services.AddScoped<IPromotionActivityRepository, EfCorePromotionActivityRepository>();
        services.AddScoped<ICouponRepository, EfCoreCouponRepository>();
        services.AddScoped<IUserCouponRepository, EfCoreUserCouponRepository>();
        services.AddScoped<ISeckillActivityRepository, EfCoreSeckillActivityRepository>();
        services.AddScoped<ISeckillPreOccupationRecordRepository, EfCoreSeckillPreOccupationRecordRepository>();

        services.AddScoped<IPromotionQueryService, EfCorePromotionQueryService>();
        services.AddScoped<ISeckillStockService, RedisSeckillStockService>();

        // 应用服务
        services.AddScoped<IPromotionAppService, PromotionAppService>();
        services.AddScoped<ICouponAppService, CouponAppService>();
        services.AddScoped<ISeckillAppService, SeckillAppService>();
        services.AddScoped<IPromotionCalculateAppService, PromotionCalculateAppService>();

        // FluentValidation 校验器
        services.AddValidatorsFromAssembly(typeof(IPromotionAppService).Assembly);

        return services;
    }

    /// <summary>
    /// 注册促销域的 MassTransit 集成事件消费者。
    /// 在表现层调用 <c>AddLenoInfrastructure(configuration, cfg => cfg.AddPromotionConsumers())</c>。
    /// </summary>
    public static IBusRegistrationConfigurator AddPromotionConsumers(this IBusRegistrationConfigurator configurator)
    {
        ArgumentNullException.ThrowIfNull(configurator);

        configurator.AddConsumer<Consumers.OrderPaidEventConsumer>();
        configurator.AddConsumer<Consumers.OrderCancelledEventConsumer>();
        configurator.AddConsumer<Consumers.RefundCompletedEventConsumer>();
        configurator.AddConsumer<Consumers.SeckillOrderCreationFailedEventConsumer>();
        configurator.AddConsumer<Consumers.SeckillOrderConfirmedEventConsumer>();
        configurator.AddConsumer<Consumers.PointsExchangeConsumer>();

        return configurator;
    }

    /// <summary>
    /// 注册促销域的后台服务（补偿任务等）。
    /// </summary>
    public static IServiceCollection AddPromotionHostedServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddHostedService<BackgroundServices.SeckillPreOccupationCompensationService>();

        return services;
    }
}
