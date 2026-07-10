using Leno.Notification.Domain.Repositories;
using Leno.Notification.Domain.Services;
using Leno.Notification.Infrastructure.Channels;
using Leno.Notification.Infrastructure.Channels.Email;
using Leno.Notification.Infrastructure.Channels.Sms;
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

        // 通知渠道
        services.Configure<SmsOptions>(configuration.GetSection("Notification:Sms"));
        services.Configure<EmailOptions>(configuration.GetSection("Notification:Email"));
        services.AddScoped<SmsClient>();
        services.AddScoped<SmtpClientWrapper>();
        services.AddScoped<IChannel, InAppChannel>();
        services.AddScoped<IChannel, SmsChannel>();
        services.AddScoped<IChannel, EmailChannel>();

        // 通知调度器
        services.AddScoped<INotificationDispatcher, NotificationDispatcher>();

        // 调度与重试任务
        services.AddScoped<NotificationDispatchJob>();
        services.AddScoped<NotificationRetryJob>();

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

        return configurator;
    }
}
