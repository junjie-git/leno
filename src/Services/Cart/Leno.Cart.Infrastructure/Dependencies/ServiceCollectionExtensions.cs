using FluentValidation;
using Leno.Cart.Application;
using Leno.Cart.Application.Abstractions;
using Leno.Cart.Application.InternalQueryServices;
using Leno.Cart.Application.Services;
using Leno.Cart.Domain.Repositories;
using Leno.Cart.Domain.Services;
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

        // P1-1：匿名购物车合并记录仓储，防止跨存储非原子操作导致重复合并
        services.AddScoped<ICartMergeRecordRepository, CartMergeRecordRepository>();

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
#pragma warning disable CS0618 // 阶段三 3.11：GrpcCartPriceService 已标记 [Obsolete]，保留作为降级备份
            services.AddScoped<GrpcCartPriceService>();
#pragma warning restore CS0618

            services.AddKeyedSingleton<CircuitBreakerState>("product", (sp, _) =>
            {
                // P1-13：注入 IOptionsMonitor 引用而非构造时读取 CurrentValue，
                // 使 CircuitBreakerState 每次状态判定时从 CurrentValue.CircuitBreaker 读取最新阈值，
                // 支持 Consul KV 热更新（原实现构造时冻结阈值，热更新不生效）
                var optionsMonitor = sp.GetRequiredService<IOptionsMonitor<AntiCorruptionOptions>>();
                return new CircuitBreakerState("product", optionsMonitor);
            });

            services.AddScoped<AntiCorruptionDispatcher<ICartPriceService>>(sp =>
            {
                var httpImpl = sp.GetRequiredService<CartPriceService>();
#pragma warning disable CS0618 // 阶段三 3.11：GrpcCartPriceService 已标记 [Obsolete]，保留作为降级备份
                var grpcImpl = sp.GetService<GrpcCartPriceService>();
#pragma warning restore CS0618
                var options = sp.GetRequiredService<IOptionsMonitor<AntiCorruptionOptions>>();
                var logger = sp.GetRequiredService<ILogger<AntiCorruptionDispatcher<ICartPriceService>>>();
                var cb = sp.GetRequiredKeyedService<CircuitBreakerState>("product");
                return new AntiCorruptionDispatcher<ICartPriceService>(
                    httpImpl, grpcImpl, options, logger, "product", cb);
            });
            services.AddScoped<CartPriceDispatcherAdapter>();
        }
        else
        {
            // UseGrpc=false：HttpClient 实现已通过 AddHttpClient<CartPriceService> 注册，
            // ICartPriceService 的最终注册由下方 SnapshotCartPriceService 装饰器统一处理
        }

        // 阶段三 3.11：Cart SKU 快照本地化配置 + 后台刷新队列
        // CartSnapshotOptions 绑定 Cart 配置节，支持 Consul KV 热更新（UseSkuSnapshot 开关灰度）
        services.Configure<CartSnapshotOptions>(configuration.GetSection(CartSnapshotOptions.SectionName));

        // 后台快照刷新队列：BackgroundService + Channel，Singleton 生命周期（要求 CreateScope 解析 Scoped 依赖）
        // IBackgroundSnapshotRefresher 同时由 SkuSnapshotRefreshQueue 实现，供 SnapshotCartPriceService 非阻塞入队
        // 显式注册 Singleton + 工厂式 HostedService，保证 IBackgroundSnapshotRefresher 与后台服务解析同一实例
        services.AddSingleton<SkuSnapshotRefreshQueue>();
        services.AddHostedService(sp => sp.GetRequiredService<SkuSnapshotRefreshQueue>());
        services.AddSingleton<IBackgroundSnapshotRefresher>(sp => sp.GetRequiredService<SkuSnapshotRefreshQueue>());

        // 阶段三 3.11：SnapshotCartPriceService 装饰器包装内部 ICartPriceService 实现
        // 优先读取本地 SkuSnapshot，过期/缺失时回退内部实现（CartPriceDispatcherAdapter 或 CartPriceService）并触发后台刷新
        // feature flag UseSkuSnapshot=false 时透传给内部实现，保持向后兼容
        services.AddScoped<ICartPriceService>(sp =>
        {
            // 按具体类型解析内部实现，避免 ICartPriceService 自解析递归
            ICartPriceService inner = antiCorruptionOptions.UseGrpc
                ? sp.GetRequiredService<CartPriceDispatcherAdapter>()
                : sp.GetRequiredService<CartPriceService>();

            var dbContext = sp.GetRequiredService<CartDbContext>();
            var refresher = sp.GetRequiredService<IBackgroundSnapshotRefresher>();
            var options = sp.GetRequiredService<IOptionsMonitor<CartSnapshotOptions>>();
            var logger = sp.GetRequiredService<ILogger<SnapshotCartPriceService>>();
            return new SnapshotCartPriceService(inner, dbContext, refresher, options, logger);
        });

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
        // 阶段三 3.11：SKU 级更新事件，直接刷新购物车本地快照（无需回调 ACL）
        configurator.AddConsumer<ProductSkuUpdatedEventConsumer>();

        return configurator;
    }
}
