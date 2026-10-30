using AppServices = Leno.Review.Application.Services;
using InfraServices = Leno.Review.Infrastructure.Services;
using Leno.Infrastructure.Abstractions;
using Leno.Infrastructure.AntiCorruption;
using Leno.Infrastructure.EventBus;
using Leno.Infrastructure.Persistence;
using Leno.Infrastructure.Storage;
using Leno.Review.Application;
using Leno.Review.Application.InternalQueryServices;
using Leno.Review.Application.Services;
using Leno.Review.Domain.Repositories;
using Leno.Review.Domain.Services;
using Leno.Review.Infrastructure.EventBus;
using Leno.Review.Infrastructure.ReadModels;
using Leno.Review.Infrastructure.Repositories;
using Leno.Review.Infrastructure.Services.Grpc;
using Leno.SharedContracts.Grpc.Order.V1;
using Leno.SharedContracts.Grpc.Product.V1;
using Leno.SharedKernel.Abstractions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leno.Review.Infrastructure.Dependencies;

/// <summary>
/// 评价域基础设施层 DI 注册入口（评价 BC 独立维护）。
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddReviewInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        string connectionStringName = "ReviewDb")
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddDbContext<ReviewDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString(connectionStringName);
            options.UseSqlServer(connectionString);
        });

        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork<ReviewDbContext>>();

        // 注册 Review BC 领域事件到集成事件翻译器
        services.AddSingleton<IIntegrationEventMapper, ReviewIntegrationEventMapper>();

        // 审计 3.11：文件签名校验器，防止伪装扩展名上传非图片文件
        services.AddSingleton<IFileSignatureDetector, FileSignatureDetector>();

        services.AddScoped<IReviewRepository, EfCoreReviewRepository>();

        // 防腐层单轨化（双轨下线 B2）：gRPC 唯一实现，无 HTTP/降级路径；
        // GrpcEndpoints 配置缺失时启动即抛 InvalidOperationException（fail-fast，杜绝静默降级导致的数据不一致）。
        var antiCorruptionOptions = configuration.GetSection("AntiCorruption").Get<AntiCorruptionOptions>() ?? new AntiCorruptionOptions();

        // Order 防腐点（IOrderStatusProvider）：gRPC 客户端 + 熔断器 + Dispatcher
        var orderGrpcEndpoint = antiCorruptionOptions.GrpcEndpoints.GetValueOrDefault("Order")
            ?? throw new InvalidOperationException("AntiCorruption:GrpcEndpoints:Order 配置缺失");

        services.AddGrpcClient<OrderInternalService.OrderInternalServiceClient>(options =>
        {
            options.Address = new Uri(orderGrpcEndpoint);
        });
        services.AddScoped<GrpcOrderStatusProvider>();

        services.AddKeyedSingleton<CircuitBreakerState>("order", (sp, _) =>
        {
            var opts = sp.GetRequiredService<IOptionsMonitor<AntiCorruptionOptions>>().CurrentValue;
            var cbOpts = opts.CircuitBreaker ?? new CircuitBreakerOptions();
            return new CircuitBreakerState(
                "order",
                cbOpts.FailureThreshold,
                cbOpts.SuccessThreshold,
                TimeSpan.FromSeconds(cbOpts.OpenDurationSeconds));
        });

        services.AddScoped<AntiCorruptionDispatcher<IOrderStatusProvider>>(sp =>
        {
            var grpcImpl = sp.GetRequiredService<GrpcOrderStatusProvider>();
            var logger = sp.GetRequiredService<ILogger<AntiCorruptionDispatcher<IOrderStatusProvider>>>();
            var cb = sp.GetRequiredKeyedService<CircuitBreakerState>("order");
            return new AntiCorruptionDispatcher<IOrderStatusProvider>(
                grpcImpl, logger, "order", cb);
        });
        services.AddScoped<OrderStatusDispatcherAdapter>();
        services.AddScoped<IOrderStatusProvider>(sp =>
            sp.GetRequiredService<OrderStatusDispatcherAdapter>());

        // Product 防腐点（IProductInfoQueryService）：gRPC 唯一实现
        // （卖家侧评价列表按商品名称过滤场景；单 SPU 查询失败按 ACL 契约从映射中剔除，见 GrpcProductInfoQueryService）。
        var productGrpcEndpoint = antiCorruptionOptions.GrpcEndpoints.GetValueOrDefault("Product")
            ?? throw new InvalidOperationException("AntiCorruption:GrpcEndpoints:Product 配置缺失");

        services.AddGrpcClient<ProductInternalService.ProductInternalServiceClient>(options =>
        {
            options.Address = new Uri(productGrpcEndpoint);
        });
        services.AddScoped<GrpcProductInfoQueryService>();
        services.AddScoped<IProductInfoQueryService>(sp =>
            sp.GetRequiredService<GrpcProductInfoQueryService>());

        // 资格校验器（依赖 IOrderStatusProvider）
        services.AddScoped<IReviewEligibilityChecker, InfraServices.ReviewEligibilityChecker>();

        // 应用服务
        services.AddScoped<IReviewAppService, AppServices.ReviewAppService>();

        // 注册跨 BC 内部查询服务（供 ReviewGrpcService 复用）
        services.AddScoped<IReviewInternalQueryService, ReviewInternalQueryService>();

        // ES 索引初始化器（评价 BC 独立 reviews_v2 索引）
        services.AddHostedService<ReviewIndexInitializer>();

        return services;
    }

    public static IBusRegistrationConfigurator AddReviewConsumers(
        this IBusRegistrationConfigurator configurator)
    {
        ArgumentNullException.ThrowIfNull(configurator);

        // 评价读模型同步消费者：监听评价生命周期事件同步 ES reviews_v2 索引
        configurator.AddConsumer<ReviewReadModelSyncConsumer>();

        return configurator;
    }
}
