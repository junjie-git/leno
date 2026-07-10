using Leno.Payment.Domain.Repositories;
using Leno.Payment.Domain.Services;
using Leno.Payment.Infrastructure.Channels;
using Leno.Payment.Infrastructure.Channels.Alipay;
using Leno.Payment.Infrastructure.Channels.WeChatPay;
using Leno.Payment.Infrastructure.Config;
using Leno.Payment.Infrastructure.Consumers;
using Leno.Payment.Infrastructure.Jobs;
using Leno.Payment.Infrastructure.Notify;
using Leno.Payment.Infrastructure.Repositories;
using Leno.SharedKernel.Abstractions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Leno.Payment.Infrastructure.Dependencies;

/// <summary>
/// 支付域基础设施层 DI 注册入口。
/// 注册 DbContext、工作单元、仓储、渠道配置、渠道适配器、通知处理器与补偿任务。
/// 调用方在表现层 Program.cs 调用 <c>services.AddPaymentInfrastructure(configuration)</c>。
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <param name="connectionStringName">连接字符串名称，默认 <c>PaymentDb</c>。</param>
    public static IServiceCollection AddPaymentInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        string connectionStringName = "PaymentDb")
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddDbContext<PaymentDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString(connectionStringName);
            options.UseSqlServer(connectionString);
        });

        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddScoped<IPaymentOrderRepository, EfCorePaymentOrderRepository>();
        services.AddScoped<IRefundOrderRepository, EfCoreRefundOrderRepository>();

        // 渠道配置
        services.Configure<PaymentChannelOptions>(configuration.GetSection("Payment:Channels"));
        services.AddSingleton<IChannelConfigProvider, ChannelConfigProvider>();

        // 渠道 HTTP 客户端（通过 IHttpClientFactory 注入 HttpClient）
        services.AddHttpClient<WeChatPayClient>();
        services.AddHttpClient<AlipayClient>();

        // 渠道适配器
        services.AddScoped<WeChatPayAdapter>();
        services.AddScoped<AlipayAdapter>();
        services.AddScoped<PaymentChannelFactory>();

        // 异步通知处理器
        services.AddScoped<WeChatPayNotifyHandler>();
        services.AddScoped<AlipayNotifyHandler>();

        // 补偿任务
        services.AddScoped<PaymentStatusCheckJob>();
        services.AddScoped<RefundStatusCheckJob>();

        return services;
    }

    /// <summary>
    /// 注册支付域的 MassTransit 消费者（集成事件消费者）。
    /// 在表现层调用 <c>AddLenoInfrastructure(configuration, cfg => cfg.AddPaymentConsumers())</c>。
    /// </summary>
    public static IBusRegistrationConfigurator AddPaymentConsumers(
        this IBusRegistrationConfigurator configurator)
    {
        ArgumentNullException.ThrowIfNull(configurator);

        configurator.AddConsumer<PaymentRequestedEventConsumer>();
        configurator.AddConsumer<RefundRequestedEventConsumer>();

        return configurator;
    }
}
