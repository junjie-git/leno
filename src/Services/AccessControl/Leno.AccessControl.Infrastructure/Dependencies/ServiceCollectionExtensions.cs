using FluentValidation;
using Leno.AccessControl.Application;
using Leno.AccessControl.Application.Services;
using Leno.AccessControl.Domain.Repositories;
using Leno.AccessControl.Domain.Services;
using Leno.AccessControl.Infrastructure.Repositories;
using Leno.AccessControl.Infrastructure.Services;
using Leno.Infrastructure.Persistence;
using Leno.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Leno.AccessControl.Infrastructure.Dependencies;

/// <summary>
/// AccessControl BC 基础设施层 DI 注册入口。
/// 注册 DbContext、工作单元、仓储、权限校验服务与应用服务。
/// 调用方在 Presentation 层 Program.cs 调用 <c>services.AddAccessControlInfrastructure(configuration)</c>。
/// 从 UserAuth BC 拆分而来（3.6 AuthN/AuthZ 拆分）。
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 注册 AccessControl BC 的全部基础设施服务。
    /// </summary>
    /// <param name="connectionStringName">连接字符串名称，默认 <c>AccessControlDb</c>。</param>
    public static IServiceCollection AddAccessControlInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        string connectionStringName = "AccessControlDb")
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddDbContext<AccessControlDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString(connectionStringName);
            options.UseSqlServer(connectionString);
        });

        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork<AccessControlDbContext>>();

        services.AddScoped<IPermissionRepository, EfCorePermissionRepository>();
        services.AddScoped<IUserRoleAssignmentRepository, EfCoreUserRoleAssignmentRepository>();

        // IMemoryCache 用于 PermissionChecker 与 JwtTokenService 的角色缓存（5 分钟）
        services.AddMemoryCache();

        services.AddScoped<IPermissionChecker, PermissionChecker>();

        // 应用服务
        services.AddScoped<IPermissionAppService, PermissionAppService>();
        services.AddScoped<IUserRoleAppService, UserRoleAppService>();
        // 角色 CRUD + 角色权限管理（AdminRolesController 7 端点使用）
        services.AddScoped<IRoleAppService, RoleAppService>();
        services.AddScoped<IRolePermissionAppService, RolePermissionAppService>();

        services.AddValidatorsFromAssembly(typeof(IPermissionAppService).Assembly);

        return services;
    }
}
