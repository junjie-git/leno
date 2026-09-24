using FluentValidation;
using Leno.Infrastructure.AntiCorruption;
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

        // 3. 跨 BC 防腐层：用户默认地址经 Identity BC 内部 API 更新（P0 架构修复）。
        //    不再直连 IdentityDbContext 写他域聚合，改走 PUT internal/v1/users/{id}/default-address
        //    （X-Internal-Key 鉴权），事务与领域事件由 Identity BC 自行提交
        var identityApiUrl = configuration["ServiceUrls:IdentityApi"] ?? "http://localhost:5162";
        services.AddHttpClient<UserDefaultAddressStore>(c => c.BaseAddress = new Uri(identityApiUrl))
            .AddAntiCorruptionPolicies();

        // 4. UserCenter BC 仓储注册
        services.AddScoped<IAddressRepository, EfCoreAddressRepository>();
        services.AddScoped<IBrowseHistoryRepository, EfCoreBrowseHistoryRepository>();
        services.AddScoped<IFavoriteRepository, EfCoreFavoriteRepository>();
        services.AddScoped<INotificationPreferencesRepository, EfCoreNotificationPreferencesRepository>();

        // 5. 跨 BC 防腐层接口注册（实现为上方 AddHttpClient 的 typed client）
        services.AddScoped<IUserDefaultAddressStore>(sp => sp.GetRequiredService<UserDefaultAddressStore>());

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
