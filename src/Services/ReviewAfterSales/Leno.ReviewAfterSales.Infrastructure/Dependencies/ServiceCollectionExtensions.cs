using AppServices = Leno.ReviewAfterSales.Application.Services;
using InfraServices = Leno.ReviewAfterSales.Infrastructure.Services;
using Leno.Infrastructure.EventBus;
using Leno.Infrastructure.Persistence;
using Leno.ReviewAfterSales.Application;
using Leno.ReviewAfterSales.Application.Services;
using Leno.ReviewAfterSales.Domain.Repositories;
using Leno.ReviewAfterSales.Domain.Services;
using Leno.ReviewAfterSales.Infrastructure.Consumers;
using Leno.ReviewAfterSales.Infrastructure.EventBus;
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

        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork<ReviewAfterSalesDbContext>>();

        // 注册 ReviewAfterSales BC 领域事件到集成事件翻译器
        services.AddSingleton<IIntegrationEventMapper, ReviewAfterSalesIntegrationEventMapper>();

        services.AddScoped<IReviewRepository, EfCoreReviewRepository>();
        services.AddScoped<IAfterSalesRepository, EfCoreAfterSalesRepository>();

        // 防腐层实现（通过 HttpClient 调用跨域内部接口）
        var paymentApiUrl = configuration["ServiceUrls:PaymentApi"] ?? "http://localhost:5155";
        var orderApiUrl = configuration["ServiceUrls:OrderApi"] ?? "http://localhost:5154";

        services.AddHttpClient<IPaymentInfoQueryService, InfraServices.PaymentInfoQueryService>(c => c.BaseAddress = new Uri(paymentApiUrl));
        services.AddHttpClient<IAfterSalesEligibilityChecker, InfraServices.AfterSalesEligibilityChecker>(c => c.BaseAddress = new Uri(orderApiUrl));
        services.AddHttpClient<IReviewEligibilityChecker, InfraServices.ReviewEligibilityChecker>(c => c.BaseAddress = new Uri(orderApiUrl));

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
