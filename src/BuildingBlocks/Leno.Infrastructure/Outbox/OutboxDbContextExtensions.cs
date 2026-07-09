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
    /// </summary>
    public static async Task<int> SaveChangesWithOutboxAsync(this DbContext context, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var aggregates = context.ChangeTracker.Entries<AggregateRoot>()
            .Select(e => e.Entity)
            .ToList();

        foreach (var aggregate in aggregates)
        {
            foreach (var domainEvent in aggregate.DomainEvents.ToList())
            {
                if (domainEvent is IIntegrationEvent integrationEvent)
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
}
