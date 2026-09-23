using FluentValidation;
using Leno.Infrastructure.Cqrs;
using Leno.Infrastructure.EventBus;
using Leno.Infrastructure.Persistence;
using Leno.Inventory.Application;
using Leno.Inventory.Application.Services;
using Leno.Inventory.Domain.Repositories;
using Leno.Inventory.Domain.Services;
using Leno.Inventory.Infrastructure.Consumers;
using Leno.Inventory.Infrastructure.EventBus;
using Leno.Inventory.Infrastructure.Repositories;
using Leno.Inventory.Infrastructure.Services;
using Leno.SharedKernel.Abstractions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Leno.Inventory.Infrastructure.Dependencies;

/// <summary>
/// Inventory BC 基础设施层 DI 注册入口。
/// <para>
/// 双轨下线 DEC-4（2026-09-22）后的形态：库存以 SQL 单库事务为唯一权威
/// （台账 + 基线原子 UPDATE 同事务），Redis 仅保留秒杀配额通道；
/// 旧的 Redis 直写仓储、库存域服务、补偿后台服务、对账后台服务已随双轨一并下线。
/// </para>
/// 调用方在表现层 Program.cs 调用 <c>services.AddInventoryInfrastructure(configuration)</c>。
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <param name="connectionStringName">连接字符串名称，默认 <c>InventoryDb</c>。</param>
    public static IServiceCollection AddInventoryInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        string connectionStringName = "InventoryDb")
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddDbContext<InventoryDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString(connectionStringName);
            options.UseSqlServer(connectionString);
        });

        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork<InventoryDbContext>>();

        // 领域事件 → 集成事件翻译器（Outbox 同事务发布时由 UnitOfWork 调用）
        services.AddSingleton<IIntegrationEventMapper, InventoryIntegrationEventMapper>();

        // 仓储：台账（订单 × SKU 占用记录，幂等与审计的唯一事实来源）+ 基线（SKU 计数器，原子条件 UPDATE）
        services.AddScoped<IStockReservationRepository, EfCoreStockReservationRepository>();
        services.AddScoped<IStockBaselineRepository, EfCoreStockBaselineRepository>();

        // 应用服务（预占/确认/释放/归还四用例，同事务更新台账与基线）
        services.AddScoped<IInventoryAppService, InventoryAppService>();
        // 秒杀库存应用服务（**当前无调用方**：秒杀配额仍由 Promotion 自己的 Redis 实现承担；
        // 库存台账由 Order 秒杀建单写入 —— 保留/删除待单独决策，2026-09-24 复核）
        services.AddScoped<ISeckillStockAppService, SeckillStockAppService>();

        // 秒杀库存 Redis 原子层（Redis 在本 BC 仅存的职责：秒杀配额，SQL 为结算点）
        services.AddScoped<ISeckillStockService, RedisSeckillStockService>();

        // FluentValidation 校验器
        services.AddValidatorsFromAssembly(typeof(IInventoryAppService).Assembly);

        // CQRS 读侧：扫描 Application 程序集注册所有 IQueryHandler<TQuery, TResult>
        services.AddQueryHandlers(typeof(IInventoryAppService).Assembly);

        return services;
    }

    /// <summary>
    /// 注册 Inventory BC 的 MassTransit 消费者（集成命令 + 集成事件消费者）。
    /// 在表现层调用 <c>AddLenoInfrastructure(configuration, cfg => cfg.AddInventoryConsumers())</c>。
    /// </summary>
    public static IBusRegistrationConfigurator AddInventoryConsumers(
        this IBusRegistrationConfigurator configurator)
    {
        ArgumentNullException.ThrowIfNull(configurator);

        // 集成命令消费者（Order BC → Inventory BC）
        configurator.AddConsumer<ReserveStockCommandConsumer>();
        configurator.AddConsumer<ConfirmStockCommandConsumer>();
        configurator.AddConsumer<ReleaseStockCommandConsumer>();

        // 集成事件消费者（Product BC → Inventory BC，基线的唯一写入方）
        configurator.AddConsumer<StockAdjustedEventConsumer>();

        return configurator;
    }
}
