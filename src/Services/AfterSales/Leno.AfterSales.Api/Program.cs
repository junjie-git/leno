using Leno.AfterSales.Application;
using Leno.AfterSales.Application.Services;
using Leno.AfterSales.Domain.Repositories;
using Leno.AfterSales.Domain.Services;
using Leno.AfterSales.Infrastructure;
using Leno.AfterSales.Infrastructure.Repositories;
using Leno.AfterSales.Infrastructure.Services;
using Leno.Infrastructure.AntiCorruption;
using Leno.Infrastructure.Configuration;
using Leno.Infrastructure.Dependencies;
using Leno.Infrastructure.Persistence;
using Leno.Infrastructure.ServiceDiscovery;
using Leno.Infrastructure.Telemetry;
using Leno.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;

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

        // 防腐层 HttpClient 实现：支付信息查询 + 订单状态查询
        var paymentApiUrl = builder.Configuration["ServiceUrls:PaymentApi"] ?? "http://localhost:5155";
        var orderApiUrl = builder.Configuration["ServiceUrls:OrderApi"] ?? "http://localhost:5154";

        services.AddHttpClient<PaymentInfoQueryService>(c => c.BaseAddress = new Uri(paymentApiUrl))
            .AddAntiCorruptionPolicies();
        services.AddHttpClient<HttpOrderStatusProvider>(c => c.BaseAddress = new Uri(orderApiUrl))
            .AddAntiCorruptionPolicies();

        // 防腐层接口绑定到 HttpClient 实现（UseGrpc=false 时的默认模式）
        services.AddScoped<IPaymentInfoQueryService>(sp => sp.GetRequiredService<PaymentInfoQueryService>());
        services.AddScoped<IOrderStatusProvider>(sp => sp.GetRequiredService<HttpOrderStatusProvider>());

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
