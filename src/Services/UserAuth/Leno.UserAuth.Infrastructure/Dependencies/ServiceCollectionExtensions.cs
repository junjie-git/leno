using FluentValidation;
using Leno.SharedKernel.Abstractions;
using Leno.UserAuth.Application;
using Leno.UserAuth.Application.Abstractions;
using Leno.UserAuth.Domain.Repositories;
using Leno.UserAuth.Domain.Services;
using Leno.UserAuth.Infrastructure.Audit;
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

        services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
        services.AddScoped<IUserUniquenessChecker, UserUniquenessChecker>();

        services.AddScoped<ITokenService, TokenService>();
        services.AddSingleton<IRefreshTokenStore, InMemoryRefreshTokenStore>();

        services.AddScoped<AuditLogInterceptor>();

        services.AddValidatorsFromAssembly(typeof(IUserAppService).Assembly);

        return services;
    }
}
