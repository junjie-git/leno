using System.Text;
using Leno.Infrastructure.Auth;
using Leno.Infrastructure.Dependencies;
using Leno.Infrastructure.Logging;
using Leno.Infrastructure.Middleware;
using Leno.Infrastructure.ServiceDiscovery;
using Leno.Infrastructure.Telemetry;
using Leno.Promotion.Infrastructure;
using Leno.Promotion.Infrastructure.Dependencies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Serilog 结构化日志（JSON 输出 + Application/Environment/TraceId 富化）
builder.Host.UseSerilog((context, _, configuration) =>
{
    var appName = context.Configuration["Application:Name"] ?? "leno-promotion-api";
    SerilogConfig.ConfigureDefaults(
        configuration, appName, context.HostingEnvironment.EnvironmentName)
        .ReadFrom.Configuration(context.Configuration.GetSection("Serilog"));
});

// OpenTelemetry 分布式追踪（ASP.NET Core / HttpClient / EF Core / MassTransit 自动埋点 + OTLP 导出）
builder.AddLenoOpenTelemetry();

// 共享内核基础设施：JWT 生成器、当前用户上下文、事件总线（含促销域消费者）、Redis、ES、健康检查
builder.Services.AddLenoInfrastructure(builder.Configuration, cfg => cfg.AddPromotionConsumers());
builder.Services.AddInternalApiKeyAuth(builder.Configuration);

// 促销域基础设施：DbContext、工作单元、仓储、Redis 秒杀库存、防腐层、应用服务、FluentValidation 校验器
builder.Services.AddPromotionInfrastructure(builder.Configuration);

// Consul 服务自注册（启动时注册，关闭时注销，健康检查路径 /health/live）
builder.AddConsulServiceRegistration("leno-promotion-api");

// 后台服务：优惠券过期处理
builder.Services.AddHostedService<Leno.Promotion.Api.BackgroundServices.CouponExpiryService>();
builder.Services.AddHealthChecks()
    .AddDbContextCheck<PromotionDbContext>(tags: ["ready"]);

builder.Services.AddControllers();

builder.Services.AddOpenApi();

// JWT Bearer 鉴权
var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>()
    ?? throw new InvalidOperationException("Jwt 配置节缺失");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SecretKey)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

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
