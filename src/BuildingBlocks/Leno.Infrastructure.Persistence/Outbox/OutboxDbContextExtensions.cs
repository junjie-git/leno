using Leno.Infrastructure.EventBus;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Leno.Infrastructure.Outbox;

/// <summary>
/// DbContext 发件箱扩展：在保存聚合变更的同一事务内，将聚合产生的集成事件写入发件箱表。
/// </summary>
public static class OutboxDbContextExtensions
{
    /// <summary>
    /// 收集变更跟踪器中所有聚合根的领域事件，将其中的集成事件转为发件箱记录，
    /// 与聚合状态变更在同一事务保存，保存完成后清除领域事件。
    /// 双发期兼容：优先使用 <paramref name="mapper"/> 翻译领域事件；若 mapper 为 null 或翻译返回 null，
    /// 回退到旧的 <c>is IIntegrationEvent</c> 双身份模式（下线后移除）。
    /// </summary>
    public static async Task<int> SaveChangesWithOutboxAsync(
        this DbContext context,
        IIntegrationEventMapper? mapper = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var aggregates = context.ChangeTracker.Entries<AggregateRoot>()
            .Select(e => e.Entity)
            .ToList();

        foreach (var aggregate in aggregates)
        {
            foreach (var domainEvent in aggregate.DomainEvents.ToList())
            {
                IIntegrationEvent? integrationEvent = null;

                // 双发期兼容：先尝试通过 mapper 翻译，回退到旧 is IIntegrationEvent 模式
                if (mapper is not null)
                {
                    integrationEvent = mapper.Map(domainEvent);
                }

                // 旧模式回退（双发期内保留，下线后移除）
                if (integrationEvent is null && domainEvent is IIntegrationEvent legacyEvent)
                {
                    integrationEvent = legacyEvent;
                }

                if (integrationEvent is not null)
                {
                    context.Set<OutboxMessage>().Add(OutboxMessage.Create(integrationEvent));
                }
            }
        }

        var result = await context.SaveChangesAsync(ct);

        foreach (var aggregate in aggregates)
        {
            aggregate.ClearDomainEvents();
        }

        return result;
    }

    /// <summary>
    /// 收集变更跟踪器中所有聚合根的领域事件，将其中的集成事件转为带分片信息的发件箱记录，
    /// 与聚合状态变更在同一事务保存，保存完成后清除领域事件。
    /// <para>
    /// 4.4 Outbox 分片发布器：通过 <paramref name="shardingStrategy"/> 按聚合根 ID 计算
    /// <see cref="OutboxMessage.ShardKey"/>，保证同一聚合根的事件始终由同一实例顺序发布。
    /// </para>
    /// <para>
    /// 双发期兼容：优先使用 <paramref name="mapper"/> 翻译领域事件；若 mapper 为 null 或翻译返回 null，
    /// 回退到旧的 <c>is IIntegrationEvent</c> 双身份模式（下线后移除）。
    /// </para>
    /// </summary>
    /// <param name="context">DbContext 实例。</param>
    /// <param name="mapper">领域事件 → 集成事件翻译器；null 时回退到 is IIntegrationEvent 模式。</param>
    /// <param name="shardingStrategy">分片策略；null 时所有消息落到分片 0（兼容单实例模式）。</param>
    /// <param name="shardCount">分片总数；&lt;= 1 时所有消息落到分片 0。</param>
    /// <param name="ct">取消令牌。</param>
    public static async Task<int> SaveChangesWithOutboxAsync(
        this DbContext context,
        IIntegrationEventMapper? mapper,
        IShardingStrategy? shardingStrategy,
        int shardCount,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var aggregates = context.ChangeTracker.Entries<AggregateRoot>()
            .Select(e => e.Entity)
            .ToList();

        foreach (var aggregate in aggregates)
        {
            foreach (var domainEvent in aggregate.DomainEvents.ToList())
            {
                IIntegrationEvent? integrationEvent = null;

                if (mapper is not null)
                {
                    integrationEvent = mapper.Map(domainEvent);
                }

                if (integrationEvent is null && domainEvent is IIntegrationEvent legacyEvent)
                {
                    integrationEvent = legacyEvent;
                }

                if (integrationEvent is not null)
                {
                    // 用聚合根 ID 作为分片哈希输入，保证同一聚合根事件落到同一分片
                    var outboxMessage = OutboxMessage.Create(
                        integrationEvent,
                        aggregate.Id,
                        shardingStrategy,
                        shardCount);
                    context.Set<OutboxMessage>().Add(outboxMessage);
                }
            }
        }

        var result = await context.SaveChangesAsync(ct);

        foreach (var aggregate in aggregates)
        {
            aggregate.ClearDomainEvents();
        }

        return result;
    }
}
