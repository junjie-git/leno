using Leno.Infrastructure.Persistence;
using Leno.SharedKernel.Abstractions;
using Leno.UserCenter.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Leno.UserCenter.Infrastructure.Dependencies;

/// <summary>
/// 用户中心域基础设施层 DI 注册入口（Task A5 骨架）。
/// Task A6 将补齐 Repository 与 AppService 注册。
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddUserCenterInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        string connectionStringName = "UserCenterDb")
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddDbContext<UserCenterDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString(connectionStringName);
            options.UseSqlServer(connectionString);
        });

        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork<UserCenterDbContext>>();

        // Task A6 将在此处注册 Repository 与 AppService

        return services;
    }
}
