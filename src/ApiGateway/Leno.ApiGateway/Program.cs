using Leno.ApiGateway.Extensions;
using Leno.ApiGateway.Middleware;
using Leno.Infrastructure.HealthChecks;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Serilog 替换默认日志
builder.Host.UseSerilog((context, services, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration.GetSection("Serilog")));

// 可观测性（Serilog + OpenTelemetry + Prometheus + TracingTransform）
// 注意：AddObservability 内部已调用 AddReverseProxy().LoadFromConfig().AddTransforms<TracingTransform>()
builder.Services.AddObservability(builder.Configuration);

// Phase 1：Consul 服务发现 + 动态 Destination 解析器
builder.Services.AddConsulServiceDiscovery(builder.Configuration);
builder.Services.AddConsulDestinationResolver();

// Phase 4：Redis（用于分布式限流计数器）
builder.Services.AddGatewayRedis(builder.Configuration);

// Phase 4：限流策略（global/default/seckill/per-user，Redis 启用时使用 RedisSlidingWindowRateLimiter）
builder.Services.AddGatewayRateLimiter(builder.Configuration);

// Phase 4：超时策略（default/seckill/upload/internal）
builder.Services.AddGatewayTimeouts(builder.Configuration);

// Phase 6：响应缓存（Redis + Pub/Sub 失效）
builder.Services.AddGatewayCaching(builder.Configuration);

// Phase 6：统一 CORS（Origin 从 Consul KV 热更新）
builder.Services.AddGatewayCors(builder.Configuration);

// Phase 6：协议转换预留注册表（当前无实现）
builder.Services.AddProtocolTranslators();

// Phase 6：响应压缩
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
});

// HealthChecksUI 仪表盘
builder.Services.AddLenoHealthChecksUI(builder.Configuration);

// 网关自身健康检查：存活探针 + Consul 连通性就绪检查
#pragma warning disable CA1861
builder.Services.AddHealthChecks()
    .AddUrlGroup(
        new Uri(builder.Configuration["Consul:Url"] ?? "http://localhost:8500"),
        "consul",
        tags: new[] { "ready" });
#pragma warning restore CA1861

var app = builder.Build();

// 存活探针：仅检查网关进程存活
app.MapGet("/health/live", () => Results.Ok(new { status = "Healthy" }));

// 就绪探针与 HealthChecksUI 仪表盘
app.MapLenoHealthChecks();
app.MapLenoHealthChecksUI();

// 中间件管道顺序：
//   1. UseObservability — 访问日志 + 指标中间件 + /metrics 端点
//   2. UseCors — Phase 6：预检 OPTIONS 在缓存之前处理
//   3. FallbackResponseMiddleware — 503 降级
//   4. UseResponseCompression — Phase 6：响应压缩
//   5. CacheMiddleware — Phase 6：命中即短路，未命中透传到 YARP
//   6. UseRateLimiter — 路由级限流
//   7. UseRequestTimeouts — 路由级超时
//   8. MapReverseProxy — YARP 反向代理
app.UseObservability(builder.Configuration);
app.UseCors();
app.UseMiddleware<FallbackResponseMiddleware>();
app.UseResponseCompression();
app.UseMiddleware<CacheMiddleware>();
app.UseRateLimiter();
app.UseRequestTimeouts();

// YARP 反向代理端点
app.MapReverseProxy();

app.Run();

// 使 Program 类对 WebApplicationFactory<Program> 可见（集成测试需要）
public partial class Program { }
