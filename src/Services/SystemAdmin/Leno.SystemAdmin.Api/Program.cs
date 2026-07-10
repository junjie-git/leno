using System.Text;
using Leno.Infrastructure.Auth;
using Leno.Infrastructure.Dependencies;
using Leno.Infrastructure.Middleware;
using Leno.SystemAdmin.Infrastructure.Dependencies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// 共享内核基础设施：JWT、当前用户上下文、事件总线（含系统管理域消费者）、Redis、ES、健康检查
builder.Services.AddLenoInfrastructure(builder.Configuration, cfg => cfg.AddSystemAdminConsumers());
builder.Services.AddInternalApiKeyAuth(builder.Configuration);

// 系统管理域基础设施：DbContext、工作单元、仓储、缓存、Quartz 调度器、特性开关评估器
builder.Services.AddSystemAdminInfrastructure(builder.Configuration);

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

app.MapControllers();

app.Run();
