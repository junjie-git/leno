using Leno.PointsMembership.Domain.Repositories;
using Leno.PointsMembership.Domain.Services;
using Leno.PointsMembership.Infrastructure.Repositories;
using Leno.PointsMembership.Infrastructure.Services;
using Leno.SharedKernel.Abstractions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Leno.PointsMembership.Infrastructure.Dependencies;

/// <summary>
/// 积分会员域基础设施层 DI 注册入口。
/// 注册 DbContext、工作单元、仓储与积分抵扣防腐层实现。
/// 应用层尚未实现，故暂不注册应用服务与校验器。
/// 调用方在表现层 Program.cs 调用 <c>services.AddPointsMembershipInfrastructure(configuration)</c>。
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <param name="connectionStringName">连接字符串名称，默认 <c>PointsMembershipDb</c>。</param>
    public static IServiceCollection AddPointsMembershipInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        string connectionStringName = "PointsMembershipDb")
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddDbContext<PointsMembershipDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString(connectionStringName);
            options.UseSqlServer(connectionString);
        });

        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddScoped<IPointsAccountRepository, EfCorePointsAccountRepository>();
        services.AddScoped<ICheckInRecordRepository, EfCoreCheckInRecordRepository>();
        services.AddScoped<IMemberRepository, EfCoreMemberRepository>();
        services.AddScoped<IMembershipLevelRepository, EfCoreMembershipLevelRepository>();
        services.AddScoped<IMembershipPackageRepository, EfCoreMembershipPackageRepository>();
        services.AddScoped<IUserMembershipRepository, EfCoreUserMembershipRepository>();

        services.AddScoped<IPointsOffsetService, EfCorePointsOffsetService>();

        return services;
    }

    /// <summary>
    /// 注册积分会员域的 MassTransit 集成事件消费者。
    /// 在表现层调用 <c>AddLenoInfrastructure(configuration, cfg => cfg.AddPointsMembershipConsumers())</c>。
    /// </summary>
    public static IBusRegistrationConfigurator AddPointsMembershipConsumers(
        this IBusRegistrationConfigurator configurator)
    {
        ArgumentNullException.ThrowIfNull(configurator);

        configurator.AddConsumer<Consumers.OrderCompletedEventConsumer>();
        configurator.AddConsumer<Consumers.OrderCancelledEventConsumer>();
        configurator.AddConsumer<Consumers.OrderPaidEventConsumer>();
        configurator.AddConsumer<Consumers.UserRegisteredEventConsumer>();

        return configurator;
    }
}
