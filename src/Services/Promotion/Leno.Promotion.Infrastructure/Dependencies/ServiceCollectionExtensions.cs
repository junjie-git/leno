using Leno.Promotion.Domain.Repositories;
using Leno.Promotion.Domain.Services;
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
/// 注册 DbContext、工作单元、仓储、Redis 秒杀库存、防腐层实现。
/// 应用服务注册在 Task 7 创建 Application 层后补充。
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

        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddScoped<IPromotionActivityRepository, EfCorePromotionActivityRepository>();
        services.AddScoped<ICouponRepository, EfCoreCouponRepository>();
        services.AddScoped<IUserCouponRepository, EfCoreUserCouponRepository>();
        services.AddScoped<ISeckillActivityRepository, EfCoreSeckillActivityRepository>();

        services.AddScoped<IPromotionQueryService, EfCorePromotionQueryService>();
        services.AddScoped<ISeckillStockService, RedisSeckillStockService>();

        return services;
    }

    /// <summary>
    /// 注册促销域的 MassTransit 集成事件消费者。
    /// 在表现层调用 <c>AddLenoInfrastructure(configuration, cfg => cfg.AddPromotionConsumers())</c>。
    /// 消费者在 Task 9 中实现。
    /// </summary>
    public static IBusRegistrationConfigurator AddPromotionConsumers(this IBusRegistrationConfigurator configurator)
    {
        ArgumentNullException.ThrowIfNull(configurator);

        // 消费者在 Task 9 中添加
        return configurator;
    }
}
