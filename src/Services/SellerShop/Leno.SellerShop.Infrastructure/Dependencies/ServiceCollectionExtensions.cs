using FluentValidation;
using Leno.SellerShop.Application;
using Leno.SellerShop.Application.Services;
using Leno.SellerShop.Domain.Repositories;
using Leno.SellerShop.Domain.Services;
using Leno.SellerShop.Infrastructure.Consumers;
using Leno.SellerShop.Infrastructure.Repositories;
using Leno.SellerShop.Infrastructure.Services;
using Leno.SharedKernel.Abstractions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Leno.SellerShop.Infrastructure.Dependencies;

/// <summary>
/// 卖家与店铺管理域基础设施层 DI 注册入口。
/// 注册 DbContext、工作单元、仓储、防腐层、应用服务实现与 FluentValidation 校验器。
/// 调用方在 Presentation 层 Program.cs 调用 <c>services.AddSellerShopInfrastructure(configuration)</c>。
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 注册卖家与店铺管理域的全部基础设施与应用服务。
    /// </summary>
    /// <param name="connectionStringName">连接字符串名称，默认 <c>SellerShopDb</c>。</param>
    public static IServiceCollection AddSellerShopInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        string connectionStringName = "SellerShopDb")
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddDbContext<SellerShopDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString(connectionStringName);
            options.UseSqlServer(connectionString);
        });

        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddScoped<IShopRepository, EfCoreShopRepository>();
        services.AddScoped<ISellerProfileRepository, EfCoreSellerProfileRepository>();
        services.AddScoped<IShopMetricsRepository, EfCoreShopMetricsRepository>();

        services.AddScoped<IShopQueryService, EfCoreShopQueryService>();

        services.AddScoped<IShopAppService, ShopAppService>();
        services.AddScoped<ISellerAppService, SellerAppService>();
        services.AddScoped<ISellerDashboardAppService, SellerDashboardAppService>();

        services.AddValidatorsFromAssembly(typeof(IShopAppService).Assembly);

        return services;
    }

    /// <summary>
    /// 注册卖家与店铺管理域的 MassTransit 集成事件消费者。
    /// 在 Presentation 层调用 <c>AddLenoInfrastructure(configuration, cfg => cfg.AddSellerShopConsumers())</c>。
    /// </summary>
    public static IBusRegistrationConfigurator AddSellerShopConsumers(this IBusRegistrationConfigurator configurator)
    {
        ArgumentNullException.ThrowIfNull(configurator);
        configurator.AddConsumer<ProductPublishedEventConsumer>();
        configurator.AddConsumer<ProductTakenDownEventConsumer>();
        configurator.AddConsumer<OrderCompletedEventConsumer>();
        return configurator;
    }
}
