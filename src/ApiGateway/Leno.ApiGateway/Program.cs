using Leno.ApiGateway.Extensions;
using Leno.Infrastructure.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// YARP 反向代理从配置加载路由
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// Consul 服务发现 + 动态 Destination 解析器（替换 YARP 默认解析器）
builder.Services.AddConsulServiceDiscovery(builder.Configuration);
builder.Services.AddConsulDestinationResolver();

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

// YARP 反向代理端点
app.MapReverseProxy();

app.Run();

// 使 Program 类对 WebApplicationFactory<Program> 可见（Task 7 集成测试需要）
public partial class Program { }
