using FluentValidation;
using Leno.Cart.Application;
using Leno.Cart.Application.Abstractions;
using Leno.Cart.Application.InternalQueryServices;
using Leno.Cart.Application.Services;
using Leno.Cart.Domain.Repositories;
using Leno.Cart.Domain.Services;
using Leno.Cart.Infrastructure.Caching;
using Leno.Cart.Infrastructure.Consumers;
using Leno.Cart.Infrastructure.EventBus;
using Leno.Cart.Infrastructure.Repositories;
using Leno.Cart.Infrastructure.Services;
using Leno.Cart.Infrastructure.Services.Grpc;
using Leno.Infrastructure.AntiCorruption;
using Leno.Infrastructure.EventBus;
using Leno.Infrastructure.Persistence;
using Leno.SharedContracts.Grpc.Product.V1;
using Leno.SharedKernel.Abstractions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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

        // 注册 CartSkuIndexDomainEventDispatcher 与 CartUnitOfWork：
        // 在落库前分发 SkuAddedToCartEvent/SkuRemovedFromCartEvent 到反向索引服务，维护购物车-SKU 索引一致
        services.AddScoped<CartSkuIndexDomainEventDispatcher>();
        services.AddScoped<IUnitOfWork, CartUnitOfWork>();

        // 注册 Cart BC 领域事件到集成事件翻译器
        services.AddSingleton<IIntegrationEventMapper, CartIntegrationEventMapper>();

        services.AddScoped<ICartRepository, EfCoreCartRepository>();

        // 价格防腐层 HttpClient 实现（保留作为降级备份）：BaseAddress 来自 ServiceUrls:ProductApi
        services.AddHttpClient<CartPriceService>(client =>
        {
            var baseAddress = configuration["ServiceUrls:ProductApi"] ?? "http://localhost:5150";
            client.BaseAddress = new Uri(baseAddress);
        })
            .AddAntiCorruptionPolicies();

        // M4 双轨方案：gRPC 客户端 + 熔断器 + Dispatcher（仅当 UseGrpc=true 时生效）
        var antiCorruptionOptions = configuration.GetSection("AntiCorruption").Get<AntiCorruptionOptions>() ?? new AntiCorruptionOptions();
        if (antiCorruptionOptions.UseGrpc)
        {
            var productGrpcEndpoint = antiCorruptionOptions.GrpcEndpoints.GetValueOrDefault("Product")
                ?? throw new InvalidOperationException("AntiCorruption:GrpcEndpoints:Product 配置缺失");

            services.AddGrpcClient<ProductInternalService.ProductInternalServiceClient>(options =>
            {
                options.Address = new Uri(productGrpcEndpoint);
            });
            services.AddScoped<GrpcCartPriceService>();

            services.AddKeyedSingleton<CircuitBreakerState>("product", (sp, _) =>
            {
                var opts = sp.GetRequiredService<IOptionsMonitor<AntiCorruptionOptions>>().CurrentValue;
                var cbOpts = opts.CircuitBreaker ?? new CircuitBreakerOptions();
                return new CircuitBreakerState(
                    "product",
                    cbOpts.FailureThreshold,
                    cbOpts.SuccessThreshold,
                    TimeSpan.FromSeconds(cbOpts.OpenDurationSeconds));
            });

            services.AddScoped<AntiCorruptionDispatcher<ICartPriceService>>(sp =>
            {
                var httpImpl = sp.GetRequiredService<CartPriceService>();
                var grpcImpl = sp.GetService<GrpcCartPriceService>();
                var options = sp.GetRequiredService<IOptionsMonitor<AntiCorruptionOptions>>();
                var logger = sp.GetRequiredService<ILogger<AntiCorruptionDispatcher<ICartPriceService>>>();
                var cb = sp.GetRequiredKeyedService<CircuitBreakerState>("product");
                return new AntiCorruptionDispatcher<ICartPriceService>(
                    httpImpl, grpcImpl, options, logger, "product", cb);
            });
            services.AddScoped<CartPriceDispatcherAdapter>();
            services.AddScoped<ICartPriceService>(sp =>
                sp.GetRequiredService<CartPriceDispatcherAdapter>());
        }
        else
        {
            // UseGrpc=false：直接注册 HttpClient 实现（兼容期）
            services.AddScoped<ICartPriceService>(sp =>
                sp.GetRequiredService<CartPriceService>());
        }

        // 商品快照防腐层 HttpClient 实现（保留作为降级备份）
        services.AddHttpClient<ProductSnapshotAntiCorruptionService>(client =>
        {
            var baseAddress = configuration["ServiceUrls:ProductApi"] ?? "http://localhost:5150";
            client.BaseAddress = new Uri(baseAddress);
        })
            .AddAntiCorruptionPolicies();

        // M4 双轨方案：商品快照防腐层 gRPC 客户端 + Dispatcher（仅当 UseGrpc=true 时生效）
        if (antiCorruptionOptions.UseGrpc)
        {
            // ProductInternalServiceClient 已在 CartPriceService 双轨时注册，此处不重复注册
            services.AddScoped<GrpcProductSnapshotAntiCorruptionClient>();

            // CircuitBreakerState("product") 已在 CartPriceService 双轨时注册为 KeyedSingleton，此处复用

            services.AddScoped<AntiCorruptionDispatcher<IProductSnapshotAntiCorruption>>(sp =>
            {
                var httpImpl = sp.GetRequiredService<ProductSnapshotAntiCorruptionService>();
                var grpcImpl = sp.GetService<GrpcProductSnapshotAntiCorruptionClient>();
                var options = sp.GetRequiredService<IOptionsMonitor<AntiCorruptionOptions>>();
                var logger = sp.GetRequiredService<ILogger<AntiCorruptionDispatcher<IProductSnapshotAntiCorruption>>>();
                var cb = sp.GetRequiredKeyedService<CircuitBreakerState>("product");
                return new AntiCorruptionDispatcher<IProductSnapshotAntiCorruption>(
                    httpImpl, grpcImpl, options, logger, "product", cb);
            });
            services.AddScoped<ProductSnapshotDispatcherAdapter>();
            services.AddScoped<IProductSnapshotAntiCorruption>(sp =>
                sp.GetRequiredService<ProductSnapshotDispatcherAdapter>());
        }
        else
        {
            // UseGrpc=false：直接注册 HttpClient 实现
            services.AddScoped<IProductSnapshotAntiCorruption>(sp =>
                sp.GetRequiredService<ProductSnapshotAntiCorruptionService>());
        }

        // 购物车-SKU 反向索引：基于 Redis Set，商品事件消费时定位受影响购物车
        services.AddScoped<ICartSkuIndexService, CartSkuIndexService>();

        services.AddSingleton<RedisCartCache>();

        // 匿名购物车：Redis 仓储 + 应用服务
        services.AddSingleton<IAnonymousCartRepository, RedisAnonymousCartRepository>();
        services.AddScoped<IAnonymousCartAppService, AnonymousCartAppService>();

        services.AddScoped<ICartAppService, CartAppService>();

        // M4 双轨方案：注册跨 BC 内部查询服务（供 CartGrpcService 复用）
        services.AddScoped<ICartInternalQueryService, CartInternalQueryService>();

        services.AddValidatorsFromAssembly(typeof(ICartAppService).Assembly);

        return services;
    }

    /// <summary>
    /// 注册购物车域的 MassTransit 集成事件消费者。
    /// 在表现层调用 <c>AddLenoInfrastructure(configuration, cfg => cfg.AddCartConsumers())</c>。
    /// 含订单创建事件消费者（清空已结算项）与商品事件消费者（联动商品可售性）。
    /// </summary>
    public static IBusRegistrationConfigurator AddCartConsumers(this IBusRegistrationConfigurator configurator)
    {
        ArgumentNullException.ThrowIfNull(configurator);

        // 订单创建后清空购物车已结算项
        configurator.AddConsumer<OrderCreatedEventConsumer>();

        // 商品事件联动购物车
        configurator.AddConsumer<ProductTakenDownEventConsumer>();
        configurator.AddConsumer<ProductPublishedEventConsumer>();
        configurator.AddConsumer<ProductUpdatedEventConsumer>();

        return configurator;
    }
}
