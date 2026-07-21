using FluentValidation;
using Leno.Infrastructure.EventBus;
using Leno.Infrastructure.Persistence;
using Leno.PointsMembership.Application;
using Leno.PointsMembership.Application.Services;
using Leno.PointsMembership.Domain.Repositories;
using Leno.PointsMembership.Domain.Services;
using Leno.PointsMembership.Infrastructure.EventBus;
using Leno.PointsMembership.Infrastructure.ReadModels;
using Leno.PointsMembership.Infrastructure.Repositories;
using Leno.SharedKernel.Abstractions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Leno.PointsMembership.Infrastructure.Dependencies;

/// <summary>
/// 积分会员域基础设施层 DI 注册入口。
/// 注册 DbContext、工作单元、仓储、积分抵扣防腐层、应用服务与 FluentValidation 校验器。
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

        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork<PointsMembershipDbContext>>();

        // PM-L02 修复：集中管理硬编码业务阈值与时区设置，从 appsettings.json PointsMembership 节绑定
        services.Configure<PointsMembershipOptions>(configuration.GetSection("PointsMembership"));

        // 注册 PointsMembership BC 领域事件到集成事件翻译器
        services.AddSingleton<IIntegrationEventMapper, PointsMembershipIntegrationEventMapper>();

        services.AddScoped<IPointsAccountRepository, EfCorePointsAccountRepository>();
        services.AddScoped<ICheckInRecordRepository, EfCoreCheckInRecordRepository>();
        services.AddScoped<IMemberRepository, EfCoreMemberRepository>();
        services.AddScoped<IMembershipLevelRepository, EfCoreMembershipLevelRepository>();
        services.AddScoped<IMembershipPackageRepository, EfCoreMembershipPackageRepository>();
        services.AddScoped<IUserMembershipRepository, EfCoreUserMembershipRepository>();
        services.AddScoped<IMemberLevelRepository, EfCoreMemberLevelRepository>();
        services.AddScoped<ITaskRepository, EfCoreTaskRepository>();
        services.AddScoped<IUserTaskRepository, EfCoreUserTaskRepository>();

        // 积分抵扣防腐层实现位于应用层
        services.AddScoped<IPointsOffsetAppService, PointsOffsetAppService>();

        // 应用服务
        services.AddScoped<IPointsAppService, PointsAppService>();
        services.AddScoped<IPointsInternalAppService, PointsInternalAppService>();
        services.AddScoped<IMemberAppService, MemberAppService>();
        services.AddScoped<IMembershipPackageAppService, MembershipPackageAppService>();
        services.AddScoped<IExchangeCouponAppService, ExchangeCouponAppService>();
        services.AddScoped<ITaskAppService, TaskAppService>();

        // FluentValidation 校验器
        services.AddValidatorsFromAssembly(typeof(IPointsAppService).Assembly);

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
        configurator.AddConsumer<Consumers.OrderAfterSalesWindowClosedEventConsumer>();
        configurator.AddConsumer<Consumers.UserRegisteredEventConsumer>();
        configurator.AddConsumer<Consumers.ReviewApprovedEventConsumer>();
        configurator.AddConsumer<Consumers.RefundCompletedEventConsumer>();
        configurator.AddConsumer<Consumers.CouponExchangeSucceededEventConsumer>();
        configurator.AddConsumer<Consumers.CouponExchangeFailedEventConsumer>();

        // ES 读模型同步：积分账户创建索引/余额变更重建，会员档案创建索引/等级升级重建
        configurator.AddConsumer<PointsAccountCreatedReadModelSyncConsumer>();
        configurator.AddConsumer<PointsAdjustedReadModelSyncConsumer>();
        configurator.AddConsumer<MemberRegisteredReadModelSyncConsumer>();
        configurator.AddConsumer<MemberLevelUpgradedReadModelSyncConsumer>();

        return configurator;
    }
}
