using System.Text;
using Leno.Infrastructure.Auth;
using Leno.Infrastructure.Dependencies;
using Leno.Infrastructure.Logging;
using Leno.Infrastructure.Middleware;
using Leno.Infrastructure.ServiceDiscovery;
using Leno.Infrastructure.Telemetry;
using Leno.SystemAdmin.Infrastructure;
using Leno.SystemAdmin.Infrastructure.Dependencies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Serilog 结构化日志（JSON 输出 + Application/Environment/TraceId 富化）
builder.Host.UseSerilog((context, _, configuration) =>
{
    var appName = context.Configuration["Application:Name"] ?? "leno-system-admin-api";
    SerilogConfig.ConfigureDefaults(
        configuration, appName, context.HostingEnvironment.EnvironmentName)
        .ReadFrom.Configuration(context.Configuration.GetSection("Serilog"));
});

// OpenTelemetry 分布式追踪（ASP.NET Core / HttpClient / EF Core / MassTransit 自动埋点 + OTLP 导出）
builder.AddLenoOpenTelemetry();

// 共享内核基础设施：JWT、当前用户上下文、事件总线（含系统管理域消费者）、Redis、ES、健康检查
builder.Services.AddLenoInfrastructure(builder.Configuration, cfg => cfg.AddSystemAdminConsumers());
builder.Services.AddInternalApiKeyAuth(builder.Configuration);

// 系统管理域基础设施：DbContext、工作单元、仓储、缓存、Quartz 调度器、特性开关评估器
builder.Services.AddSystemAdminInfrastructure(builder.Configuration);

// Consul 服务自注册（启动时注册，关闭时注销，健康检查路径 /health/live）
builder.AddConsulServiceRegistration("leno-system-admin-api");

builder.Services.AddHealthChecks()
    .AddDbContextCheck<SystemAdminDbContext>(tags: ["ready"]);

builder.Services.AddControllers();

builder.Services.AddOpenApi();

// 认证配置（支持 JwtBearer 与 GatewayHeader 两种模式，灰度切换）
var authMode = builder.Configuration["Auth:Mode"] ?? "JwtBearer";

builder.Services.AddAuthentication(authMode == "GatewayHeader"
    ? "GatewayHeader"
    : JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
    {
        var jwtOpts = builder.Configuration.GetSection("Jwt").Get<JwtOptions>()
            ?? throw new InvalidOperationException("Jwt 配置节缺失");
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOpts.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOpts.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOpts.SecretKey)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    })
    .AddScheme<GatewayAuthOptions, GatewayAuthHandler>("GatewayHeader", _ => { });

builder.Services.AddAuthorization();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseMiddleware<InternalApiKeyMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => !check.Tags.Contains("ready")
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.MapControllers();

app.Run();
