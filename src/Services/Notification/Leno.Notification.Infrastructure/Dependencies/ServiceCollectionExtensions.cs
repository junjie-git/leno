using AppServices = Leno.Notification.Application.Services;
using Leno.Infrastructure.AntiCorruption;
using Leno.Infrastructure.Configuration;
using Leno.Infrastructure.Persistence;
using Leno.Notification.Application;
using Leno.Notification.Domain.Channels;
using Leno.Notification.Domain.Repositories;
using Leno.Notification.Domain.Services;
using Leno.Notification.Infrastructure.Channels;
using Leno.Notification.Infrastructure.Consumers;
using Leno.Notification.Infrastructure.Jobs;
using Leno.Notification.Infrastructure.Options;
using Leno.Notification.Infrastructure.Repositories;
using Leno.Notification.Infrastructure.Services;
using Leno.Notification.Infrastructure.Services.Grpc;
using Leno.SharedContracts.Grpc.User.V1;
using Leno.SharedKernel.Abstractions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leno.Notification.Infrastructure.Dependencies;

/// <summary>
/// 通知域基础设施层 DI 注册入口。
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddNotificationInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        string connectionStringName = "NotificationDb")
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddDbContext<NotificationDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString(connectionStringName);
            options.UseSqlServer(connectionString);
        });

        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork<NotificationDbContext>>();

        services.AddScoped<INotificationRecordRepository, EfCoreNotificationRecordRepository>();
        services.AddScoped<INotificationTemplateRepository, EfCoreNotificationTemplateRepository>();
        services.AddScoped<INotificationPreferenceRepository, EfCoreNotificationPreferenceRepository>();
        services.AddScoped<INotificationRateLimitConfigRepository, EfCoreNotificationRateLimitConfigRepository>();
        services.AddScoped<INotificationConfigRepository, EfCoreNotificationConfigRepository>();

        // 模板渲染器
        services.AddScoped<ITemplateRenderer, TemplateRenderer>();
        services.AddScoped<ITemplateRenderService, TemplateRenderer>();

        // 用户联系方式防腐层（通过 HTTP 调用用户域内部端点获取手机号/邮箱）
        // 双轨下线 A6（2026-09-23）：UserAuth BC 退役，联系方式端点由 Identity BC 承接
        // （internal/v1/users/{userId}/contacts 与 /contacts/full 路由一致）；
        // gRPC 双轨一并删除（低频内部查询，保留单一 HTTP 机制）。
        var identityApiUrl = configuration["ServiceUrls:IdentityApi"] ?? "http://localhost:5162";
        // HttpClient 防腐层实现（保留作为降级备份，不绑定接口）
        services.AddHttpClient<UserContactAntiCorruptionService>(c => c.BaseAddress = new Uri(identityApiUrl))
            .AddAntiCorruptionPolicies();

        // 通知渠道配置
        services.Configure<EmailChannelOptions>(configuration.GetSection("Notification:Email"));
        services.Configure<SmsChannelOptions>(configuration.GetSection("Notification:Sms"));

        // P1-7：重试策略与频率限制配置（IOptionsMonitor 支持热更新）
        services.Configure<RetryPolicyOptions>(configuration.GetSection(RetryPolicyOptions.SectionName));
        services.Configure<RateLimitOptions>(configuration.GetSection(RateLimitOptions.SectionName));

        // 通知渠道实现：ISmsProvider 由 AliyunSmsProvider/TencentSmsProvider 实现，
        // SmsChannel 外壳类作为唯一的 INotificationChannel(NotificationChannel.Sms) 注册到 DI，
        // 避免两个 SMS 实现注册为 INotificationChannel 时 ToDictionary 抛重复键异常。
        services.AddHttpClient<AliyunSmsProvider>()
            .AddAntiCorruptionPolicies();
        services.AddHttpClient<TencentSmsProvider>()
            .AddAntiCorruptionPolicies();
        services.AddScoped<ISmsProvider, AliyunSmsProvider>();
        services.AddScoped<ISmsProvider, TencentSmsProvider>();
        services.AddScoped<INotificationChannel, SmsChannel>();
        services.AddScoped<INotificationChannel, SmtpEmailChannel>();
        services.AddScoped<INotificationChannel, InAppChannel>();
        // 3.9：新增 PushChannel mock 验证"实现 IChannel + DI 注册即可被注册表自动发现，零侵入核心调度"。
        services.AddScoped<INotificationChannel, PushChannel>();

        // 3.9：通知渠道注册表，从 IEnumerable<INotificationChannel> 构建。
        // 渠道实现自带 Metadata，注册表汇总后供调度器 / 限流器 / 偏好查询使用。
        // Scoped 生命周期与 INotificationChannel 对齐（依赖 IEnumerable<INotificationChannel>）。
        services.AddScoped<INotificationChannelRegistry, NotificationChannelRegistry>();

        // 重试策略
        services.AddSingleton<Domain.Services.IRetryPolicy, Infrastructure.Services.RetryPolicy>();

        // 频率限制器
        services.AddSingleton<Domain.Services.IRateLimiter, Infrastructure.Services.RedisRateLimiter>();

        // 分布式锁提供者（Job 多实例并发防重复拾取）
        services.AddSingleton<Domain.Services.IDistributedLockProvider, Infrastructure.Services.RedisDistributedLockProvider>();

        // 通知调度器
        services.AddScoped<INotificationDispatcher, NotificationDispatcher>();

        // 通知统一发送服务
        services.AddScoped<INotificationService, AppServices.NotificationService>();

        // 调度与重试任务
        services.AddScoped<NotificationDispatchJob>();
        services.AddScoped<NotificationRetryJob>();

        // 应用服务
        services.AddScoped<INotificationAppService, AppServices.NotificationAppService>();
        services.AddScoped<INotificationTemplateAppService, AppServices.NotificationTemplateAppService>();
        services.AddScoped<INotificationPreferenceAppService, AppServices.NotificationPreferenceAppService>();
        services.AddScoped<IDeadLetterAppService, AppServices.DeadLetterAppService>();
		services.AddScoped<INotificationConfigAppService, Infrastructure.Services.NotificationConfigAppService>();
		services.AddScoped<IRateLimitAppService, AppServices.RateLimitAppService>();
		services.AddScoped<INotificationRecordAppService, AppServices.NotificationRecordAppService>();

		// 渠道选择器（3.9：注入 INotificationChannelRegistry，改为 Scoped 避免 captive dependency；
		//   旧 Singleton 生命周期无法注入 Scoped 注册表。）
		services.AddScoped<Domain.Services.IChannelSelector>(sp =>
		{
			var smsProvider = configuration["Notification:Sms:Provider"] ?? "Aliyun";
			var registry = sp.GetRequiredService<INotificationChannelRegistry>();
			return new Domain.Services.ChannelSelector(smsProvider, registry);
		});

		return services;
    }

    public static IBusRegistrationConfigurator AddNotificationConsumers(
        this IBusRegistrationConfigurator configurator)
    {
        ArgumentNullException.ThrowIfNull(configurator);

        // 仅注册按 BC 拆分的专用 Consumer，避免重复订阅（P0 问题已修复）。
        configurator.AddConsumer<UserEventConsumer>();
        configurator.AddConsumer<OrderEventConsumer>();
        configurator.AddConsumer<PaymentEventConsumer>();
        configurator.AddConsumer<PromotionEventConsumer>();
        configurator.AddConsumer<PointsEventConsumer>();
        configurator.AddConsumer<AfterSalesEventConsumer>();

        return configurator;
    }
}