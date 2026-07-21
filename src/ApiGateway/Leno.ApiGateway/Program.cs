using Leno.ApiGateway.Extensions;
using Leno.ApiGateway.Middleware;
using Leno.ApiGateway.Services;
using Leno.Infrastructure.Auth;
using Leno.Infrastructure.HealthChecks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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

// Phase 7 F2 安全修复：JWT 黑名单服务（依赖 IConnectionMultiplexer，需在 AddGatewayRedis 之后注册）
// 三层保障：Redis Pub/Sub 实时同步 + 本地 MemoryCache（TTL 对齐，避免泄漏）+ 启动预热订阅
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<JwtBlacklistService>();
builder.Services.AddSingleton<IJwtBlacklistService>(sp => sp.GetRequiredService<JwtBlacklistService>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<JwtBlacklistService>());

// Phase 4：限流策略（global/default/seckill/per-user，Redis 启用时使用 RedisSlidingWindowRateLimiter）
builder.Services.AddGatewayRateLimiter(builder.Configuration);

// Phase 4：超时策略（default/seckill/upload/internal）
builder.Services.AddGatewayTimeouts(builder.Configuration);

// Phase 6：响应缓存（Redis + Pub/Sub 失效）
builder.Services.AddGatewayCaching(builder.Configuration);

// Phase 6：统一 CORS（Origin 从 Consul KV 热更新）
builder.Services.AddGatewayCors(builder.Configuration);

// Phase 7 F2 安全修复：JWT 本地验签（P0-4）
// 始终注册 JWT 服务（不在 builder 阶段读取 Jwt:Enabled 开关）：
// 测试通过 ConfigureAppConfiguration 覆盖配置，这些覆盖在 builder.Build() 之后才生效，
// 因此服务注册必须无条件进行，配置开关仅在中间件应用阶段判断。
// 绑定 Jwt 配置节到 JwtOptions（测试可通过 services.Configure<JwtOptions> 覆盖）
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
// 注册 JwtTokenGenerator 单例（依赖 IOptions<JwtOptions>，使测试覆盖生效）
builder.Services.AddSingleton<JwtTokenGenerator>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

// 延迟从 DI 解析 JwtTokenGenerator 构造验签参数：
// OptionsBuilder.Configure<TDep> 在 IOptions<JwtBearerOptions>.Value 首次访问时执行，
// 此时所有 ConfigureTestServices 的 JwtOptions 覆盖已就位
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<JwtTokenGenerator>((options, generator) =>
    {
        options.TokenValidationParameters = generator.BuildValidationParameters();
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = ctx =>
            {
                ctx.Response.StatusCode = 401;
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// Phase 6：协议转换预留注册表（当前无实现）
builder.Services.AddProtocolTranslators();

// Plan 10 (M6) Task 10：BFF 聚合转发（4 个 /api/bff/* 端点，3 秒超时，部分失败返回 partial）
builder.Services.AddBffForwarding();

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

// 在 Build 之后读取 Jwt:Enabled 开关：此时测试通过 ConfigureAppConfiguration 注入的覆盖已合并进最终配置。
// 在 builder 阶段读取会早于测试覆盖生效，导致禁用开关无法拦截 JWT 中间件。
var jwtEnabled = app.Configuration.GetValue("Jwt:Enabled", true);

// 存活探针：仅检查网关进程存活
app.MapGet("/health/live", () => Results.Ok(new { status = "Healthy" }));

// 就绪探针与 HealthChecksUI 仪表盘
app.MapLenoHealthChecks();
app.MapLenoHealthChecksUI();

// 中间件管道顺序：
//   1. UseObservability — 访问日志 + 指标中间件 + /metrics 端点
//   2. UseCors — Phase 6：预检 OPTIONS 在缓存之前处理
//   3. UseAuthentication — Phase 7：JWT 本地验签，填充 HttpContext.User
//   4. JwtBlacklistMiddleware — Phase 7 F2：命中黑名单返回 401，递增 gateway_blacklist_hits
//   5. 白名单路由中间件 — Phase 7：login/register/refresh-token/health/metrics 放行，否则要求已认证
//   6. UseAuthorization — Phase 7：授权检查（当前无 [Authorize] 端点，由白名单中间件统一拦截）
//   7. FallbackResponseMiddleware — 503 降级
//   8. UseResponseCompression — Phase 6：响应压缩
//   9. CacheMiddleware — Phase 6：命中即短路，未命中透传到 YARP
//  10. UseRateLimiter — 路由级限流
//  11. UseRequestTimeouts — 路由级超时
//  12. MapReverseProxy — YARP 反向代理
app.UseObservability(builder.Configuration);
app.UseCors();
if (jwtEnabled)
{
    app.UseAuthentication();

    // JWT 黑名单拦截：紧随 UseAuthentication 之后（已填充 User），命中黑名单返回 401 并递增计数器
    app.UseMiddleware<JwtBlacklistMiddleware>();

    // 白名单路由 + 未认证拦截：在 UseAuthentication 之后（已填充 User）、UseAuthorization 之前
    app.Use(async (context, next) =>
    {
        var path = context.Request.Path.Value ?? string.Empty;
        var isWhitelisted = path.StartsWith("/api/auth/login", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/auth/register", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/auth/refresh-token", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/health", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/metrics", StringComparison.OrdinalIgnoreCase);

        if (isWhitelisted)
        {
            await next();
            return;
        }

        if (context.User?.Identity?.IsAuthenticated != true)
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { code = 401, message = "未认证" });
            return;
        }

        await next();
    });

    app.UseAuthorization();
}
app.UseMiddleware<FallbackResponseMiddleware>();
app.UseResponseCompression();
app.UseMiddleware<CacheMiddleware>();
app.UseRateLimiter();
app.UseRequestTimeouts();

// YARP 反向代理端点
app.MapReverseProxy();

// Plan 10 (M6) Task 10：BFF 聚合端点（/api/bff/*）
// 注册顺序在 MapReverseProxy 之后：ASP.NET Core 端点路由按 URL 模式与 Order 匹配，
// BFF 路由前缀 /api/bff/ 不在 YARP ReverseProxy:Routes 配置内，互不冲突
app.MapControllers();

app.Run();

// 使 Program 类对 WebApplicationFactory<Program> 可见（集成测试需要）
public partial class Program { }
