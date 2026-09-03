using FluentValidation;
using Leno.Infrastructure.Auth;
using Leno.Infrastructure.EventBus;
using Leno.Infrastructure.Persistence;
using Leno.SharedKernel.Abstractions;
using Leno.UserAuth.Application;
using Leno.UserAuth.Application.Abstractions;
using Leno.UserAuth.Application.Services;
using Leno.UserAuth.Domain.Repositories;
using Leno.UserAuth.Domain.Services;
using Leno.UserAuth.Infrastructure.Audit;
using Leno.UserAuth.Infrastructure.Auth;
using Leno.UserAuth.Infrastructure.EventBus;
using Leno.UserAuth.Infrastructure.Options;
using Leno.UserAuth.Infrastructure.Repositories;
using Leno.UserAuth.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

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
    /// <param name="hostEnvironment">宿主环境，用于校验 InMemory 刷新令牌存储仅用于 Development。若为 null 则尝试从 DI 解析。</param>
    public static IServiceCollection AddUserAuthInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        string connectionStringName = "UserAuthDb",
        IHostEnvironment? hostEnvironment = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // 若未显式传入环境，尝试从已注册的 IHostEnvironment 单例中解析（用于支持 Program.cs 旧调用形式）
        hostEnvironment ??= TryResolveHostEnvironment(services);

        services.Configure<PasswordHashOptions>(configuration.GetSection("PasswordHash"));
        services.Configure<OAuth2Options>(configuration.GetSection("OAuth2"));

        // P1-9：JWT 吊销服务配置（IOptionsMonitor 支持热更新，TTL 与 JWT 有效期联动）
        services.Configure<JwtRevocationOptions>(configuration.GetSection(JwtRevocationOptions.SectionName));

        services.AddDbContext<UserAuthDbContext>((sp, options) =>
        {
            var connectionString = configuration.GetConnectionString(connectionStringName);
            options.UseSqlServer(connectionString);
            options.AddInterceptors(sp.GetRequiredService<AuditLogInterceptor>());
        });

        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork<UserAuthDbContext>>();

        // 注册 UserAuth BC 领域事件到集成事件翻译器
        services.AddSingleton<IIntegrationEventMapper, UserAuthIntegrationEventMapper>();

        services.AddScoped<IUserRepository, EfCoreUserRepository>();
        services.AddScoped<IAddressRepository, EfCoreAddressRepository>();
        services.AddScoped<IAuditLogRepository, EfCoreAuditLogRepository>();
        services.AddScoped<IOAuthClientRepository, EfCoreOAuthClientRepository>();
        services.AddScoped<INotificationPreferencesRepository, EfCoreNotificationPreferencesRepository>();
        services.AddScoped<IFavoriteRepository, EfCoreFavoriteRepository>();
        services.AddScoped<IBrowseHistoryRepository, EfCoreBrowseHistoryRepository>();

        services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
        services.AddScoped<IUserUniquenessChecker, UserUniquenessChecker>();

        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<ITokenVerifier, TotpTokenVerifier>();
        RegisterRefreshTokenStore(services, configuration, hostEnvironment);

        // OAuth state / 2FA 临时令牌 / 密码重置令牌 存储抽象（P1-3）
        // 全部基于 Redis 实现复用 IConnectionMultiplexer 单例；用 AddScoped 以便在请求作用域内复用连接。
        services.AddScoped<IOAuthStateStore>(sp =>
        {
            var multiplexer = sp.GetRequiredService<IConnectionMultiplexer>();
            var logger = sp.GetRequiredService<ILogger<RedisOAuthStateStore>>();
            return new RedisOAuthStateStore(multiplexer, logger);
        });
        services.AddScoped<ITwoFactorTempTokenStore>(sp =>
        {
            var multiplexer = sp.GetRequiredService<IConnectionMultiplexer>();
            var logger = sp.GetRequiredService<ILogger<RedisTwoFactorTempTokenStore>>();
            return new RedisTwoFactorTempTokenStore(multiplexer, logger);
        });
        services.AddScoped<IPasswordResetTokenStore>(sp =>
        {
            var multiplexer = sp.GetRequiredService<IConnectionMultiplexer>();
            var logger = sp.GetRequiredService<ILogger<RedisPasswordResetTokenStore>>();
            return new RedisPasswordResetTokenStore(multiplexer, logger);
        });

        // JWT 黑名单吊销服务：登出时写入 Redis 黑名单（与网关共用 Redis 实例与 Key 格式）
        services.AddScoped<IJwtRevocationService, JwtRevocationService>();

        services.AddScoped<IUserInternalQueryService, UserInternalQueryService>();

        services.AddScoped<IPermissionRepository, EfCorePermissionRepository>();

        services.AddScoped<AuditLogInterceptor>();
        // AuditLogMiddleware 不做 DI 注册：中间件由 Program.cs 的 UseMiddleware 约定激活，
        // 若注册为 Scoped 会在 Development 环境 ValidateOnBuild 时因 RequestDelegate 无法解析而启动失败

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
        // P2-12: 启动期 fail-fast，AesKey 缺失时直接抛异常，
        // 避免运行时静默跳过加密导致 OAuthClientAppService 写入明文 ClientSecret。
        var aesKey = configuration["OAuth2:AesKey"];
        if (string.IsNullOrWhiteSpace(aesKey))
        {
            throw new InvalidOperationException(
                "OAuth2:AesKey 配置缺失，无法启动 UserAuth 服务。" +
                "请在配置中提供 256 位 AES 密钥（Base64 编码 32 字节）。");
        }
        services.AddSingleton<IClientSecretEncryptionService>(new AesEncryptionService(aesKey));

        // 应用服务
        services.AddScoped<IUserAppService, UserAppService>();
        services.AddScoped<IUserAdminAppService, UserAdminAppService>();
        services.AddScoped<IAddressAppService, AddressAppService>();
        services.AddScoped<IPermissionAppService, PermissionAppService>();
        services.AddScoped<IOAuthClientAppService, OAuthClientAppService>();
        services.AddScoped<IAccountAppService, AccountAppService>();
        services.AddScoped<INotificationPreferencesAppService, NotificationPreferencesAppService>();
        services.AddScoped<IFavoritesAppService, FavoritesAppService>();
        services.AddScoped<IBrowseHistoryAppService, BrowseHistoryAppService>();

        services.AddValidatorsFromAssembly(typeof(IUserAppService).Assembly);

        return services;
    }

    /// <summary>
    /// 按配置与环境切换刷新令牌存储：
    /// 默认 Redis；仅当显式配置 <c>RefreshToken:Provider=InMemory</c> 且环境为 Development 时使用 InMemoryRefreshTokenStore；
    /// 生产环境配置 InMemory 直接抛出异常，避免静默退化导致多实例刷新令牌失效。
    /// </summary>
    private static void RegisterRefreshTokenStore(
        IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment? hostEnvironment)
    {
        var provider = configuration["RefreshToken:Provider"] ?? "Redis";
        var jwtOptions = configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
        var refreshTokenExpiry = TimeSpan.FromDays(jwtOptions.RefreshTokenExpiryDays > 0
            ? jwtOptions.RefreshTokenExpiryDays
            : 7);

        if (string.Equals(provider, "InMemory", StringComparison.OrdinalIgnoreCase))
        {
            if (hostEnvironment is null || !hostEnvironment.IsDevelopment())
            {
                throw new InvalidOperationException(
                    "InMemoryRefreshTokenStore 仅允许在 Development 环境使用；" +
                    "生产环境必须配置 RefreshToken:Provider=Redis 与 Redis:Connection。");
            }

            services.AddSingleton<IRefreshTokenStore, InMemoryRefreshTokenStore>();
            return;
        }

        if (!string.Equals(provider, "Redis", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"不支持的 RefreshToken:Provider={provider}，仅支持 Redis 或 InMemory（仅 Development）。");
        }

        services.AddSingleton<IRefreshTokenStore>(sp =>
        {
            var multiplexer = sp.GetRequiredService<IConnectionMultiplexer>();
            var logger = sp.GetRequiredService<ILogger<RedisRefreshTokenStore>>();
            return new RedisRefreshTokenStore(multiplexer, refreshTokenExpiry, logger);
        });
    }

    /// <summary>
    /// 从已注册的服务描述符中查找 <see cref="IHostEnvironment"/> 实例（仅对 AddSingleton(instance) 形式有效）。
    /// </summary>
    private static IHostEnvironment? TryResolveHostEnvironment(IServiceCollection services)
    {
        for (var i = 0; i < services.Count; i++)
        {
            var descriptor = services[i];
            if (descriptor.ServiceType == typeof(IHostEnvironment)
                && descriptor.ImplementationInstance is IHostEnvironment env)
            {
                return env;
            }
        }

        return null;
    }
}

