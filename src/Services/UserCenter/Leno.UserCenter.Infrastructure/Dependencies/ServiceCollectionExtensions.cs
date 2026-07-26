using FluentValidation;
using Leno.Identity.Infrastructure;
using Leno.Infrastructure.Persistence;
using Leno.SharedKernel.Abstractions;
using Leno.UserCenter.Application;
using Leno.UserCenter.Application.Services;
using Leno.UserCenter.Domain.Repositories;
using Leno.UserCenter.Infrastructure;
using Leno.UserCenter.Infrastructure.Repositories;
using Leno.UserCenter.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Leno.UserCenter.Infrastructure.Dependencies;

/// <summary>
/// 用户中心域基础设施层 DI 注册入口。
/// 注册 DbContext、工作单元、仓储、应用服务、防腐层实现与 FluentValidation 校验器。
/// Task A6：补齐 Repository 与 AppService 注册，并注册跨 BC 防腐层 UserDefaultAddressStore。
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

        // 1. UserCenter BC DbContext（承载 Address/Favorite/BrowseHistory/NotificationPreferences 聚合）
        services.AddDbContext<UserCenterDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString(connectionStringName);
            options.UseSqlServer(connectionString);
        });

        // 2. 工作单元（UserCenter BC 事务边界）
        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork<UserCenterDbContext>>();

        // 3. Identity BC DbContext（跨 BC 防腐层访问 User 聚合，更新 DefaultAddressId 字段）
        //    仅注册 DbContext，不调用 AddIdentityInfrastructure 以避免引入 OAuth/JWT 等无关服务。
        services.AddDbContext<IdentityDbContext>(options =>
        {
            var identityConnectionString = configuration.GetConnectionString("IdentityDb");
            options.UseSqlServer(identityConnectionString);
        });

        // 4. UserCenter BC 仓储注册
        services.AddScoped<IAddressRepository, EfCoreAddressRepository>();
        services.AddScoped<IBrowseHistoryRepository, EfCoreBrowseHistoryRepository>();
        services.AddScoped<IFavoriteRepository, EfCoreFavoriteRepository>();
        services.AddScoped<INotificationPreferencesRepository, EfCoreNotificationPreferencesRepository>();

        // 5. 跨 BC 防腐层：用户默认地址存储（依赖 IdentityDbContext）
        services.AddScoped<IUserDefaultAddressStore, UserDefaultAddressStore>();

        // 6. UserCenter BC 应用服务注册
        services.AddScoped<IAddressAppService, AddressAppService>();
        services.AddScoped<IBrowseHistoryAppService, BrowseHistoryAppService>();
        services.AddScoped<IFavoritesAppService, FavoritesAppService>();
        services.AddScoped<INotificationPreferencesAppService, NotificationPreferencesAppService>();

        // 7. FluentValidation 校验器自动扫描（SaveAddressDtoValidator 等）
        services.AddValidatorsFromAssembly(typeof(IAddressAppService).Assembly);

        return services;
    }
}
