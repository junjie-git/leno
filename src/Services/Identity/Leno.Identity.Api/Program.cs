using Leno.Identity.Application;
using Leno.Identity.Infrastructure;
using Leno.Identity.Infrastructure.Dependencies;
using Leno.Infrastructure.Configuration;
using Leno.Infrastructure.Dependencies;
using Leno.Infrastructure.Persistence;
using Leno.Infrastructure.ServiceDiscovery;
using Leno.Infrastructure.Telemetry;

var builder = WebApplication.CreateBuilder(args);

// Serilog 结构化日志 + OpenTelemetry 分布式追踪 + Consul 服务自注册
builder.Host.UseLenoSerilog(builder.Configuration, "leno-identity-api");
builder.AddLenoOpenTelemetry();
builder.AddConsulServiceRegistration("leno-identity-api");

// 一站式注册：共享内核基础设施 + 鉴权 + 健康检查 + Controllers + OpenAPI + Identity BC 基础设施
// Identity BC 不再暴露 gRPC 服务端（角色查询由 AccessControl BC 提供），仅作为 gRPC 客户端消费方
builder.Services.AddLenoApi<IdentityDbContext>(
    builder.Configuration,
    "leno-identity-api",
    configureInfrastructure: s => s.AddIdentityInfrastructure(builder.Configuration));

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
await app.Services.MigrateWithLockAsync<IdentityDbContext>();
app.Run();

/// <summary>
/// Program 类部分声明，供 WebApplicationFactory&lt;Program&gt; 在集成测试中引用。
/// </summary>
public partial class Program;
