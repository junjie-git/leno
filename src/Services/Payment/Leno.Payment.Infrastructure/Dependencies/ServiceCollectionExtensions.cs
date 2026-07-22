using Leno.Infrastructure.EventBus;
using Leno.Infrastructure.Persistence;
using Leno.Payment.Application;
using Leno.Payment.Application.Services;
using Leno.Payment.Domain.Repositories;
using Leno.Payment.Domain.Services;
using Leno.Payment.Infrastructure.Channels;
using Leno.Payment.Infrastructure.Channels.Alipay;
using Leno.Payment.Infrastructure.Channels.WeChatPay;
using Leno.Payment.Infrastructure.Config;
using Leno.Payment.Infrastructure.Consumers;
using Leno.Payment.Infrastructure.EventBus;
using Leno.Payment.Infrastructure.Jobs;
using Leno.Payment.Infrastructure.Notify;
using Leno.Payment.Infrastructure.Repositories;
using Leno.Payment.Infrastructure.Services;
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

        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork<PaymentDbContext>>();

        // 注册 Payment BC 领域事件到集成事件翻译器
        services.AddSingleton<IIntegrationEventMapper, PaymentIntegrationEventMapper>();

        services.AddScoped<IPaymentOrderRepository, EfCorePaymentOrderRepository>();
        services.AddScoped<IRefundOrderRepository, EfCoreRefundOrderRepository>();
        services.AddScoped<IReconciliationDiffRepository, EfCoreReconciliationDiffRepository>();
        services.AddScoped<IPaymentChannelConfigRepository, EfCorePaymentChannelConfigRepository>();

        // 渠道配置
        services.Configure<PaymentChannelOptions>(configuration.GetSection("Payment:Channels"));
        services.Configure<AlipayOptions>(configuration.GetSection("Payment:Alipay"));
        services.Configure<WeChatPayOptions>(configuration.GetSection("Payment:WeChatPayV3"));
        // 补偿任务配置（P2-20）：绑定 Payment:Jobs 节，允许按环境调整 ThresholdMinutes/BatchSize
        services.Configure<PaymentJobOptions>(configuration.GetSection("Payment:Jobs"));
        services.AddSingleton<IChannelConfigProvider, ChannelConfigProvider>();

        // 渠道 HTTP 客户端（通过 IHttpClientFactory 注入 HttpClient）
        services.AddHttpClient<WeChatPayClient>();
        services.AddHttpClient<AlipayClient>();

        // 渠道适配器
        services.AddScoped<WeChatPayAdapter>();
        services.AddScoped<AlipayAdapter>();
        services.AddScoped<PaymentChannelFactory>();
        // WeChatPayNotifyHandler 依赖 IPaymentChannelAdapter 抽象（P0-1），将其解析为 WeChatPayAdapter 具体实现。
        // AlipayNotifyHandler 仍直接依赖 AlipayAdapter 具体类，不受此注册影响。
        services.AddScoped<IPaymentChannelAdapter>(sp => sp.GetRequiredService<WeChatPayAdapter>());
        // PaymentRequestedEventConsumer 依赖 IPaymentChannelFactory 抽象（P0-6），解析为 PaymentChannelFactory 具体实现，
        // 与同 scope 的 PaymentChannelFactory 共享实例。
        services.AddScoped<IPaymentChannelFactory>(sp => sp.GetRequiredService<PaymentChannelFactory>());

        // 渠道签名验证（供表现层验签后处理业务）
        services.AddScoped<WeChatPayChannel>();
        services.AddScoped<AlipayChannel>();

        // 异步通知处理器
        services.AddScoped<WeChatPayNotifyHandler>();
        services.AddScoped<AlipayNotifyHandler>();

        // 补偿任务
        services.AddScoped<PaymentStatusCheckJob>();
        services.AddScoped<RefundStatusCheckJob>();

        // 防腐层实现
        services.AddScoped<IChannelStatusQueryService, ChannelStatusQueryService>();

        // 应用服务
        services.AddScoped<IPaymentAppService, PaymentAppService>();
        services.AddScoped<IRefundAppService, RefundAppService>();
        services.AddScoped<IReconciliationAppService, ReconciliationAppService>();
        services.AddScoped<IPaymentChannelConfigAppService, PaymentChannelConfigAppService>();

        // 内部查询服务（供跨域调用）
        services.AddScoped<IPaymentInternalQueryService, PaymentInternalQueryService>();

        // 对账服务（后台服务）
        services.AddSingleton<ReconciliationService>();
        services.AddSingleton<IReconciliationService>(sp => sp.GetRequiredService<ReconciliationService>());
        services.AddHostedService(sp => sp.GetRequiredService<ReconciliationService>());

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
