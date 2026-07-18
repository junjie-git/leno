using FluentValidation;
using Leno.Infrastructure.Cqrs;
using Leno.Infrastructure.EventBus;
using Leno.Infrastructure.Persistence;
using Leno.Product.Application;
using Leno.Product.Application.Queries;
using Leno.Product.Application.Services;
using Leno.Product.Domain.Repositories;
using Leno.Product.Domain.Services;
using Leno.Product.Infrastructure.Consumers;
using Leno.Product.Infrastructure.EventBus;
using Leno.Product.Infrastructure.ReadModels;
using Leno.Product.Infrastructure.Repositories;
using Leno.Product.Infrastructure.Services;
using Leno.SharedKernel.Abstractions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Leno.Product.Infrastructure.Dependencies;

/// <summary>
/// 商品域基础设施层 DI 注册入口。
/// 注册 DbContext、工作单元、仓储、防腐层、ES 搜索服务、应用服务实现与 FluentValidation 校验器。
/// 调用方在表现层 Program.cs 调用 <c>services.AddProductInfrastructure(configuration)</c>。
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <param name="connectionStringName">连接字符串名称，默认 <c>ProductDb</c>。</param>
    public static IServiceCollection AddProductInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        string connectionStringName = "ProductDb")
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddDbContext<ProductDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString(connectionStringName);
            options.UseSqlServer(connectionString);
        });

        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork<ProductDbContext>>();

        // 注册 Product BC 领域事件到集成事件翻译器
        services.AddSingleton<IIntegrationEventMapper, ProductIntegrationEventMapper>();

        services.AddScoped<ISPURepository, EfCoreSPURepository>();
        services.AddScoped<ICategoryRepository, EfCoreCategoryRepository>();
        services.AddScoped<IBrandRepository, EfCoreBrandRepository>();
        services.AddScoped<IStockBaselineRepository, EfCoreStockBaselineRepository>();
        services.AddScoped<IPriceHistoryRepository, EfCorePriceHistoryRepository>();

        services.AddScoped<IProductQueryService, EfCoreProductQueryService>();
        services.AddScoped<IProductSearchService, ProductSearchService>();
        services.AddScoped<IProductUniquenessChecker, ProductUniquenessChecker>();

        // CQRS 读侧：ES 读模型访问器（Application 端口，Infrastructure 实现）
        services.AddScoped<IProductReadModelAccessor, ProductReadModelAccessor>();

        services.AddScoped<ISPUAppService, SPUAppService>();
        services.AddScoped<ICategoryAppService, CategoryAppService>();
        services.AddScoped<IBrandAppService, BrandAppService>();
        services.AddScoped<IInventoryAppService, InventoryAppService>();
        services.AddScoped<IProductInternalQueryService, ProductInternalQueryService>();

        services.AddValidatorsFromAssembly(typeof(ISPUAppService).Assembly);

        // CQRS 读侧：扫描 Application 程序集注册所有 IQueryHandler<TQuery, TResult>
        services.AddQueryHandlers(typeof(ISPUAppService).Assembly);

        return services;
    }

    /// <summary>
    /// 注册商品域的 MassTransit 集成事件消费者。
    /// 在表现层调用 <c>AddLenoInfrastructure(configuration, cfg => cfg.AddProductConsumers())</c>。
    /// 含店铺状态事件消费者（联动商品可售性）与 ES 读模型同步消费者。
    /// </summary>
    public static IBusRegistrationConfigurator AddProductConsumers(this IBusRegistrationConfigurator configurator)
    {
        ArgumentNullException.ThrowIfNull(configurator);

        // 店铺状态联动商品可售性
        configurator.AddConsumer<ShopSuspendedEventConsumer>();
        configurator.AddConsumer<ShopResumedEventConsumer>();
        configurator.AddConsumer<ShopClosedEventConsumer>();

        // 评价评分增量同步到 ES 读模型（不再回写 SPU 聚合）
        configurator.AddConsumer<SpuReviewSubmittedSummaryConsumer>();
        configurator.AddConsumer<SpuReviewHiddenSummaryConsumer>();

        // ES 读模型同步
        configurator.AddConsumer<ProductPublishedReadModelSyncConsumer>();
        configurator.AddConsumer<ProductTakenDownReadModelSyncConsumer>();
        configurator.AddConsumer<StockAdjustedEventConsumer>();

        return configurator;
    }
}
