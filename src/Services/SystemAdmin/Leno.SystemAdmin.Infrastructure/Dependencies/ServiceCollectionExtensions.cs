using Leno.SharedKernel.Abstractions;
using Leno.SystemAdmin.Application;
using Leno.SystemAdmin.Application.Abstractions;
using Leno.SystemAdmin.Application.Services;
using Leno.SystemAdmin.Domain.Repositories;
using Leno.SystemAdmin.Domain.Services;
using Leno.SystemAdmin.Infrastructure.Cache;
using Leno.SystemAdmin.Infrastructure.Consumers;
using Leno.SystemAdmin.Infrastructure.Jobs;
using Leno.SystemAdmin.Infrastructure.Repositories;
using Leno.SystemAdmin.Infrastructure.Services;
using ReconciliationServiceImpl = Leno.SystemAdmin.Infrastructure.Services.StatisticsReconciliationService;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using FeatureFlagEvaluatorImpl = Leno.SystemAdmin.Infrastructure.Services.FeatureFlagEvaluator;
using ScheduledTaskExecutorImpl = Leno.SystemAdmin.Infrastructure.Services.ScheduledTaskExecutor;

namespace Leno.SystemAdmin.Infrastructure.Dependencies;

/// <summary>
/// 系统管理域基础设施层 DI 注册入口。
/// 注册 DbContext、工作单元、仓储与 Redis 缓存。
/// 调用方在表现层 Program.cs 调用 <c>services.AddSystemAdminInfrastructure(configuration)</c>。
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <param name="connectionStringName">连接字符串名称，默认 <c>SystemAdminDb</c>。</param>
    public static IServiceCollection AddSystemAdminInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        string connectionStringName = "SystemAdminDb")
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddDbContext<SystemAdminDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString(connectionStringName);
            options.UseSqlServer(connectionString);
        });

        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddScoped<IOperatorRepository, EfCoreOperatorRepository>();
        services.AddScoped<ISystemConfigRepository, EfCoreSystemConfigRepository>();
        services.AddScoped<IDataDictionaryRepository, EfCoreDataDictionaryRepository>();
        services.AddScoped<IAuditLogRepository, EfCoreAuditLogRepository>();
        services.AddScoped<IOperationLogRepository, EfCoreOperationLogRepository>();
        services.AddScoped<ISystemAnnouncementRepository, EfCoreSystemAnnouncementRepository>();
        services.AddScoped<IFeatureFlagRepository, EfCoreFeatureFlagRepository>();
        services.AddScoped<IScheduledTaskRepository, EfCoreScheduledTaskRepository>();
        services.AddScoped<IIndexRebuildTaskRepository, EfCoreIndexRebuildTaskRepository>();
        services.AddScoped<IDeadLetterMessageRepository, EfCoreDeadLetterMessageRepository>();
        services.AddScoped<IDashboardReportRepository, EfCoreDashboardReportRepository>();
        services.AddScoped<IReconciliationRecordRepository, EfCoreReconciliationRecordRepository>();
        services.AddScoped<IAuditLogEntryRepository, EfCoreAuditLogEntryRepository>();
        services.AddScoped<IRateLimitRuleRepository, EfCoreRateLimitRuleRepository>();

        services.AddScoped<IDeadLetterQueueManager, DeadLetterQueueManager>();

        services.AddScoped<IIndexRebuildOrchestrator, IndexRebuildOrchestrator>();

        // 基础设施抽象：通过 HttpClientFactory 注册需要 HTTP 调用的服务
        services.AddHttpClient<IIndexRebuildTrigger, ElasticsearchRebuildTrigger>();
        services.AddHttpClient<IModuleHealthProbe, HttpModuleHealthProbe>();

        services.AddScoped<IHealthAggregator, HealthAggregator>();
        services.AddSingleton<IRateLimitCounter, RedisRateLimitCounter>();

        services.AddSingleton<SystemConfigCache>();
        services.AddSingleton<FeatureFlagCache>();

        services.AddQuartz(q =>
        {
            q.UseSimpleTypeLoader();
            q.UseDefaultThreadPool(tp => tp.MaxConcurrency = 10);
        });
        services.AddQuartzHostedService(options => options.WaitForJobsToComplete = true);

        services.AddScoped<IFeatureFlagEvaluator, FeatureFlagEvaluatorImpl>();
        services.AddScoped<IStatisticsAggregationService, StatisticsAggregationService>();
        services.AddScoped<IStatisticsReconciliationService, ReconciliationServiceImpl>();
        services.AddSingleton<QuartzJobScheduler>();
        services.AddScoped<ScheduledTaskDispatcher>();
        services.AddScoped<IScheduledTaskExecutor, ScheduledTaskExecutorImpl>();

        // 限流策略解析器
        services.AddScoped<IRateLimitPolicyResolver, RateLimitPolicyResolver>();

        // 审计日志保留后台服务
        services.AddHostedService<AuditLogRetentionService>();

        // Application Services
        services.AddScoped<IDeadLetterAppService, DeadLetterAppService>();
        services.AddScoped<IHealthAppService, HealthAppService>();

        // 对账后台作业
        services.AddHostedService<StatisticsReconciliationJob>();

        // T20: 死信积压告警后台服务，定期扫描死信数量，超阈值告警
        services.Configure<DeadLetterMonitorOptions>(
            configuration.GetSection("DeadLetterMonitor"));
        services.AddHostedService<DeadLetterMonitorBackgroundService>();

        services.AddScoped<IAuditLogEntryAppService, AuditLogEntryAppService>();
        services.AddScoped<IRateLimitRuleAppService, RateLimitRuleAppService>();

        return services;
    }

    /// <summary>
    /// 注册系统管理域的 MassTransit 集成事件消费者。
    /// 在表现层调用 <c>AddLenoInfrastructure(configuration, cfg => cfg.AddSystemAdminConsumers())</c>。
    /// 已注册售后审核通过事件的审计日志与操作日志消费者。
    /// </summary>
    public static IBusRegistrationConfigurator AddSystemAdminConsumers(this IBusRegistrationConfigurator configurator)
    {
        ArgumentNullException.ThrowIfNull(configurator);

        configurator.AddConsumer<AuditLogConsumer>();
        configurator.AddConsumer<AfterSalesEventConsumer>();

        return configurator;
    }
}
