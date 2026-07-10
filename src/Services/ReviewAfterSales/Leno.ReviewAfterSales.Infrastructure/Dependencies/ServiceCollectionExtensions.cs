using Leno.ReviewAfterSales.Domain.Repositories;
using Leno.ReviewAfterSales.Domain.Services;
using Leno.ReviewAfterSales.Infrastructure.Consumers;
using Leno.ReviewAfterSales.Infrastructure.ReadModels;
using Leno.ReviewAfterSales.Infrastructure.Repositories;
using Leno.ReviewAfterSales.Infrastructure.Services;
using Leno.SharedKernel.Abstractions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Leno.ReviewAfterSales.Infrastructure.Dependencies;

/// <summary>
/// 评价与售后域基础设施层 DI 注册入口。
/// 注册 DbContext、工作单元、仓储与防腐层实现；MassTransit 消费者经 <see cref="AddReviewAfterSalesConsumers"/> 注册。
/// 调用方在表现层 Program.cs 调用 <c>services.AddReviewAfterSalesInfrastructure(configuration)</c>。
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <param name="connectionStringName">连接字符串名称，默认 <c>ReviewAfterSalesDb</c>。</param>
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

        // 防腐层实现（占位，实际部署替换为 HTTP 调用订单域 API）
        services.AddScoped<IReviewEligibilityChecker, ReviewEligibilityChecker>();
        services.AddScoped<IAfterSalesEligibilityChecker, AfterSalesEligibilityChecker>();

        return services;
    }

    /// <summary>
    /// 注册评价与售后域的 MassTransit 消费者（集成事件消费者 + ES 读模型同步消费者）。
    /// 在表现层调用 <c>AddLenoInfrastructure(configuration, cfg => cfg.AddReviewAfterSalesConsumers())</c>。
    /// </summary>
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
