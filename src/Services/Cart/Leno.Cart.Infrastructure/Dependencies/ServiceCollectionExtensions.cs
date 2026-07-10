using FluentValidation;
using Leno.Cart.Application;
using Leno.Cart.Application.Services;
using Leno.Cart.Domain.Repositories;
using Leno.Cart.Domain.Services;
using Leno.Cart.Infrastructure.Caching;
using Leno.Cart.Infrastructure.Consumers;
using Leno.Cart.Infrastructure.Repositories;
using Leno.Cart.Infrastructure.Services;
using Leno.SharedKernel.Abstractions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Leno.Cart.Infrastructure.Dependencies;

/// <summary>
/// 购物车域基础设施层 DI 注册入口。
/// 注册 DbContext、工作单元、仓储、Redis 缓存、防腐层、应用服务实现与 FluentValidation 校验器。
/// 调用方在表现层 Program.cs 调用 <c>services.AddCartInfrastructure(configuration)</c>。
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <param name="connectionStringName">连接字符串名称，默认 <c>CartDb</c>。</param>
    public static IServiceCollection AddCartInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        string connectionStringName = "CartDb")
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddDbContext<CartDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString(connectionStringName);
            options.UseSqlServer(connectionString);
        });

        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddScoped<ICartRepository, EfCoreCartRepository>();

        // 价格防腐层：通过 typed HttpClient 调用商品域内部 API，BaseAddress 来自 ServiceUrls:ProductApi
        services.AddHttpClient<ICartPriceService, CartPriceService>(client =>
        {
            var baseAddress = configuration["ServiceUrls:ProductApi"] ?? "http://localhost:5150";
            client.BaseAddress = new Uri(baseAddress);
        });
        services.AddSingleton<RedisCartCache>();

        services.AddScoped<ICartAppService, CartAppService>();

        services.AddValidatorsFromAssembly(typeof(ICartAppService).Assembly);

        return services;
    }

    /// <summary>
    /// 注册购物车域的 MassTransit 集成事件消费者。
    /// 在表现层调用 <c>AddLenoInfrastructure(configuration, cfg => cfg.AddCartConsumers())</c>。
    /// 含订单创建事件消费者（清空已结算项）。
    /// </summary>
    public static IBusRegistrationConfigurator AddCartConsumers(this IBusRegistrationConfigurator configurator)
    {
        ArgumentNullException.ThrowIfNull(configurator);

        // 订单创建后清空购物车已结算项
        configurator.AddConsumer<OrderCreatedEventConsumer>();

        return configurator;
    }
}
