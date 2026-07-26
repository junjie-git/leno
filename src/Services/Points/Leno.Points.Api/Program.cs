using Leno.Infrastructure.Configuration;
using Leno.Infrastructure.Dependencies;
using Leno.Infrastructure.Persistence;
using Leno.Infrastructure.ServiceDiscovery;
using Leno.Infrastructure.Telemetry;
using Leno.Points.Application;
using Leno.Points.Application.Services;
using Leno.Points.Api.GrpcServices;
using Leno.Points.Infrastructure;
using Leno.Points.Infrastructure.Dependencies;

var builder = WebApplication.CreateBuilder(args);

// Serilog 结构化日志 + OpenTelemetry 分布式追踪 + Consul 服务自注册
builder.Host.UseLenoSerilog(builder.Configuration, "leno-points-api");
builder.AddLenoOpenTelemetry();
builder.AddConsulServiceRegistration("leno-points-api");

// 一站式注册：共享内核基础设施 + 鉴权 + 健康检查 + Controllers + OpenAPI + Points BC 消费者 + Points BC 基础设施
// 双轨期：与旧 PointsMembership BC 并行运行，由 feature flag PointsMembershipSplit:Enabled 控制切流比例
builder.Services.AddLenoApi<PointsDbContext>(
    builder.Configuration,
    "leno-points-api",
    cfg => cfg.AddPointsConsumers(),
    s =>
    {
        s.AddPointsInfrastructure(builder.Configuration);
        // 注册 Points BC 应用服务（Application 层）
        s.AddScoped<IPointsAppService, PointsAppService>();
        s.AddScoped<ICheckInAppService, CheckInAppService>();
        s.AddScoped<IExchangeCouponAppService, ExchangeCouponAppService>();
        s.AddScoped<IAwardAppService, AwardAppService>();
        s.AddScoped<ITaskAppService, TaskAppService>();
        s.AddScoped<IPointsRuleAppService, PointsRuleAppService>();
        s.AddScoped<IPointsInternalAppService, PointsInternalAppService>();
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

// M4 双轨方案 + Spec §2.2.1 gRPC 重建：仅当 AntiCorruption:UseGrpc=true 时映射 Points gRPC 服务
if (builder.Configuration.GetValue<bool>("AntiCorruption:UseGrpc"))
{
    app.MapGrpcService<PointsGrpcService>();
    app.Logger.LogInformation("Points gRPC service mapped.");
}

// 启动时执行 EF Core 迁移（带 Redis 分布式锁，避免多实例并发冲突）
// 双轨期迁移独立运行，不影响旧 points_membership_db
await app.Services.MigrateWithLockAsync<PointsDbContext>();
app.Run();

/// <summary>
/// Program 类部分声明，供 WebApplicationFactory&lt;Program&gt; 在集成测试中引用。
/// </summary>
public partial class Program;
