using AppServices = Leno.Notification.Application.Services;
using Leno.Notification.Application;
using Leno.Notification.Domain.Repositories;
using Leno.Notification.Domain.Services;
using Leno.Notification.Infrastructure.Channels;
using Leno.Notification.Infrastructure.Consumers;
using Leno.Notification.Infrastructure.Jobs;
using Leno.Notification.Infrastructure.Repositories;
using Leno.Notification.Infrastructure.Services;
using Leno.SharedKernel.Abstractions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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

        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddScoped<INotificationRecordRepository, EfCoreNotificationRecordRepository>();
        services.AddScoped<INotificationTemplateRepository, EfCoreNotificationTemplateRepository>();
        services.AddScoped<INotificationPreferenceRepository, EfCoreNotificationPreferenceRepository>();

        // 模板渲染器
        services.AddScoped<ITemplateRenderer, TemplateRenderer>();
        services.AddScoped<ITemplateRenderService, TemplateRenderer>();

        // 用户联系方式防腐层（通过 HTTP 调用用户域内部端点获取手机号/邮箱）
        var userAuthApiUrl = configuration["ServiceUrls:UserAuthApi"] ?? "http://localhost:5173";
        services.AddHttpClient<IUserContactService, UserContactAntiCorruptionService>(c => c.BaseAddress = new Uri(userAuthApiUrl));

        // 通知渠道配置
        services.Configure<EmailChannelOptions>(configuration.GetSection("Notification:Email"));
        services.Configure<SmsChannelOptions>(configuration.GetSection("Notification:Sms"));

        // 通知渠道实现
        services.AddHttpClient<AliyunSmsChannel>();
        services.AddHttpClient<TencentSmsChannel>();
        services.AddScoped<INotificationChannel, InAppChannel>();
        services.AddScoped<INotificationChannel, SmtpEmailChannel>();
        services.AddScoped<INotificationChannel, AliyunSmsChannel>();
        services.AddScoped<INotificationChannel, TencentSmsChannel>();

        // 重试策略
        services.AddSingleton<Domain.Services.IRetryPolicy, Infrastructure.Services.RetryPolicy>();

        // 频率限制器
        services.AddSingleton<Domain.Services.IRateLimiter, Infrastructure.Services.RedisRateLimiter>();

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

		return services;
    }

    public static IBusRegistrationConfigurator AddNotificationConsumers(
        this IBusRegistrationConfigurator configurator)
    {
        ArgumentNullException.ThrowIfNull(configurator);

        configurator.AddConsumer<UserEventConsumer>();
        configurator.AddConsumer<OrderEventConsumer>();
        configurator.AddConsumer<PaymentEventConsumer>();
        configurator.AddConsumer<PromotionEventConsumer>();
        configurator.AddConsumer<PointsEventConsumer>();
        configurator.AddConsumer<AfterSalesEventConsumer>();
        configurator.AddConsumer<NotificationEventConsumer>();

        return configurator;
    }
}