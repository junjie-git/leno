using Leno.Infrastructure.EventBus;
using Leno.Infrastructure.Persistence;
using Leno.Points.Domain.Repositories;
using Leno.Points.Infrastructure.Consumers;
using Leno.Points.Infrastructure.EventBus;
using Leno.Points.Infrastructure.Repositories;
using Leno.SharedKernel.Abstractions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Leno.Points.Infrastructure.Dependencies;

/// <summary>
/// Points BC 基础设施层 DI 注册入口（积分 BC 独立维护）。
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <param name="connectionStringName">连接字符串名称，默认 <c>PointsDb</c>。</param>
    public static IServiceCollection AddPointsInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        string connectionStringName = "PointsDb")
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddDbContext<PointsDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString(connectionStringName);
            options.UseSqlServer(connectionString);
        });

        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork<PointsDbContext>>();

        // 注册 Points BC 领域事件到集成事件翻译器
        services.AddSingleton<IIntegrationEventMapper, PointsIntegrationEventMapper>();

        // 配置选项
        services.Configure<PointsBonusOptions>(configuration.GetSection("Points:Bonus"));

        services.AddScoped<IPointsAccountRepository, EfCorePointsAccountRepository>();
        services.AddScoped<IPointsFlowRepository, EfCorePointsFlowRepository>();
        services.AddScoped<IPointsExchangeRepository, EfCorePointsExchangeRepository>();

        return services;
    }

    /// <summary>
    /// 注册 Points BC 的 MassTransit 集成事件消费者。
    /// 在表现层调用 <c>AddLenoInfrastructure(configuration, cfg => cfg.AddPointsConsumers())</c>。
    /// </summary>
    public static IBusRegistrationConfigurator AddPointsConsumers(
        this IBusRegistrationConfigurator configurator)
    {
        ArgumentNullException.ThrowIfNull(configurator);

        // 跨 BC 协作消费者：Membership BC 发布 MemberLevelChangedIntegrationEvent → Points BC 消费发放奖励积分
        configurator.AddConsumer<Consumers.MemberLevelChangedEventConsumer>();

        return configurator;
    }
}
