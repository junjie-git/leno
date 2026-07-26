using FluentValidation;
using Grpc.Net.Client;
using Leno.Identity.Application;
using Leno.Identity.Application.Abstractions;
using Leno.Identity.Application.Services;
using Leno.Identity.Domain.Repositories;
using Leno.Identity.Domain.Services;
using Leno.Identity.Infrastructure.EventBus;
using Leno.Identity.Infrastructure.OAuth;
using Leno.Identity.Infrastructure.Repositories;
using Leno.Identity.Infrastructure.Security;
using Leno.Identity.Infrastructure.Services;
using Leno.Infrastructure.EventBus;
using Leno.Infrastructure.Persistence;
using Leno.Infrastructure.Security;
using Leno.SharedContracts.Grpc.AccessControl.V1;
using Leno.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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

        // 4. 领域服务实现 + 3.10 安全技术栈升级（Argon2id + PEPPER / HS256→RS256 / KMS 托管）
        //    4.1 配置选项（须先于服务注册，供 IOptions<T> 注入）
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<PasswordHashOptions>(configuration.GetSection(PasswordHashOptions.SectionName));
        services.Configure<JwtSigningOptions>(configuration.GetSection(JwtSigningOptions.SectionName));

        //    4.2 KMS 基础设施：默认 EnvironmentKms（环境变量 PEM 回退），生产可切换 AzureKeyVaultKms
        //        DG-4 务实推进：代码完整实现，实际 KMS 连接待生产验证，失败回退环境变量。
        services.AddSingleton<IKeyManagementService>(sp =>
        {
            var signingOptions = sp.GetRequiredService<IOptions<JwtSigningOptions>>().Value;
            if (signingOptions.UseAzureKeyVault && !string.IsNullOrWhiteSpace(signingOptions.KeyVaultUri))
            {
                // 生产环境：Azure Key Vault HSM 托管 RSA 密钥
                var credential = new Azure.Identity.DefaultAzureCredential();
                var akvLogger = sp.GetRequiredService<ILogger<AzureKeyVaultKms>>();
                return new AzureKeyVaultKms(
                    new Uri(signingOptions.KeyVaultUri),
                    credential,
                    Options.Create(signingOptions),
                    akvLogger);
            }

            // 开发/CI 回退：从环境变量 JWT_RSA_PRIVATE_KEY_PEM / JWT_RSA_PUBLIC_KEY_PEM 读取 PEM
            var envKmsLogger = sp.GetRequiredService<ILogger<EnvironmentKms>>();
            return new EnvironmentKms(Options.Create(signingOptions), envKmsLogger);
        });

        //    4.3 Argon2id 密码哈希栈：pepper 注入 + bcrypt 旧哈希兼容校验
        //        - BcryptPasswordVerifier：仅校验历史 bcrypt 哈希（不含 pepper）
        //        - IPepperProvider：KMS 解包 > 环境变量 PASSWORD_PEPPER > 静态配置
        //        - Argon2PasswordHasher：新签发 Argon2id + pepper，校验时自动识别算法
        services.AddSingleton<BcryptPasswordVerifier>();
        services.AddSingleton<IPepperProvider, PepperProvider>();
        services.AddSingleton<Leno.Infrastructure.Security.IPasswordHasher, Argon2PasswordHasher>();
        //        桥接：领域端口 IPasswordHasher（Hash/Verify）→ 基础设施 IPasswordHasher（HashPassword/VerifyPassword/DetectAlgorithm）
        services.AddScoped<Leno.Identity.Domain.Services.IPasswordHasher, IdentityPasswordHasherAdapter>();
        services.AddScoped<IBcryptToArgon2Migrator, BcryptToArgon2Migrator>();

        //    4.4 JWT 签名服务（HS256 / Dual / RS256，由 JwtSigning:SigningMode feature flag 控制）
        services.AddSingleton<IJwtSigningService, RsaJwtSigningService>();

        //    4.5 其他领域服务
        services.AddScoped<IUserUniquenessChecker, UserUniquenessChecker>();
        services.AddScoped<ITokenVerifier, TotpTokenVerifier>();

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

        // 7.1 OAuth2 / OIDC / SAML2 适配器与工厂（3.7 OAuth/SSO 通用化）
        //     适配器通过 HttpClientFactory 注册（typed client 模式），便于配置超时、重试与日志策略；
        //     Discovery 文档缓存在适配器内部静态字段，多实例共享。
        //     使用 factory delegate 解析具体类型，确保 HttpClient 通过 typed client 激活器注入。
        services.AddHttpClient<OidcProviderAdapter>();
        services.AddHttpClient<Saml2ProviderAdapter>();
        services.AddScoped<IOAuth2ProviderAdapter>(sp => sp.GetRequiredService<OidcProviderAdapter>());
        services.AddScoped<IOAuth2ProviderAdapter>(sp => sp.GetRequiredService<Saml2ProviderAdapter>());
        services.AddScoped<IOAuth2ProviderFactory, OAuth2ProviderFactory>();

        // 7.2 Task A2 补齐：身份管理 / OAuth 客户端管理 / 内部查询应用服务
        //     a) AES-256 加密服务（OAuth ClientSecret 加密存储）：fail-fast，密钥缺失时拒绝启动
        var aesKey = configuration["OAuth2:AesKey"];
        if (string.IsNullOrWhiteSpace(aesKey))
        {
            throw new InvalidOperationException(
                "OAuth2:AesKey 配置缺失，无法启动 Identity 服务。" +
                "请在配置中提供 256 位 AES 密钥（Base64 编码 32 字节）。");
        }
        services.AddSingleton<IClientSecretEncryptionService>(new AesEncryptionService(aesKey));

        //     b) UserAdminAppService（含 AccessControl BC HTTP 跨域调用）：通过 HttpClientFactory 注册 typed client，
        //        配置 BaseAddress 与 X-Internal-Key 头，调用 AccessControl api/admin/users/{id}/roles 端点
        var accessControlHttpUrl = configuration["ServiceUrls:AccessControlApi"]
            ?? configuration["AntiCorruption:GrpcEndpoints:AccessControl"]
            ?? "http://localhost:8082";
        var internalApiKey = configuration["InternalAuth:ApiKey"]
            ?? configuration["Security:InternalApiKey:Shared"];

        services.AddHttpClient<UserAdminAppService>(client =>
        {
            client.BaseAddress = new Uri(accessControlHttpUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
            if (!string.IsNullOrWhiteSpace(internalApiKey))
            {
                client.DefaultRequestHeaders.Add("X-Internal-Key", internalApiKey);
            }
        });
        services.AddScoped<IUserAdminAppService>(sp => sp.GetRequiredService<UserAdminAppService>());

        //     c) OAuthClientAppService / UserInternalAppService（无 HttpClient 依赖，直接注册）
        services.AddScoped<IOAuthClientAppService, OAuthClientAppService>();
        services.AddScoped<IUserInternalAppService, UserInternalAppService>();

        // 8. FluentValidation 校验器自动扫描
        services.AddValidatorsFromAssembly(typeof(IAuthenticationAppService).Assembly);

        return services;
    }
}
