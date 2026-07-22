using System.Threading.RateLimiting;
using Leno.Cart.Api.GrpcServices;
using Leno.Cart.Infrastructure;
using Leno.Cart.Infrastructure.Dependencies;
using Leno.Infrastructure.Configuration;
using Leno.Infrastructure.Dependencies;
using Leno.Infrastructure.Persistence;
using Leno.Infrastructure.ServiceDiscovery;
using Leno.Infrastructure.Telemetry;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Serilog 结构化日志 + OpenTelemetry 分布式追踪 + Consul 服务自注册
builder.Host.UseLenoSerilog(builder.Configuration, "leno-cart-api");
builder.AddLenoOpenTelemetry();
builder.AddConsulServiceRegistration("leno-cart-api");

// 一站式注册：共享内核基础设施 + 鉴权 + 健康检查 + Controllers + OpenAPI + 购物车域消费者 + 购物车域基础设施
builder.Services.AddLenoApi<CartDbContext>(
    builder.Configuration,
    "leno-cart-api",
    cfg => cfg.AddCartConsumers(),
    s => s.AddCartInfrastructure(builder.Configuration));

// P1-7：匿名购物车接口限流策略（IP 维度，10 次/分钟），防止未认证接口滥用
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("anonymous-cart", httpContext =>
    {
        var remoteIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(remoteIp, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0
        });
    });
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

// P1-7：限流中间件需在端点路由之后、控制器执行之前生效，以读取 [EnableRateLimiting] 元数据
app.UseRouting();
app.UseRateLimiter();

// 一站式中间件管线：OpenAPI + 全局异常 + 内部 API Key + 鉴权 + 健康检查端点 + Controllers
app.UseLenoPipeline();

// M4 双轨方案：启用 gRPC 服务端（仅当 AntiCorruption:UseGrpc=true 时映射）
if (builder.Configuration.GetValue<bool>("AntiCorruption:UseGrpc"))
{
    app.MapGrpcService<CartGrpcService>();
}

// 启动时执行 EF Core 迁移（带 Redis 分布式锁，避免多实例并发冲突）
await app.Services.MigrateWithLockAsync<CartDbContext>();
app.Run();
