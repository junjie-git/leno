using Leno.AfterSales.Application;
using Leno.AfterSales.Application.Services;
using Leno.AfterSales.Domain.Repositories;
using Leno.AfterSales.Domain.Services;
using Leno.AfterSales.Infrastructure;
using Leno.AfterSales.Infrastructure.Repositories;
using Leno.AfterSales.Infrastructure.Services;
using Leno.AfterSales.Infrastructure.Services.Grpc;
using Leno.Infrastructure.AntiCorruption;
using Leno.Infrastructure.Configuration;
using Leno.Infrastructure.Dependencies;
using Leno.Infrastructure.Persistence;
using Leno.Infrastructure.ServiceDiscovery;
using Leno.Infrastructure.Telemetry;
using Leno.SharedContracts.Grpc.Order.V1;
using Leno.SharedContracts.Grpc.Payment.V1;
using Leno.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Serilog 结构化日志 + OpenTelemetry 分布式追踪 + Consul 服务自注册
builder.Host.UseLenoSerilog(builder.Configuration, "leno-aftersales-api");
builder.AddLenoOpenTelemetry();
builder.AddConsulServiceRegistration("leno-aftersales-api");

// 一站式注册：共享内核基础设施 + 鉴权 + 健康检查 + Controllers + OpenAPI + 售后域基础设施
builder.Services.AddLenoApi<AfterSalesDbContext>(
    builder.Configuration,
    "leno-aftersales-api",
    configureInfrastructure: services =>
    {
        services.AddDbContext<AfterSalesDbContext>(options =>
            options.UseSqlServer(builder.Configuration.GetConnectionString("AfterSalesDb")));

        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork<AfterSalesDbContext>>();

        // 仓储
        services.AddScoped<IAfterSalesRepository, EfCoreAfterSalesRepository>();

        // 防腐层单轨化（双轨下线 B2）：gRPC 唯一实现，无 HTTP/降级路径；
        // GrpcEndpoints 配置缺失时启动即抛 InvalidOperationException（fail-fast，杜绝静默降级导致的数据不一致）。
        var antiCorruptionOptions = builder.Configuration.GetSection("AntiCorruption").Get<AntiCorruptionOptions>() ?? new AntiCorruptionOptions();

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

        // Payment 防腐点（IPaymentInfoQueryService）：gRPC 客户端 + 熔断器 + Dispatcher
        var paymentGrpcEndpoint = antiCorruptionOptions.GrpcEndpoints.GetValueOrDefault("Payment")
            ?? throw new InvalidOperationException("AntiCorruption:GrpcEndpoints:Payment 配置缺失");

        services.AddGrpcClient<PaymentInternalService.PaymentInternalServiceClient>(options =>
        {
            options.Address = new Uri(paymentGrpcEndpoint);
        });
        services.AddScoped<GrpcPaymentInfoQueryService>();

        services.AddKeyedSingleton<CircuitBreakerState>("payment", (sp, _) =>
        {
            var opts = sp.GetRequiredService<IOptionsMonitor<AntiCorruptionOptions>>().CurrentValue;
            var cbOpts = opts.CircuitBreaker ?? new CircuitBreakerOptions();
            return new CircuitBreakerState(
                "payment",
                cbOpts.FailureThreshold,
                cbOpts.SuccessThreshold,
                TimeSpan.FromSeconds(cbOpts.OpenDurationSeconds));
        });

        services.AddScoped<AntiCorruptionDispatcher<IPaymentInfoQueryService>>(sp =>
        {
            var grpcImpl = sp.GetRequiredService<GrpcPaymentInfoQueryService>();
            var logger = sp.GetRequiredService<ILogger<AntiCorruptionDispatcher<IPaymentInfoQueryService>>>();
            var cb = sp.GetRequiredKeyedService<CircuitBreakerState>("payment");
            return new AntiCorruptionDispatcher<IPaymentInfoQueryService>(
                grpcImpl, logger, "payment", cb);
        });
        services.AddScoped<PaymentInfoDispatcherAdapter>();
        services.AddScoped<IPaymentInfoQueryService>(sp =>
            sp.GetRequiredService<PaymentInfoDispatcherAdapter>());

        // 资格校验器（依赖 IOrderStatusProvider 与 IAfterSalesRepository）
        services.AddScoped<IAfterSalesEligibilityChecker, AfterSalesEligibilityChecker>();

        // 应用服务
        services.AddScoped<IAfterSalesAppService, AfterSalesAppService>();
    });

// 启用 Consul KV 配置中心
builder.AddLenoConsulConfig();

var app = builder.Build();

// 启动前校验敏感配置
if (!app.Configuration.ValidateSensitiveConfig())
{
    var missing = app.Configuration.GetMissingSensitiveConfigKeys();
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogWarning("敏感配置缺失：{MissingKeys}", string.Join(", ", missing));
    // 生产环境拒绝启动，开发环境仅警告
    if (!app.Environment.IsDevelopment())
    {
        throw new InvalidOperationException($"敏感配置缺失：{string.Join(", ", missing)}");
    }
}

// 一站式中间件管线：OpenAPI + 全局异常 + 内部 API Key + 鉴权 + 健康检查端点 + Controllers
app.UseLenoPipeline();

// 启动时执行 EF Core 迁移（带 Redis 分布式锁，避免多实例并发冲突）
await app.Services.MigrateWithLockAsync<AfterSalesDbContext>();
app.Run();
