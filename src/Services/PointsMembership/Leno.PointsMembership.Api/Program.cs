using System.Text;
using Leno.Infrastructure.Auth;
using Leno.Infrastructure.Dependencies;
using Leno.Infrastructure.Logging;
using Leno.Infrastructure.Middleware;
using Leno.Infrastructure.ServiceDiscovery;
using Leno.Infrastructure.Telemetry;
using Leno.PointsMembership.Infrastructure;
using Leno.PointsMembership.Infrastructure.Dependencies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Serilog 结构化日志（JSON 输出 + Application/Environment/TraceId 富化）
builder.Host.UseSerilog((context, _, configuration) =>
{
    var appName = context.Configuration["Application:Name"] ?? "leno-points-api";
    SerilogConfig.ConfigureDefaults(
        configuration, appName, context.HostingEnvironment.EnvironmentName)
        .ReadFrom.Configuration(context.Configuration.GetSection("Serilog"));
});

// OpenTelemetry 分布式追踪（ASP.NET Core / HttpClient / EF Core / MassTransit 自动埋点 + OTLP 导出）
builder.AddLenoOpenTelemetry();

// 共享内核基础设施：JWT 生成器、当前用户上下文、事件总线（含积分会员域消费者）、Redis、ES、健康检查
builder.Services.AddLenoInfrastructure(builder.Configuration, cfg => cfg.AddPointsMembershipConsumers());
builder.Services.AddInternalApiKeyAuth(builder.Configuration);

// 积分会员域基础设施：DbContext、工作单元、仓储、积分抵扣防腐层、应用服务、FluentValidation 校验器
builder.Services.AddPointsMembershipInfrastructure(builder.Configuration);

// Consul 服务自注册（启动时注册，关闭时注销，健康检查路径 /health/live）
builder.AddConsulServiceRegistration("leno-points-api");

// 后台服务：会员成长值等级评估 + 积分过期处理
builder.Services.AddHostedService<Leno.PointsMembership.Api.BackgroundServices.MemberLevelEvaluationJob>();
builder.Services.AddHostedService<Leno.PointsMembership.Api.BackgroundServices.PointsExpiryService>();
builder.Services.AddHealthChecks()
    .AddDbContextCheck<PointsMembershipDbContext>(tags: ["ready"]);

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
