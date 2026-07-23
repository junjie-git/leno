using Leno.Order.Application.Sagas.States;
using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Leno.Order.Infrastructure.Sagas;

/// <summary>
/// Order Saga 持久化注册扩展，复用 <see cref="OrderDbContext"/> 作为 EF Core Saga 存储上下文。
/// MassTransit 8.x 的 <c>EntityFrameworkSagaRepository&lt;TDbContext&gt;</c> 直接读写 Saga 实体表，
/// 无需独立的 SagaDbContext；Saga 实体 <see cref="OrderSagaState"/> 通过
/// <see cref="Configurations.OrderSagaStateConfiguration"/> 在 <see cref="OrderDbContext.OnModelCreating"/> 中映射。
/// </summary>
public static class OrderSagaRepository
{
    /// <summary>
    /// 注册 <see cref="OrderSagaState"/> 的 EF Core Saga Repository，复用 <see cref="OrderDbContext"/>。
    /// 在 <c>AddSagaStateMachine&lt;TStateMachine, TState&gt;</c> 之后链式调用
    /// <c>.EntityFrameworkRepository(r => r.ExistingDbContext&lt;OrderDbContext&gt;())</c> 即可启用持久化。
    /// </summary>
    /// <remarks>
    /// 使用 ExistingDbContext 模式：MassTransit 复用 DI 容器中已注册的 OrderDbContext，
    /// Saga 状态变更与业务实体变更可共享同一 DbContext 事务边界（如需）。
    /// Saga 实体的乐观锁由 <see cref="OrderSagaState.Version"/>（rowversion）保证，
    /// MassTransit EF Saga Repository 在 SaveChanges 时检测并发冲突并重试。
    /// </remarks>
    public static void UseOrderSagaRepository<TState>(
        this ISagaRegistrationConfigurator<TState> configurator)
        where TState : class, SagaStateMachineInstance
    {
        ArgumentNullException.ThrowIfNull(configurator);

        configurator.EntityFrameworkRepository(r =>
        {
            r.ExistingDbContext<OrderDbContext>();
            r.UseSqlServer();
        });
    }
}
