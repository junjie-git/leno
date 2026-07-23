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
/// 注册 DbContext、工作单元、仓储（EF Core + Redis 双写）、领域服务、应用服务、
/// FluentValidation 校验器、MassTransit 消费者与库存对账/补偿后台服务。
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

        // 仓储：EF Core 聚合审计源 + Redis 原子层双写
        services.AddScoped<IStockReservationRepository, EfCoreStockReservationRepository>();
        services.AddScoped<IStockBaselineRepository, EfCoreStockBaselineRepository>();
        services.AddScoped<IStockReservationCompensationRepository, EfCoreStockReservationCompensationRepository>();
        services.AddScoped<IInventoryRepository, RedisInventoryRepository>();

        // 领域服务
        services.AddScoped<IStockReservationDomainService, StockReservationDomainService>();

        // 应用服务
        services.AddScoped<IInventoryAppService, InventoryAppService>();
        services.AddScoped<IOrderReservationQueryService, RedisOrderReservationQueryService>();
        // 秒杀库存应用服务（Promotion BC 秒杀库存迁移为遗留项，待 Promotion 规则引擎任务完成后单独迁移调用方）
        services.AddScoped<ISeckillStockAppService, SeckillStockAppService>();

        // 秒杀库存 Redis 原子层（从 Promotion BC 迁入的新实现，旧实现保留不动）
        services.AddScoped<ISeckillStockService, RedisSeckillStockService>();

        // FluentValidation 校验器
        services.AddValidatorsFromAssembly(typeof(IInventoryAppService).Assembly);

        // CQRS 读侧：扫描 Application 程序集注册所有 IQueryHandler<TQuery, TResult>
        services.AddQueryHandlers(typeof(IInventoryAppService).Assembly);

        // 库存对账后台服务（扫描 Redis 库存键，校验可用库存与预占之和是否匹配基线）
        services.AddHostedService<StockReconciliationService>();

        // T18: 库存预占回滚补偿后台服务，定期重试 Pending 补偿记录释放库存
        services.Configure<StockReservationCompensationOptions>(
            configuration.GetSection("StockReservationCompensation"));
        services.AddHostedService<StockReservationCompensationBackgroundService>();

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

        // 集成事件消费者（Product BC → Inventory BC，同步库存基线）
        configurator.AddConsumer<StockAdjustedEventConsumer>();

        return configurator;
    }
}
