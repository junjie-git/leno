using Leno.Infrastructure.Configuration;
using Leno.Infrastructure.Dependencies;
using Leno.Infrastructure.Persistence;
using Leno.Infrastructure.ServiceDiscovery;
using Leno.Infrastructure.Telemetry;
using Leno.SellerShop.Infrastructure;
using Leno.SellerShop.Infrastructure.Dependencies;

var builder = WebApplication.CreateBuilder(args);

// Serilog 结构化日志 + OpenTelemetry 分布式追踪 + Consul 服务自注册
builder.Host.UseLenoSerilog(builder.Configuration, "leno-seller-shop-api");
builder.AddLenoOpenTelemetry();
builder.AddConsulServiceRegistration("leno-seller-shop-api");

// 一站式注册：共享内核基础设施 + 鉴权 + 健康检查 + Controllers + OpenAPI + 卖家域消费者 + 卖家与店铺管理域基础设施
builder.Services.AddLenoApi<SellerShopDbContext>(
    builder.Configuration,
    "leno-seller-shop-api",
    cfg => cfg.AddSellerShopConsumers(),
    s => s.AddSellerShopInfrastructure(builder.Configuration));

// 启用 Consul KV 配置中心
builder.AddLenoConsulConfig();

// 启动前校验敏感配置
if (!builder.Configuration.ValidateSensitiveConfig())
{
    var missing = builder.Configuration.GetMissingSensitiveConfigKeys();
    var logger = builder.Services.BuildServiceProvider().GetRequiredService<ILogger<Program>>();
    logger.LogWarning("敏感配置缺失：{MissingKeys}", string.Join(", ", missing));
    // 生产环境拒绝启动，开发环境仅警告
    if (!builder.Environment.IsDevelopment())
    {
        throw new InvalidOperationException($"敏感配置缺失：{string.Join(", ", missing)}");
    }
}

var app = builder.Build();

// 一站式中间件管线：OpenAPI + 全局异常 + 内部 API Key + 鉴权 + 健康检查端点 + Controllers
app.UseLenoPipeline();

// 启动时执行 EF Core 迁移（带 Redis 分布式锁，避免多实例并发冲突）
await app.Services.MigrateWithLockAsync<SellerShopDbContext>();
app.Run();
