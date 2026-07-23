using FluentValidation;
using Grpc.Net.Client;
using Leno.Identity.Application;
using Leno.Identity.Application.Services;
using Leno.Identity.Domain.Repositories;
using Leno.Identity.Domain.Services;
using Leno.Identity.Infrastructure.EventBus;
using Leno.Identity.Infrastructure.Repositories;
using Leno.Identity.Infrastructure.Services;
using Leno.Infrastructure.EventBus;
using Leno.Infrastructure.Persistence;
using Leno.SharedContracts.Grpc.AccessControl.V1;
using Leno.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Leno.Identity.Infrastructure.Dependencies;

/// <summary>
/// Identity BC 基础设施层 DI 注册入口（3.6 AuthN/AuthZ 拆分）。
/// 注册 DbContext、工作单元、仓储、领域服务实现、JWT 令牌服务、认证应用服务与 AccessControl gRPC 客户端。
/// 调用方在 Presentation 层 Program.cs 调用 <c>services.AddIdentityInfrastructure(configuration)</c>。
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 注册 Identity BC 的全部基础设施服务。
    /// </summary>
    /// <param name="connectionStringName">连接字符串名称，默认 <c>IdentityDb</c>。</param>
    public static IServiceCollection AddIdentityInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        string connectionStringName = "IdentityDb")
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // 1. DbContext
        services.AddDbContext<IdentityDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString(connectionStringName);
            options.UseSqlServer(connectionString);
        });

        // 2. 工作单元（依赖 IIntegrationEventMapper，须先于本行注册）
        services.AddSingleton<IIntegrationEventMapper, IdentityIntegrationEventMapper>();
        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork<IdentityDbContext>>();

        // 3. 仓储
        services.AddScoped<IUserRepository, EfCoreUserRepository>();
        services.AddScoped<IRefreshTokenRepository, EfCoreRefreshTokenRepository>();
        services.AddScoped<ITwoFactorSessionRepository, EfCoreTwoFactorSessionRepository>();
        services.AddScoped<IOAuthClientRepository, EfCoreOAuthClientRepository>();

        // 4. 领域服务实现
        services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
        services.AddScoped<IUserUniquenessChecker, UserUniquenessChecker>();
        services.AddScoped<ITokenVerifier, TotpTokenVerifier>();

        // 5. 配置选项
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<PasswordHashOptions>(configuration.GetSection("PasswordHash"));

        // 6. AccessControl BC gRPC 客户端注册（JwtTokenService 通过此客户端调用 GetUserRoles RPC 填充角色 claims）
        //    地址从 ServiceUrls:AccessControlApi 读取（与 AntiCorruption:GrpcEndpoints:AccessControl 二选一，前者保留向后兼容）
        var accessControlUrl = configuration["ServiceUrls:AccessControlApi"]
            ?? configuration["AntiCorruption:GrpcEndpoints:AccessControl"]
            ?? "http://localhost:8082";

        // .NET 10 + Grpc.Net.Client 默认在 macOS/Windows 上不再要求额外开关即可使用 HTTP/2 over plaintext；
        // 此处显式配置确保目标地址支持 HTTP/2（AccessControl BC 服务端为 Kestrel HTTP/2 端点）。
        services.AddGrpcClient<AccessControlService.AccessControlServiceClient>(options =>
        {
            options.Address = new Uri(accessControlUrl);
        });

        // 7. 应用服务（位于 Application 层，由 Infrastructure 注册以集中管理 DI 装配）
        services.AddScoped<JwtTokenService>();
        services.AddScoped<IAuthenticationAppService, AuthenticationAppService>();
        services.AddScoped<TwoFactorAppService>();

        // 8. FluentValidation 校验器自动扫描
        services.AddValidatorsFromAssembly(typeof(IAuthenticationAppService).Assembly);

        return services;
    }
}
