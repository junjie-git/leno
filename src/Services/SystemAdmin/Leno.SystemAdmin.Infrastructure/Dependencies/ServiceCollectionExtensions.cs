using Leno.SharedKernel.Abstractions;
using Leno.Infrastructure.Abstractions.Geo;
using Leno.Infrastructure.Abstractions.Sessions;
using Leno.Infrastructure.Abstractions.UserAgent;
using Leno.Infrastructure.EventBus;
using Leno.Infrastructure.Persistence;
using Leno.SystemAdmin.Application;
using Leno.SystemAdmin.Application.Abstractions;
using Leno.SystemAdmin.Application.Services;
using Leno.SystemAdmin.Domain.Repositories;
using Leno.SystemAdmin.Domain.Services;
using Leno.SystemAdmin.Infrastructure.BackgroundServices;
using Leno.SystemAdmin.Infrastructure.Cache;
using Leno.SystemAdmin.Infrastructure.Consumers;
using Leno.SystemAdmin.Infrastructure.EventBus;
using Leno.SystemAdmin.Infrastructure.Jobs;
using Leno.SystemAdmin.Infrastructure.Options;
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

        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork<SystemAdminDbContext>>();

        // 注册 SystemAdmin BC 领域事件到集成事件翻译器
        services.AddSingleton<IIntegrationEventMapper, SystemAdminIntegrationEventMapper>();

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
        services.AddSingleton<ISystemConfigCache>(sp => sp.GetRequiredService<SystemConfigCache>());
        services.AddSingleton<FeatureFlagCache>();
        services.AddSingleton<IFeatureFlagCache>(sp => sp.GetRequiredService<FeatureFlagCache>());

        services.AddQuartz(q =>
        {
            q.UseSimpleTypeLoader();
            q.UseDefaultThreadPool(tp => tp.MaxConcurrency = 10);

            // L-04: DLQ 清理作业，默认每小时执行一次，可通过 DlqCleanup:CronExpression 配置
            var dlqCleanupCron = configuration["DlqCleanup:CronExpression"] ?? "0 0 * * * ?";
            var dlqCleanupJobKey = new JobKey("dlq-cleanup", "systemadmin");
            q.AddJob<DlqCleanupJob>(opts => opts.WithIdentity(dlqCleanupJobKey));
            q.AddTrigger(opts => opts
                .ForJob(dlqCleanupJobKey)
                .WithIdentity("dlq-cleanup-trigger", "systemadmin")
                .WithCronSchedule(dlqCleanupCron));
        });
        services.AddQuartzHostedService(options => options.WaitForJobsToComplete = true);

        services.AddScoped<IFeatureFlagEvaluator, FeatureFlagEvaluatorImpl>();
        services.AddHttpClient<StatisticsMetricsQueryClient>();
        services.AddScoped<IStatisticsDataSource, StatisticsMetricsSource>();
        services.AddScoped<IStatisticsAggregationService, StatisticsAggregationService>();
        services.AddScoped<IStatisticsReconciliationService, ReconciliationServiceImpl>();
        services.AddSingleton<QuartzJobScheduler>();
        services.AddScoped<ScheduledTaskDispatcher>();
        services.AddScoped<IScheduledTaskExecutor, ScheduledTaskExecutorImpl>();

        // 限流策略解析器
        services.AddScoped<IRateLimitPolicyResolver, RateLimitPolicyResolver>();

        // 审计日志保留后台服务
        services.AddHostedService<AuditLogRetentionService>();

        // 2.4.7: Outbox 表 7 天归档后台服务，每天 02:00 UTC 清理已处理记录至 outbox_messages_archive
        services.Configure<OutboxArchivalOptions>(
            configuration.GetSection(OutboxArchivalOptions.SectionName));
        services.AddHostedService<OutboxArchivalBackgroundService>();

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

        // 告警管理与 Outbox 监控（BC11 P1）
        services.Configure<AlertmanagerOptions>(
            configuration.GetSection(AlertmanagerOptions.SectionName));
        services.AddHttpClient<IAlertmanagerClient, HttpAlertmanagerClient>();

        services.Configure<OutboxMonitorOptions>(
            configuration.GetSection(OutboxMonitorOptions.SectionName));
        services.AddScoped<IOutboxQueryService, OutboxQueryService>();
        services.AddScoped<IOutboxArchiveRecordRepository, EfCoreOutboxArchiveRecordRepository>();

        services.AddScoped<IAlertAppService, AlertAppService>();
        services.AddScoped<IAlertSilenceAppService, AlertSilenceAppService>();
        services.AddScoped<IOutboxMonitorAppService, OutboxMonitorAppService>();

        // P0 功能：菜单、登录日志仓储
        services.AddScoped<IMenuRepository, EfCoreMenuRepository>();
        services.AddScoped<ILoginLogRepository, EfCoreLoginLogRepository>();

        // Redis 抽象实现：复用主 Redis 连接
        services.AddSingleton<IUserSessionStore, RedisUserSessionStore>();
        services.AddSingleton<IRedisCacheMonitor, RedisCacheMonitorService>();

        // 进程监控
        services.AddSingleton<IDotNetProcessMonitor, DotNetProcessMonitorService>();
        services.AddSingleton<IMetricHistoryStore, MemoryMetricHistoryStore>();
        services.AddHostedService<ServerMetricSamplerBackgroundService>();

        // UA 解析与地理定位
        services.AddSingleton<IUserAgentParser, UAParserUserAgentParser>();
        services.AddSingleton<IGeoLocationResolver>(sp =>
        {
            var configuration = sp.GetRequiredService<IConfiguration>();
            var mmdbPath = configuration["P0Features:GeoLocation:MaxMindDbPath"] ?? "/var/lib/leno/GeoLite2-City.mmdb";
            return new MaxMindGeoLocationResolver(mmdbPath);
        });

        // P0 配置选项
        services.Configure<P0FeaturesOptions>(configuration.GetSection(P0FeaturesOptions.SectionName));

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
        configurator.AddConsumer<LoginLogConsumer>();

        return configurator;
    }
}
