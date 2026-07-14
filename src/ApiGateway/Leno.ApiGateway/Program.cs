using Leno.ApiGateway.Extensions;
using Leno.ApiGateway.Middleware;
using Leno.Infrastructure.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// YARP 反向代理从配置加载路由（含 RateLimiterPolicy/TimeoutPolicy 字段）
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// Phase 1：Consul 服务发现 + 动态 Destination 解析器
builder.Services.AddConsulServiceDiscovery(builder.Configuration);
builder.Services.AddConsulDestinationResolver();

// Phase 4：Redis（用于分布式限流计数器）
builder.Services.AddGatewayRedis(builder.Configuration);

// Phase 4：限流策略（global/default/seckill/per-user，Redis 启用时使用 RedisSlidingWindowRateLimiter）
builder.Services.AddGatewayRateLimiter(builder.Configuration);

// Phase 4：超时策略（default/seckill/upload/internal）
builder.Services.AddGatewayTimeouts(builder.Configuration);

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

// 中间件管道顺序（Phase 4 新增）：
//   1. FallbackResponseMiddleware — 拦截 YARP 503 改写为降级 JSON（在 MapReverseProxy 之前）
//   2. UseRateLimiter — 应用路由级 RateLimiterPolicy（ASP.NET Core 内建）
//   3. UseRequestTimeouts — 应用路由级 TimeoutPolicy（由 AddGatewayTimeouts 隐式注册）
//   4. MapReverseProxy — YARP 反向代理（含 CircuitBreaker/Retry/HttpRequest.ActivityTimeout）
app.UseMiddleware<FallbackResponseMiddleware>();
app.UseRateLimiter();
app.UseRequestTimeouts();

// YARP 反向代理端点
app.MapReverseProxy();

app.Run();

// 使 Program 类对 WebApplicationFactory<Program> 可见（集成测试需要）
public partial class Program { }
