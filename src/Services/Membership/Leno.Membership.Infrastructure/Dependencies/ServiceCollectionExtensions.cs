using Leno.Infrastructure.EventBus;
using Leno.Infrastructure.Persistence;
using Leno.Membership.Domain.Repositories;
using Leno.Membership.Infrastructure.EventBus;
using Leno.Membership.Infrastructure.Repositories;
using Leno.SharedKernel.Abstractions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Leno.Membership.Infrastructure.Dependencies;

/// <summary>
/// Membership BC 基础设施层 DI 注册入口（会员 BC 独立维护）。
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <param name="connectionStringName">连接字符串名称，默认 <c>MembershipDb</c>。</param>
    public static IServiceCollection AddMembershipInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        string connectionStringName = "MembershipDb")
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddDbContext<MembershipDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString(connectionStringName);
            options.UseSqlServer(connectionString);
        });

        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork<MembershipDbContext>>();

        // 注册 Membership BC 领域事件到集成事件翻译器
        services.AddSingleton<IIntegrationEventMapper, MembershipIntegrationEventMapper>();

        services.AddScoped<IMemberRepository, EfCoreMemberRepository>();
        services.AddScoped<IMemberLevelDefinitionRepository, EfCoreMemberLevelDefinitionRepository>();
        services.AddScoped<IMembershipPackageRepository, EfCoreMembershipPackageRepository>();

        return services;
    }

    /// <summary>
    /// 注册 Membership BC 的 MassTransit 集成事件消费者。
    /// 在表现层调用 <c>AddLenoInfrastructure(configuration, cfg => cfg.AddMembershipConsumers())</c>。
    /// </summary>
    public static IBusRegistrationConfigurator AddMembershipConsumers(
        this IBusRegistrationConfigurator configurator)
    {
        ArgumentNullException.ThrowIfNull(configurator);

        // 跨 BC 协作消费者：Points BC 发布 PointsEarnedIntegrationEvent → Membership BC 消费累加成长值
        configurator.AddConsumer<Consumers.PointsEarnedEventConsumer>();

        return configurator;
    }
}
