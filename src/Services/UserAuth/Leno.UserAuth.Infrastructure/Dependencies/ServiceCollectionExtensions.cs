using FluentValidation;
using Leno.SharedKernel.Abstractions;
using Leno.UserAuth.Application;
using Leno.UserAuth.Application.Abstractions;
using Leno.UserAuth.Application.Services;
using Leno.UserAuth.Domain.Repositories;
using Leno.UserAuth.Domain.Services;
using Leno.UserAuth.Infrastructure.Audit;
using Leno.UserAuth.Infrastructure.Auth;
using Leno.UserAuth.Infrastructure.Repositories;
using Leno.UserAuth.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Leno.UserAuth.Infrastructure.Dependencies;

/// <summary>
/// 用户与认证授权域基础设施层 DI 注册入口。
/// 注册 DbContext、工作单元、仓储、领域服务实现、令牌与刷新令牌存储、审计拦截器与 FluentValidation 校验器。
/// 调用方在 Presentation 层 Program.cs 调用 <c>services.AddUserAuthInfrastructure(configuration)</c>。
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 注册用户与认证授权域的全部基础设施服务。
    /// </summary>
    /// <param name="connectionStringName">连接字符串名称，默认 <c>UserAuthDb</c>。</param>
    public static IServiceCollection AddUserAuthInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        string connectionStringName = "UserAuthDb")
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<PasswordHashOptions>(configuration.GetSection("PasswordHash"));

        services.AddDbContext<UserAuthDbContext>((sp, options) =>
        {
            var connectionString = configuration.GetConnectionString(connectionStringName);
            options.UseSqlServer(connectionString);
            options.AddInterceptors(sp.GetRequiredService<AuditLogInterceptor>());
        });

        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddScoped<IUserRepository, EfCoreUserRepository>();
        services.AddScoped<IAddressRepository, EfCoreAddressRepository>();
        services.AddScoped<IAuditLogRepository, EfCoreAuditLogRepository>();
        services.AddScoped<IOAuthClientRepository, EfCoreOAuthClientRepository>();

        services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
        services.AddScoped<IUserUniquenessChecker, UserUniquenessChecker>();

        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<ITokenVerifier, TotpTokenVerifier>();
        services.AddSingleton<IRefreshTokenStore, InMemoryRefreshTokenStore>();

        // JWT 黑名单吊销服务：登出时写入 Redis 黑名单（与网关共用 Redis 实例与 Key 格式）
        services.AddScoped<IJwtRevocationService, JwtRevocationService>();

        services.AddScoped<IUserInternalQueryService, UserInternalQueryService>();

        services.AddScoped<IPermissionRepository, EfCorePermissionRepository>();

        services.AddScoped<AuditLogInterceptor>();
        services.AddScoped<AuditLogMiddleware>();

        // OAuth2 第三方登录
        services.AddHttpClient<GoogleOAuth2Client>();
        services.AddHttpClient<WeChatOAuth2Client>();
        services.AddHttpClient<AlipayOAuth2Client>();

        services.AddScoped<IExternalAuthService, GoogleOAuth2Client>();
        services.AddScoped<IExternalAuthService, WeChatOAuth2Client>();
        services.AddScoped<IExternalAuthService, AlipayOAuth2Client>();

        services.AddScoped<OAuth2ProviderResolver>();
        services.AddScoped<IOAuth2ProviderResolver>(sp => sp.GetRequiredService<OAuth2ProviderResolver>());

        // AES-256 加密服务（用于 OAuth ClientSecret）
        var aesKey = configuration["OAuth2:AesKey"];
        if (!string.IsNullOrWhiteSpace(aesKey))
        {
            services.AddSingleton<IClientSecretEncryptionService>(new AesEncryptionService(aesKey));
        }

        // 应用服务
        services.AddScoped<IUserAppService, UserAppService>();
        services.AddScoped<IUserAdminAppService, UserAdminAppService>();
        services.AddScoped<IAddressAppService, AddressAppService>();
        services.AddScoped<IPermissionAppService, PermissionAppService>();
        services.AddScoped<IOAuthClientAppService, OAuthClientAppService>();
        services.AddScoped<IAccountAppService, AccountAppService>();

        services.AddValidatorsFromAssembly(typeof(IUserAppService).Assembly);

        return services;
    }
}
