using Leno.Infrastructure.AntiCorruption;
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
using Leno.Payment.Infrastructure.Services.Grpc;
using Leno.SharedContracts.Grpc.Order.V1;
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
        // 阶段三 3.8 插件化：绑定 Payment:Plugins 节，支持 EnabledChannels 白名单 + PluginAssemblies 动态加载
        services.Configure<PaymentChannelPluginOptions>(configuration.GetSection("Payment:Plugins"));
        services.AddSingleton<IChannelConfigProvider, ChannelConfigProvider>();

        // 渠道 HTTP 客户端（通过 IHttpClientFactory 注入 HttpClient）
        services.AddHttpClient<WeChatPayClient>();
        services.AddHttpClient<AlipayClient>();

        // 渠道适配器（具体类注册，供 NotifyHandler 直接依赖）
        services.AddScoped<WeChatPayAdapter>();
        services.AddScoped<AlipayAdapter>();

        // 阶段三 3.8 插件化：将各适配器注册为 IPaymentChannelAdapter，
        // 使 IEnumerable<IPaymentChannelAdapter> 解析得到全部已注册适配器，
        // PaymentChannelFactory / PaymentChannelRegistry 据此构建按 ChannelKey 查找字典，无需 switch/if-else。
        // WeChatPayNotifyHandler 改为直接依赖 WeChatPayAdapter 具体类（与 AlipayNotifyHandler 对称），
        // 不再依赖 IPaymentChannelAdapter 单注入，避免多注册歧义。
        services.AddScoped<IPaymentChannelAdapter>(sp => sp.GetRequiredService<WeChatPayAdapter>());
        services.AddScoped<IPaymentChannelAdapter>(sp => sp.GetRequiredService<AlipayAdapter>());

        // 动态加载外部插件程序集中的适配器类型并注册为 IPaymentChannelAdapter
        AddPaymentChannelPlugins(services, configuration);

        // 工厂与注册表
        services.AddScoped<PaymentChannelFactory>();
        services.AddScoped<IPaymentChannelFactory>(sp => sp.GetRequiredService<PaymentChannelFactory>());
        services.AddScoped<PaymentChannelRegistry>();
        services.AddScoped<IPaymentChannelRegistry>(sp => sp.GetRequiredService<PaymentChannelRegistry>());
        services.AddSingleton<PaymentChannelPluginLoader>();

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

        // 订单支付上下文防腐层：注册 gRPC 客户端与 GrpcPaymentOrderAntiCorruptionService 实现。
        // 默认从 AntiCorruption:GrpcEndpoints:Order 读取订单域 gRPC 端点；缺失时回退到本地开发地址 http://localhost:5154。
        var antiCorruptionOptions = configuration.GetSection("AntiCorruption").Get<AntiCorruptionOptions>() ?? new AntiCorruptionOptions();
        var orderGrpcEndpoint = antiCorruptionOptions.GrpcEndpoints.GetValueOrDefault("Order") ?? "http://localhost:5154";
        services.AddGrpcClient<OrderInternalService.OrderInternalServiceClient>(options =>
        {
            options.Address = new Uri(orderGrpcEndpoint);
        });
        services.AddScoped<IPaymentOrderAntiCorruptionService, GrpcPaymentOrderAntiCorruptionService>();

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

    /// <summary>
    /// 阶段三 3.8 插件化：动态加载外部插件程序集并注册适配器类型到 DI。
    /// 在 DI 配置阶段同步执行 <see cref="PaymentChannelPluginLoader.Load"/> 扫描插件程序集，
    /// 将识别到的适配器类型注册为 <see cref="IPaymentChannelAdapter"/>，
    /// 使 <c>IEnumerable&lt;IPaymentChannelAdapter&gt;</c> 解析时包含插件适配器。
    /// </summary>
    /// <param name="services">DI 容器。</param>
    /// <param name="configuration">应用配置。</param>
    private static void AddPaymentChannelPlugins(IServiceCollection services, IConfiguration configuration)
    {
        var pluginOptions = configuration
            .GetSection("Payment:Plugins")
            .Get<PaymentChannelPluginOptions>() ?? new PaymentChannelPluginOptions();

        if (pluginOptions.PluginAssemblies.Count == 0)
        {
            return;
        }

        // DI 配置阶段无法解析 ILogger<T>，使用 NullLogger 兜底；
        // 加载失败项记录到返回结果的 Failures，不阻塞启动。
        var loader = new PaymentChannelPluginLoader(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<PaymentChannelPluginLoader>.Instance);
        var loadResult = loader.Load(pluginOptions);

        foreach (var adapterType in loadResult.AdapterTypes)
        {
            // 插件适配器类型注册为 IPaymentChannelAdapter，
            // 由 DI 容器在解析 IEnumerable<IPaymentChannelAdapter> 时构造实例。
            // 适配器类型的构造函数依赖由 DI 自动解析（需在插件侧注册或依赖共享服务）。
            services.AddScoped(typeof(IPaymentChannelAdapter), adapterType);
        }
    }
}
