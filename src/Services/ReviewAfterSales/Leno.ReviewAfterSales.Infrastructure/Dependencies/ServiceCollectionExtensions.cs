using AppServices = Leno.ReviewAfterSales.Application.Services;
using InfraServices = Leno.ReviewAfterSales.Infrastructure.Services;
using Leno.ReviewAfterSales.Application;
using Leno.ReviewAfterSales.Application.Services;
using Leno.ReviewAfterSales.Domain.Repositories;
using Leno.ReviewAfterSales.Domain.Services;
using Leno.ReviewAfterSales.Infrastructure.Consumers;
using Leno.ReviewAfterSales.Infrastructure.ReadModels;
using Leno.ReviewAfterSales.Infrastructure.Repositories;
using Leno.SharedKernel.Abstractions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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

        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddScoped<IReviewRepository, EfCoreReviewRepository>();
        services.AddScoped<IAfterSalesRepository, EfCoreAfterSalesRepository>();

        // 防腐层实现（占位）
        services.AddScoped<IReviewEligibilityChecker, InfraServices.ReviewEligibilityChecker>();
        services.AddScoped<IAfterSalesEligibilityChecker, InfraServices.AfterSalesEligibilityChecker>();
        services.AddScoped<IPaymentInfoQueryService, InfraServices.PaymentInfoQueryService>();

        // 应用服务
        services.AddScoped<IReviewAppService, AppServices.ReviewAppService>();
        services.AddScoped<IAfterSalesAppService, AppServices.AfterSalesAppService>();

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
